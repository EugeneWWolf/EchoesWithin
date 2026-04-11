using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Фабрика для создания предметов различных типов
/// Использует Factory паттерн для создания предметов
/// </summary>
public class ItemFactory : MonoBehaviour
{
    [Header("Item Prefabs")]
    [SerializeField] private GameObject[] sellableItemPrefabs;
    [SerializeField] private GameObject[] buffItemPrefabs;
    [SerializeField] private GameObject[] weaponPrefabs;

    [Header("Factory Settings")]
    [SerializeField] private bool useObjectPooling = true;
    [SerializeField] private int poolSize = 50;

    [Header("Данж: позиция на полу")]
    [Tooltip("Пивот префаба часто в центре меша; нода даёт точку на полу. Без физики предмет не «оседает» — сдвигаем по нижней границе рендера к полу (луч по слою данжа).")]
    [SerializeField] private bool snapLootBottomToDungeonFloor = true;

    [Header("Производительность")]
    [Tooltip("Логи при каждом спавне/взятии из пула сильно грузят редактор. Выкл. по умолчанию.")]
    [SerializeField] private bool verboseItemFactoryLogs;

    private Dictionary<ItemType, Queue<GameObject>> objectPools;

    private static readonly List<Renderer> RendererScratch = new List<Renderer>(24);
    private static readonly List<Collider> ColliderScratch = new List<Collider>(12);

    private void Awake()
    {
        InitializeObjectPools();
    }

    private void InitializeObjectPools()
    {
        if (!useObjectPooling) return;

        objectPools = new Dictionary<ItemType, Queue<GameObject>>();

        // Создаем пулы для каждого типа предметов
        CreatePool(ItemType.SellableItem, sellableItemPrefabs);
        CreatePool(ItemType.BuffItem, buffItemPrefabs);
        CreatePool(ItemType.Weapon, weaponPrefabs);

        if (verboseItemFactoryLogs)
            Debug.Log($"🏭 ItemFactory: Инициализированы пулы объектов. Размер пула: {poolSize}");
    }

    private void CreatePool(ItemType itemType, GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0) return;

