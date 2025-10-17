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

    private Dictionary<ItemType, Queue<GameObject>> objectPools;

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
            Debug.Log($"🏭 ItemFactory: Взят объект из пула для {itemType}");
        }
        else
        {
            // Создаем новый объект
            item = CreateNewItem(itemType, spawnData);
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

        // Добавляем физику для BuffItem и Weapon
        SetupPhysics(item);

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

                // Случайно выбираем тип стата для BuffItem (только скорость и прыжок)
                // Другие типы зелий (урон, здоровье) можно создавать вручную
                StatType[] availableStats = { StatType.Speed, StatType.JumpHeight };
                buffItem.statType = availableStats[Random.Range(0, availableStats.Length)];
                buffItem.statValue = spawnData.statValue;

                // Обновляем название предмета в зависимости от типа стата
                string statName = GetStatDisplayName(buffItem.statType);
                item.GetComponent<Item>().itemName = $"{statName} Potion (+{buffItem.statValue:F1})";
                item.GetComponent<Item>().description = $"Зелье, увеличивающее {statName.ToLower()} на {buffItem.statValue:F1}";

                // Устанавливаем цвет зелья в зависимости от типа стата
                SetPotionColor(item, buffItem.statType);

                Debug.Log($"🏭 ItemFactory: Создан {buffItem.statType} зелье со значением {buffItem.statValue:F1} (фабрика создает только Speed/Jump зелья)");
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

    private void SetupPhysics(GameObject item)
    {
        // Добавляем Rigidbody для физики
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = item.AddComponent<Rigidbody>();
        }

        // Настраиваем физику
        rb.useGravity = true;
        rb.linearDamping = 1f; // Сопротивление воздуха
        rb.angularDamping = 2f; // Сопротивление вращению

        // Делаем основной коллайдер не триггером для физики
        Collider collider = item.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }

        // Добавляем триггер для взаимодействия
        GameObject interactionTrigger = new GameObject("InteractionTrigger");
        interactionTrigger.transform.SetParent(item.transform);
        interactionTrigger.transform.localPosition = Vector3.zero;
        interactionTrigger.layer = LayerMask.NameToLayer("Interactable");

        BoxCollider triggerCollider = interactionTrigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        if (collider != null)
        {
            triggerCollider.size = collider.bounds.size * 1.2f;
        }

        // Добавляем компонент для идентификации
        interactionTrigger.AddComponent<ItemInteractionTrigger>().item = item;

        Debug.Log($"🏭 ItemFactory: Добавлена физика для {item.name} (BuffItem/Weapon)");
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
    /// Устанавливает цвет зелья в зависимости от типа стата
    /// </summary>
    private void SetPotionColor(GameObject potion, StatType statType)
    {
        Renderer renderer = potion.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color potionColor = GetPotionColor(statType);
            renderer.material.color = potionColor;
            Debug.Log($"🎨 Установлен цвет зелья: {statType} = {potionColor}");
        }
    }

    /// <summary>
    /// Получает цвет зелья для типа стата
    /// </summary>
    private Color GetPotionColor(StatType statType)
    {
        switch (statType)
        {
            case StatType.Speed:
                return Color.blue; // Синий для скорости
            case StatType.JumpHeight:
                return Color.green; // Зеленый для прыжка
            case StatType.Damage:
                return Color.red; // Красный для урона (не используется фабрикой)
            case StatType.Health:
                return Color.yellow; // Желтый для здоровья (не используется фабрикой)
            case StatType.Gravity:
                return Color.magenta; // Пурпурный для гравитации (не используется фабрикой)
            default:
                return Color.white; // Белый по умолчанию
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
