using UnityEngine;

public class PlayerInteraction
{
    private readonly InventorySystem inventory;
    private readonly Transform cameraT;
    private readonly float interactDistance;
    private LayerMask interactLayer;
    private PlayerWallet wallet;
    private PlayerStats playerStats;
    private PlayerController playerController;

    // Кэшированные объекты для оптимизации
    private Ray ray;
    private RaycastHit hit;
    private GameObject lastHitObject;
    private float lastRaycastTime;
    private const float RAYCAST_COOLDOWN = 0.1f; // Ограничиваем частоту raycast

    // Переменные для длительного зажатия
    private bool isHoldingInteract = false;
    private TeleportDoor currentTeleportDoor;
    private TeleportZone currentTeleportZone;

    public PlayerInteraction(InventorySystem inventory, Transform cameraT, PlayerSettings settings, PlayerStats playerStats)
    {
        this.inventory = inventory;
        this.cameraT = cameraT;
        this.interactDistance = settings.interactDistance;
        this.interactLayer = LayerMask.GetMask("Interactable"); // можешь заменить на поле в settings
        this.playerStats = playerStats;
    }

    public void SetWallet(PlayerWallet w) => wallet = w;

    public void SetPlayerController(PlayerController controller) => playerController = controller;

    public void ResetHoldState()
    {
        isHoldingInteract = false;
        currentTeleportDoor = null;
        currentTeleportZone = null;
        Debug.Log("🔄 Состояние зажатия кнопки сброшено");
    }

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

            // Проверяем, является ли объект телепорт-дверью
            TeleportDoor teleportDoor = hit.collider.GetComponent<TeleportDoor>();
            if (teleportDoor != null)
            {
                Debug.Log("🚪 Взаимодействие с дверью телепортации. Используйте зажатие кнопки для телепортации.");
                return; // Не подбираем дверь!
            }

            // Проверяем, находится ли игрок в зоне покупки
            if (IsPlayerInShopZone())
            {
                TryPurchaseItem(hit.collider.gameObject);
            }
            else
            {
                // Проверяем, что объект можно подобрать (не телепорт-дверь и не зона телепортации)
                if (hit.collider.GetComponent<TeleportDoor>() == null &&
                    hit.collider.GetComponent<TeleportZone>() == null)
                {
                    // Обычное подбирание предметов
                    if (inventory.TryAdd(hit.collider.gameObject))
                    {
                        // Применяем эффекты предмета если это BuffItem или Weapon
                        if (hit.collider.gameObject.TryGetComponent<BuffItem>(out var buffItem))
                        {
                            buffItem.ApplyBuff(playerStats);
                            playerController?.UpdateMovementStats();
                            Debug.Log($"✅ Подобран и применен бонус: {hit.collider.gameObject.name}");
                        }
                        else if (hit.collider.gameObject.TryGetComponent<Weapon>(out var weapon))
                        {
                            weapon.ApplyWeaponStats(playerStats);
                            playerController?.UpdateMovementStats();
                            Debug.Log($"✅ Подобрано и экипировано оружие: {hit.collider.gameObject.name}");
                        }
                        else
                        {
                            Debug.Log("✅ Подобрал " + hit.collider.gameObject.name);
                        }

                        hit.collider.gameObject.SetActive(false);
                    }
                    else
                    {
                        Debug.Log("⚠ Слот занят, не могу подобрать");
                    }
                }
                else
                {
                    Debug.Log("ℹ Этот объект нельзя подобрать (телепорт-объект)");
                }
            }
        }
        else
        {
            lastHitObject = null; // Сбрасываем при отсутствии попаданий
        }
    }

    public void StartHoldInteract()
    {
        if (isHoldingInteract)
        {
            Debug.Log("🔄 Уже держим кнопку взаимодействия");
            return;
        }

        Debug.Log("🔄 Начало зажатия кнопки взаимодействия...");

        // Сначала проверяем, есть ли обычные предметы для взаимодействия
        ray.origin = cameraT.position;
        ray.direction = cameraT.forward;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            Debug.Log($"🔍 Raycast попал в объект: {hit.collider.name}");

            // Проверяем, является ли объект телепорт-дверью
            TeleportDoor teleportDoor = hit.collider.GetComponent<TeleportDoor>();
            if (teleportDoor != null)
            {
                Debug.Log("🚪 Найдена телепорт-дверь! Начинаем зажатие...");
                currentTeleportDoor = teleportDoor;
                currentTeleportDoor.StartHold();
                isHoldingInteract = true;
                return;
            }

            // Проверяем, является ли объект обычным предметом для подбора
            if (hit.collider.GetComponent<TeleportDoor>() == null &&
                hit.collider.GetComponent<TeleportZone>() == null)
            {
                Debug.Log("ℹ Объект не является телепорт-объектом, пропускаем зажатие");
                return;
            }
        }

        // Проверяем, находимся ли мы в зоне телепортации (только если не смотрим на обычные предметы)
        TeleportZone teleportZone = GetNearbyTeleportZone();
        if (teleportZone != null)
        {
            Debug.Log("🔄 Найдена зона телепортации! Начинаем зажатие...");
            currentTeleportZone = teleportZone;
            currentTeleportZone.StartHold();
            isHoldingInteract = true;
        }
        else
        {
            Debug.Log("ℹ Зона телепортации не найдена");
        }
    }

    public void StopHoldInteract()
    {
        if (!isHoldingInteract)
        {
            Debug.Log("🔄 Не держим кнопку взаимодействия");
            return;
        }

        Debug.Log("🔄 Остановка зажатия кнопки взаимодействия...");

        if (currentTeleportDoor != null)
        {
            Debug.Log("🚪 Остановка зажатия двери");
            currentTeleportDoor.StopHold();
            currentTeleportDoor = null;
        }

        if (currentTeleportZone != null)
        {
            Debug.Log("🔄 Остановка зажатия зоны телепортации");
            currentTeleportZone.StopHold();
            currentTeleportZone = null;
        }

        isHoldingInteract = false;
    }

    private TeleportZone GetNearbyTeleportZone()
    {
        const float searchRadius = 2.0f;
        Collider[] hits = Physics.OverlapSphere(cameraT.position, searchRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].TryGetComponent<TeleportZone>(out var teleportZone))
            {
                return teleportZone;
            }
        }
        return null;
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

            // Обновляем статы движения после применения бонуса
            playerController?.UpdateMovementStats();

            Debug.Log($"✅ Куплен и применен бонус: {itemName} за {purchasePrice}");
        }
        else if (itemObject.TryGetComponent<Weapon>(out var weapon))
        {
            // Добавляем оружие в инвентарь вместо мгновенного применения
            if (inventory.TryAdd(itemObject))
            {
                itemObject.SetActive(false);
                weapon.ApplyWeaponStats(playerStats);

                // Обновляем статы движения после применения оружия
                playerController?.UpdateMovementStats();

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