        Queue<GameObject> pool = new Queue<GameObject>();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject randomPrefab = prefabs[Random.Range(0, prefabs.Length)];
            GameObject pooledObject = Instantiate(randomPrefab);
            pooledObject.SetActive(false);
            pooledObject.transform.SetParent(transform);
            pool.Enqueue(pooledObject);
        }

        objectPools[itemType] = pool;
        if (verboseItemFactoryLogs)
            Debug.Log($"🏭 ItemFactory: Создан пул для {itemType} с {pool.Count} объектами");
    }

    /// <summary>
    /// Создает предмет указанного типа
    /// </summary>
    public GameObject CreateItem(ItemType itemType, ItemSpawnData spawnData)
    {
        GameObject item = null;

        if (useObjectPooling && objectPools.ContainsKey(itemType) && objectPools[itemType].Count > 0)
        {
            // Используем объект из пула
            item = objectPools[itemType].Dequeue();
            item.SetActive(true);
            if (verboseItemFactoryLogs)
                Debug.Log($"🏭 ItemFactory: Взят объект из пула для {itemType}");
        }
        else
        {
            // Создаем новый объект
            item = CreateNewItem(itemType, spawnData);
            if (verboseItemFactoryLogs)
                Debug.Log($"🏭 ItemFactory: Создан новый объект для {itemType}");
        }

        if (item != null)
        {
            SetupItem(item, spawnData);
        }

        return item;
    }

    private GameObject CreateNewItem(ItemType itemType, ItemSpawnData spawnData)
    {
        GameObject[] prefabs = GetPrefabsForType(itemType);
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError($"❌ ItemFactory: Нет префабов для типа {itemType}");
            return null;
        }

        GameObject randomPrefab = prefabs[Random.Range(0, prefabs.Length)];
        return Instantiate(randomPrefab);
    }

    private GameObject[] GetPrefabsForType(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.SellableItem:
                return sellableItemPrefabs;
            case ItemType.BuffItem:
                return buffItemPrefabs;
            case ItemType.Weapon:
                return weaponPrefabs;
            default:
                return null;
        }
    }

    private void SetupItem(GameObject item, ItemSpawnData spawnData)
    {
        // Устанавливаем позицию
        item.transform.position = spawnData.position;
        item.transform.rotation = spawnData.rotation;

        // Добавляем базовый компонент Item
        Item baseItem = item.GetComponent<Item>();
        if (baseItem == null)
        {
            baseItem = item.AddComponent<Item>();
        }

        baseItem.itemName = spawnData.itemName;
        baseItem.price = spawnData.price;
        baseItem.description = spawnData.description;
        baseItem.itemType = spawnData.itemType;

        // Добавляем специфичные компоненты
        AddSpecificComponents(item, spawnData);

        // Настраиваем коллайдер
        SetupCollider(item);

        // Устанавливаем слой
        SetupLayer(item);

        // Коллизия лута с NavMeshAgent монстра ломает путь; динамический RB + не-триггер здесь не нужны — позицию задаёт спавнер.
        SetupPhysics(item, spawnData.itemType);

        if (snapLootBottomToDungeonFloor)
            SnapLootBottomToDungeonFloor(item);

        if (verboseItemFactoryLogs)
            Debug.Log($"🏭 ItemFactory: Настроен предмет {spawnData.itemName} в позиции {spawnData.position}");
    }

    private void AddSpecificComponents(GameObject item, ItemSpawnData spawnData)
    {
        switch (spawnData.itemType)
        {
            case ItemType.SellableItem:
                if (item.GetComponent<SellableItem>() == null)
                {
                    item.AddComponent<SellableItem>();
                }
                break;

            case ItemType.BuffItem:
                BuffItem buffItem = item.GetComponent<BuffItem>();
                if (buffItem == null)
                {
                    buffItem = item.AddComponent<BuffItem>();
                }

                // Случайный бафф для лута в данже: скорость, урон, макс. HP (цвет похлёбки как в BuffLootVisuals)
                StatType[] availableStats = { StatType.Speed, StatType.Damage, StatType.Health };
                buffItem.statType = availableStats[Random.Range(0, availableStats.Length)];
                buffItem.statValue = spawnData.statValue;

                // Обновляем название предмета в зависимости от типа стата
                string statName = GetStatDisplayName(buffItem.statType);
                item.GetComponent<Item>().itemName = $"{statName} Potion (+{buffItem.statValue:F1})";
                item.GetComponent<Item>().description = $"Зелье, увеличивающее {statName.ToLower()} на {buffItem.statValue:F1}";

                BuffLootVisuals.ApplyTintToRenderers(item, buffItem.statType);

                if (verboseItemFactoryLogs)
                    Debug.Log($"🏭 ItemFactory: Создан {buffItem.statType} зелье со значением {buffItem.statValue:F1} (Speed/Damage/Health)");
                break;

            case ItemType.Weapon:
                Weapon weapon = item.GetComponent<Weapon>();
                if (weapon == null)
                {
                    weapon = item.AddComponent<Weapon>();
                }
                weapon.damage = spawnData.statValue;
                break;
        }
    }

    private void SetupCollider(GameObject item)
    {
        if (!item.GetComponent<Collider>())
        {
            BoxCollider collider = item.AddComponent<BoxCollider>();
            collider.isTrigger = true;
        }
    }

    private void SetupLayer(GameObject item)
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1)
        {
            item.layer = interactableLayer;
        }
    }

    /// <summary>
    /// Лут в данже ставится спавнером на пол. Твёрдый коллайдер + dynamic Rigidbody мешают NavMeshAgent (застревания, ломаная траектория).
    /// </summary>
    private void SetupPhysics(GameObject item, ItemType itemType)
    {
        Collider rootCollider = item.GetComponent<Collider>();

        switch (itemType)
        {
            case ItemType.SellableItem:
                if (item.TryGetComponent(out Rigidbody sellRb))
                    Destroy(sellRb);
                if (rootCollider != null)
                    rootCollider.isTrigger = true;
                break;

            case ItemType.BuffItem:
            case ItemType.Weapon:
            {
                Rigidbody rb = item.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = item.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                if (rootCollider != null)
                    rootCollider.isTrigger = true;
                break;
            }
        }

        EnsureInteractionTrigger(item, rootCollider);
    }

    private static void EnsureInteractionTrigger(GameObject item, Collider rootCollider)
    {
        const string triggerName = "InteractionTrigger";
        Transform existing = item.transform.Find(triggerName);
        if (existing != null)
        {
            if (existing.TryGetComponent(out ItemInteractionTrigger link))
                link.item = item;
            if (existing.TryGetComponent(out BoxCollider box))
            {
                box.isTrigger = true;
                if (rootCollider != null)
                    box.size = rootCollider.bounds.size * 1.2f;
            }

            return;
        }

        GameObject interactionTrigger = new GameObject(triggerName);
        interactionTrigger.transform.SetParent(item.transform, false);
        interactionTrigger.transform.localPosition = Vector3.zero;
        int interactable = LayerMask.NameToLayer("Interactable");
        if (interactable >= 0)
            interactionTrigger.layer = interactable;

        BoxCollider triggerCollider = interactionTrigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        if (rootCollider != null)
            triggerCollider.size = rootCollider.bounds.size * 1.2f;
        else
            triggerCollider.size = Vector3.one * 0.5f;

        interactionTrigger.AddComponent<ItemInteractionTrigger>().item = item;
    }

    /// <summary>
    /// Выравнивает предмет по вертикали: нижняя точка меша/коллайдеров к ближайшему полу данжа под объектом.
    /// </summary>
    private static void SnapLootBottomToDungeonFloor(GameObject item)
    {
        if (!TryGetWorldVisualBounds(item, out Bounds wb))
            return;

        int mask = DungeonFloorMaskForRay();
        float castHeight = Mathf.Clamp(wb.size.y + 3f, 5f, 40f);
        Vector3 origin = new Vector3(item.transform.position.x, wb.max.y + 0.35f, item.transform.position.z);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castHeight, mask, QueryTriggerInteraction.Ignore))
            return;

        const float skin = 0.02f;
        float dy = hit.point.y + skin - wb.min.y;
        if (Mathf.Abs(dy) <= 0.0005f)
            return;

        item.transform.position += new Vector3(0f, dy, 0f);
    }

    private static int DungeonFloorMaskForRay()
    {
        int d = ProceduralDungeonGenerator.DungeonCollisionLayerIndex;
        return (d >= 0 && d <= 31) ? (1 << d) : Physics.DefaultRaycastLayers;
    }

    private static bool TryGetWorldVisualBounds(GameObject item, out Bounds worldBounds)
    {
        RendererScratch.Clear();
        item.GetComponentsInChildren(true, RendererScratch);
        if (RendererScratch.Count > 0)
        {
            worldBounds = RendererScratch[0].bounds;
            for (int i = 1; i < RendererScratch.Count; i++)
                worldBounds.Encapsulate(RendererScratch[i].bounds);
            return true;
        }

        ColliderScratch.Clear();
        item.GetComponentsInChildren(true, ColliderScratch);
        if (ColliderScratch.Count > 0)
        {
            bool any = false;
            worldBounds = default;
            for (int i = 0; i < ColliderScratch.Count; i++)
            {
                Collider c = ColliderScratch[i];
                if (c == null || c.gameObject.name == "InteractionTrigger")
                    continue;
                if (!any)
                {
                    worldBounds = c.bounds;
                    any = true;
                }
                else
                    worldBounds.Encapsulate(c.bounds);
            }

            if (any)
                return true;
        }

        worldBounds = default;
        return false;
    }

    /// <summary>
    /// Получает отображаемое название для типа стата
    /// </summary>
    private string GetStatDisplayName(StatType statType)
    {
        switch (statType)
        {
            case StatType.Speed:
                return "Speed";
            case StatType.JumpHeight:
                return "Jump";
            case StatType.Damage:
                return "Damage";
            case StatType.Health:
                return "Health";
            case StatType.Gravity:
                return "Gravity";
            default:
                return "Unknown";
        }
    }

    /// <summary>
    /// Возвращает объект в пул для повторного использования
    /// </summary>
    public void ReturnToPool(GameObject item, ItemType itemType)
    {
        if (!useObjectPooling || !objectPools.ContainsKey(itemType))
        {
            Destroy(item);
            return;
        }

        item.SetActive(false);
        item.transform.SetParent(transform);
        objectPools[itemType].Enqueue(item);

        if (verboseItemFactoryLogs)
            Debug.Log($"🏭 ItemFactory: Объект {item.name} возвращен в пул {itemType}");
    }

    /// <summary>
    /// Создает случайный предмет
    /// </summary>
    public GameObject CreateRandomItem(Vector3 position)
    {
        ItemType[] availableTypes = { ItemType.SellableItem, ItemType.BuffItem, ItemType.Weapon };
        ItemType randomType = availableTypes[Random.Range(0, availableTypes.Length)];

        ItemSpawnData spawnData = new ItemSpawnData
        {
            itemType = randomType,
            position = position,
            rotation = Quaternion.identity,
            itemName = $"Random {randomType}",
            price = Random.Range(10, 100),
            description = $"Случайный предмет типа {randomType}",
            statType = StatType.Health,
            statValue = Random.Range(1f, 10f)
        };

        return CreateItem(randomType, spawnData);
    }

    // Метод для отладки
    [ContextMenu("Test Factory")]
    public void TestFactory()
    {
        Vector3 testPosition = transform.position + Vector3.right * 2f;
        GameObject testItem = CreateRandomItem(testPosition);
        Debug.Log($"🧪 ItemFactory: Создан тестовый предмет {testItem.name}");
    }
}
