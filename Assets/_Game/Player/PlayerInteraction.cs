using UnityEngine;

public class PlayerInteraction
{
    private readonly InventorySystem inventory;
    private readonly Transform cameraT;
    private readonly float interactDistance;
    private LayerMask interactLayer;
    private PlayerWallet wallet;
    private PlayerStats playerStats;

    // Кэшированные объекты для оптимизации
    private Ray ray;
    private RaycastHit hit;
    private GameObject lastHitObject;
    private float lastRaycastTime;
    private const float RAYCAST_COOLDOWN = 0.1f; // Ограничиваем частоту raycast

    public PlayerInteraction(InventorySystem inventory, Transform cameraT, PlayerSettings settings, PlayerStats playerStats)
    {
        this.inventory = inventory;
        this.cameraT = cameraT;
        this.interactDistance = settings.interactDistance;
        this.interactLayer = LayerMask.GetMask("Interactable"); // можешь заменить на поле в settings
        this.playerStats = playerStats;
    }

    public void SetWallet(PlayerWallet w) => wallet = w;

    public void TryInteract()
    {
        // Ограничиваем частоту raycast для оптимизации
        if (Time.time - lastRaycastTime < RAYCAST_COOLDOWN)
            return;

        lastRaycastTime = Time.time;

        // Переиспользуем объект Ray вместо создания нового
        ray.origin = cameraT.position;
        ray.direction = cameraT.forward;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            // Проверяем, не тот же ли объект (избегаем повторных взаимодействий)
            if (lastHitObject == hit.collider.gameObject)
                return;

            lastHitObject = hit.collider.gameObject;

            // Проверяем, находится ли игрок в зоне покупки
            if (IsPlayerInShopZone())
            {
                TryPurchaseItem(hit.collider.gameObject);
            }
            else
            {
                // Обычное подбирание предметов
                if (inventory.TryAdd(hit.collider.gameObject))
                {
                    hit.collider.gameObject.SetActive(false);
                    Debug.Log("✅ Подобрал " + hit.collider.gameObject.name);
                }
                else
                {
                    Debug.Log("⚠ Слот занят, не могу подобрать");
                }
            }
        }
        else
        {
            lastHitObject = null; // Сбрасываем при отсутствии попаданий
        }
    }

    public void TryDrop()
    {
        GameObject dropped = inventory.RemoveActive();
        if (dropped != null)
        {
            dropped.SetActive(true);
            dropped.transform.position = cameraT.position + cameraT.forward * 1.5f;
            if (dropped.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.linearVelocity = cameraT.forward * 3f;
            }
            Debug.Log("✅ Выбросил предмет " + dropped.name);
        }
    }

    public void TrySell()
    {
        Debug.Log("🔍 TrySell() вызван");

        if (wallet == null)
        {
            Debug.LogWarning("⚠ Нет кошелька у игрока. Добавь PlayerWallet на игрока и вызови SetWallet().");
            return;
        }

        GameObject obj = inventory.GetItem(inventory.ActiveSlot);
        if (obj == null)
        {
            Debug.Log("ℹ Нечего продавать: активный слот пуст.");
            return;
        }

        if (!obj.TryGetComponent<Item>(out var itemData))
        {
            Debug.Log("⚠ Этот объект нельзя продать: нет компонента Item.");
            return;
        }

        Debug.Log($"🔍 Найден предмет для продажи: {obj.name}, цена: {itemData.price}");

        // Ищем ближайшую зону продажи вокруг камеры
        const float searchRadius = 2.0f;
        Collider[] hits = Physics.OverlapSphere(cameraT.position, searchRadius);
        Debug.Log($"🔍 Найдено {hits.Length} коллайдеров в радиусе {searchRadius}");

        SellZone foundZone = null;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].TryGetComponent<SellZone>(out var zone))
            {
                foundZone = zone;
                Debug.Log($"🔍 Найдена зона продажи: {hits[i].name}");
                break;
            }
        }

        if (foundZone == null || !foundZone.gameObject.activeInHierarchy)
        {
            Debug.Log("ℹ Подойдите к зоне продажи, чтобы продать предмет.");
            return;
        }

        int basePrice = Mathf.Max(0, itemData.price);
        float multiplier = Mathf.Max(0f, foundZone.priceMultiplier);
        int payout = Mathf.RoundToInt(basePrice * multiplier);

        if (payout <= 0)
        {
            Debug.Log("ℹ Этот предмет ничего не стоит.");
            return;
        }

        // Убираем предмет из инвентаря, начисляем валюту и уничтожаем объект
        GameObject soldObj = inventory.RemoveActive();
        if (soldObj != null)
        {
            wallet.Add(payout);
            Object.Destroy(soldObj);
            Debug.Log($"💰 Продано: {itemData.name} за {payout}. Баланс: {wallet.Balance}");
        }
    }

    // === МЕТОДЫ ДЛЯ ПОКУПКИ ===

    private bool IsPlayerInShopZone()
    {
        const float searchRadius = 2.0f;
        Collider[] hits = Physics.OverlapSphere(cameraT.position, searchRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].TryGetComponent<ShopZone>(out var shopZone))
            {
                return shopZone.IsPlayerInside;
            }
        }
        return false;
    }

    private void TryPurchaseItem(GameObject itemObject)
    {
        Debug.Log("🛒 Попытка покупки предмета");

        if (wallet == null)
        {
            Debug.LogWarning("⚠ Нет кошелька у игрока для покупки!");
            return;
        }

        // Получаем базовый компонент Item
        if (!itemObject.TryGetComponent<Item>(out var itemData))
        {
            Debug.Log("⚠ Этот объект нельзя купить: нет компонента Item.");
            return;
        }

        // Находим зону покупки для получения цены
        ShopZone shopZone = GetNearestShopZone();
        if (shopZone == null)
        {
            Debug.Log("⚠ Не найдена зона покупки!");
            return;
        }

        int purchasePrice = shopZone.GetPurchasePrice(itemData.price);

        if (!wallet.TrySpend(purchasePrice))
        {
            Debug.Log($"💸 Недостаточно денег! Нужно: {purchasePrice}, есть: {wallet.Balance}");
            return;
        }

        // Получаем название предмета из компонента Item
        string itemName = itemData.itemName;

        // Применяем эффекты предмета в зависимости от типа
        if (itemObject.TryGetComponent<BuffItem>(out var buffItem))
        {
            buffItem.ApplyBuff(playerStats);
            Debug.Log($"✅ Куплен и применен бонус: {itemName} за {purchasePrice}");
        }
        else if (itemObject.TryGetComponent<Weapon>(out var weapon))
        {
            // Добавляем оружие в инвентарь вместо мгновенного применения
            if (inventory.TryAdd(itemObject))
            {
                itemObject.SetActive(false);
                weapon.ApplyWeaponStats(playerStats);
                Debug.Log($"✅ Куплено и экипировано оружие: {itemName} за {purchasePrice}");
            }
            else
            {
                // Возвращаем деньги если не удалось добавить в инвентарь
                wallet.Add(purchasePrice);
                Debug.Log("⚠ Не удалось добавить оружие в инвентарь, деньги возвращены");
                return; // Не уничтожаем предмет
            }
        }
        else if (itemObject.TryGetComponent<SellableItem>(out var sellableItem))
        {
            // Обычные предметы добавляем в инвентарь
            if (inventory.TryAdd(itemObject))
            {
                itemObject.SetActive(false);
                Debug.Log($"✅ Куплен предмет: {itemName} за {purchasePrice}");
            }
            else
            {
                // Возвращаем деньги если не удалось добавить в инвентарь
                wallet.Add(purchasePrice);
                Debug.Log("⚠ Не удалось добавить предмет в инвентарь, деньги возвращены");
            }
        }
        else
        {
            Debug.LogWarning("⚠ Неизвестный тип предмета для покупки!");
            wallet.Add(purchasePrice); // Возвращаем деньги
        }

        // Уничтожаем предмет после покупки (кроме оружия, которое остается в инвентаре)
        if (!itemObject.TryGetComponent<Weapon>(out _))
        {
            Object.Destroy(itemObject);
        }
    }

    private ShopZone GetNearestShopZone()
    {
        const float searchRadius = 2.0f;
        Collider[] hits = Physics.OverlapSphere(cameraT.position, searchRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].TryGetComponent<ShopZone>(out var shopZone))
            {
                return shopZone;
            }
        }
        return null;
    }
}
