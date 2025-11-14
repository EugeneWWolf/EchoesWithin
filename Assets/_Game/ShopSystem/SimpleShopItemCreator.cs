using UnityEngine;

/// <summary>
/// Простой скрипт для создания предметов из готовых префабов
/// Прикрепите к GameObject с ShopZone для автоматического создания предметов
/// </summary>
public class SimpleShopItemCreator : MonoBehaviour
{
    [Header("Настройки создания предметов")]
    [SerializeField] private bool createItemsOnStart = true;
    [SerializeField] private Transform itemsParent; // Родитель для предметов

    [Header("Префабы предметов")]
    [SerializeField] private GameObject[] itemPrefabs; // Ваши готовые префабы

    [Header("Настройки предметов")]
    [SerializeField] private ShopItemData[] itemData; // Данные о предметах

    private ShopZone shopZone;

    [System.Serializable]
    public class ShopItemData
    {
        public string itemName;
        public int price;
        public ItemType type;
        public StatType statType;
        public float statValue;
        public string description;
    }

    private void Start()
    {
        shopZone = GetComponent<ShopZone>();

        if (createItemsOnStart)
        {
            CreateItemsFromPrefabs();
        }
    }

    private void CreateItemsFromPrefabs()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0)
        {
            Debug.LogWarning("⚠ SimpleShopItemCreator: Нет префабов для создания предметов!");
            return;
        }

        if (itemData == null || itemData.Length == 0)
        {
            Debug.LogWarning("⚠ SimpleShopItemCreator: Нет данных о предметах!");
            return;
        }

        if (shopZone == null)
        {
            Debug.LogError("❌ SimpleShopItemCreator: Не найден компонент ShopZone!");
            return;
        }

        GameObject[] createdItems = new GameObject[itemPrefabs.Length];

        for (int i = 0; i < itemPrefabs.Length && i < itemData.Length; i++)
        {
            if (itemPrefabs[i] != null)
            {
                GameObject item = Instantiate(itemPrefabs[i]);
                SetupItem(item, itemData[i]);
                createdItems[i] = item;

                Debug.Log($"✅ Создан предмет: {itemData[i].itemName}");
            }
        }

        // Назначаем созданные предметы в ShopZone
        shopZone.shopItems = createdItems;
        Debug.Log($"🔧 SimpleShopItemCreator: Назначили {createdItems.Length} предметов в ShopZone");

        // Принудительно размещаем предметы в лавке
        shopZone.PlaceShopItems();

        // Проверяем результат
        int activeItems = 0;
        for (int i = 0; i < createdItems.Length; i++)
        {
            if (createdItems[i] != null && createdItems[i].activeInHierarchy)
            {
                activeItems++;
            }
        }

        Debug.Log($"✅ SimpleShopItemCreator: Создано {createdItems.Length} предметов, активно: {activeItems}");
    }

    private void SetupItem(GameObject item, ShopItemData data)
    {
        // Устанавливаем родителя
        if (itemsParent != null)
        {
            item.transform.SetParent(itemsParent);
        }

        // Добавляем базовый компонент Item
        Item baseItem = item.GetComponent<Item>();
        if (baseItem == null)
        {
            baseItem = item.AddComponent<Item>();
        }

        baseItem.itemName = data.itemName;
        baseItem.price = data.price;
        baseItem.description = data.description;
        baseItem.itemType = data.type;

        // Добавляем специфичные компоненты в зависимости от типа
        switch (data.type)
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
                buffItem.statType = data.statType;
                buffItem.statValue = data.statValue;
                break;

            case ItemType.Weapon:
                Weapon weapon = item.GetComponent<Weapon>();
                if (weapon == null)
                {
                    weapon = item.AddComponent<Weapon>();
                }
                weapon.damage = data.statValue;
                break;
        }

        // Добавляем коллайдер если его нет
        if (!item.GetComponent<Collider>())
        {
            BoxCollider collider = item.AddComponent<BoxCollider>();
            collider.isTrigger = true;
        }

        // Устанавливаем слой
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1)
        {
            item.layer = interactableLayer;
        }

        // Добавляем отображение цены если его нет (ДО деактивации)
        if (!item.GetComponent<ItemPriceDisplay>())
        {
            var priceDisplay = item.AddComponent<ItemPriceDisplay>();
            // Устанавливаем ShopZone через SetShopZone, но не инициализируем сейчас
            // Инициализация произойдет автоматически при активации предмета
            priceDisplay.SetShopZone(shopZone);
            Debug.Log($"💰 SimpleShopItemCreator: Добавлен компонент отображения цены для {data.itemName}");
        }

        // Деактивируем предмет (ShopZone активирует его при размещении)
        // Это должно произойти ПОСЛЕ добавления компонента, чтобы OnEnable мог сработать при активации
        item.SetActive(false);
    }

    // Метод для ручного создания предметов
    [ContextMenu("Создать предметы из префабов")]
    public void CreateItemsManually()
    {
        CreateItemsFromPrefabs();
    }

    /// <summary>
    /// Создает случайный предмет для замены купленного
    /// </summary>
    public GameObject CreateRandomItem(Vector3 position)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0)
        {
            Debug.LogWarning("⚠ SimpleShopItemCreator: Нет префабов для создания предметов!");
            return null;
        }

        GameObject randomPrefab;
        ShopItemData randomData;

        // Если есть данные о предметах, используем их для выбора префаба и создания данных
        if (itemData != null && itemData.Length > 0 && itemData.Length == itemPrefabs.Length)
        {
            // Выбираем случайный индекс, чтобы префаб и данные соответствовали
            int randomIndex = Random.Range(0, itemData.Length);
            randomPrefab = itemPrefabs[randomIndex];
            randomData = CreateRandomItemDataFromBase(itemData[randomIndex]);
        }
        else
        {
            // Если данных нет или они не совпадают, выбираем случайный префаб и создаем полностью случайные данные
            randomPrefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];
            randomData = CreateRandomItemData();
        }

        if (randomPrefab == null)
        {
            Debug.LogWarning("⚠ SimpleShopItemCreator: Выбранный префаб равен null!");
            return null;
        }

        // Создаем предмет
        GameObject item = Instantiate(randomPrefab);
        SetupItem(item, randomData);

        // Устанавливаем позицию
        item.transform.position = position;

        Debug.Log($"✅ SimpleShopItemCreator: Создан случайный предмет: {randomData.itemName} в позиции {position}");
        return item;
    }

    /// <summary>
    /// Создает случайные данные для предмета на основе базовых данных
    /// </summary>
    private ShopItemData CreateRandomItemDataFromBase(ShopItemData baseData)
    {
        ItemType randomType = baseData.type;
        StatType randomStatType = baseData.statType;

        // Немного рандомизируем значения
        float randomStatValue = baseData.statValue * Random.Range(0.8f, 1.2f);
        int randomPrice = Mathf.RoundToInt(baseData.price * Random.Range(0.7f, 1.3f));
        string randomName = baseData.itemName;
        string randomDescription = baseData.description;

        // Для BuffItem рандомизируем тип стата
        if (randomType == ItemType.BuffItem)
        {
            StatType[] buffStats = { StatType.Speed, StatType.JumpHeight };
            randomStatType = buffStats[Random.Range(0, buffStats.Length)];
            // Обновляем название и описание для зелья
            string statName = randomStatType == StatType.Speed ? "Speed" : "Jump";
            randomName = $"{statName} Potion (+{randomStatValue:F1})";
            randomDescription = $"Зелье, увеличивающее {statName.ToLower()} на {randomStatValue:F1}";
        }

        return new ShopItemData
        {
            itemName = randomName,
            price = randomPrice,
            type = randomType,
            statType = randomStatType,
            statValue = randomStatValue,
            description = randomDescription
        };
    }

    /// <summary>
    /// Создает полностью случайные данные для предмета (когда нет базовых данных)
    /// </summary>
    private ShopItemData CreateRandomItemData()
    {
        // Создаем полностью случайные данные
        ItemType[] types = { ItemType.SellableItem, ItemType.BuffItem, ItemType.Weapon };
        ItemType randomType = types[Random.Range(0, types.Length)];

        StatType randomStatType;
        float randomStatValue;
        int randomPrice;
        string randomName;
        string randomDescription;

        // Для BuffItem рандомизируем тип стата
        if (randomType == ItemType.BuffItem)
        {
            StatType[] buffStats = { StatType.Speed, StatType.JumpHeight };
            randomStatType = buffStats[Random.Range(0, buffStats.Length)];
            randomStatValue = Random.Range(1f, 10f);
            string statName = randomStatType == StatType.Speed ? "Speed" : "Jump";
            randomName = $"{statName} Potion (+{randomStatValue:F1})";
            randomDescription = $"Зелье, увеличивающее {statName.ToLower()} на {randomStatValue:F1}";
        }
        else
        {
            StatType[] statTypes = { StatType.Speed, StatType.JumpHeight, StatType.Damage, StatType.Health };
            randomStatType = statTypes[Random.Range(0, statTypes.Length)];
            randomStatValue = Random.Range(1f, 10f);
            randomName = $"Random {randomType}";
            randomDescription = $"Случайный предмет типа {randomType}";
        }

        randomPrice = Random.Range(10, 100);

        return new ShopItemData
        {
            itemName = randomName,
            price = randomPrice,
            type = randomType,
            statType = randomStatType,
            statValue = randomStatValue,
            description = randomDescription
        };
    }
}
