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
                buffItem.statType = spawnData.statType;
                buffItem.statValue = spawnData.statValue;
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
