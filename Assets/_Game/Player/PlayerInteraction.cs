using UnityEngine;

// Компонент для триггера взаимодействия с предметами
public class ItemInteractionTrigger : MonoBehaviour
{
    public GameObject item;
}

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
            Debug.Log($"🔍 Обнаружен объект: {hit.collider.gameObject.name} (активен: {hit.collider.gameObject.activeInHierarchy}, слой: {hit.collider.gameObject.layer})");

            // Проверяем, что объект находится на правильном слое для взаимодействия
            int targetLayer = LayerMask.NameToLayer("Interactable");
            Debug.Log($"🔍 Проверка слоя: объект {hit.collider.gameObject.name} на слое {hit.collider.gameObject.layer}, нужен слой {targetLayer}");

            if (targetLayer != -1 && hit.collider.gameObject.layer != targetLayer)
            {
                Debug.Log($"⚠ Объект {hit.collider.gameObject.name} не на слое Interactable (слой: {hit.collider.gameObject.layer}, нужен: {targetLayer})");
                return;
            }

            // Получаем предмет из триггера взаимодействия
            GameObject targetItem = hit.collider.gameObject;
            ItemInteractionTrigger trigger = hit.collider.GetComponent<ItemInteractionTrigger>();
            Debug.Log($"🔍 Проверка триггера: {hit.collider.gameObject.name}, есть ItemInteractionTrigger: {trigger != null}");

            if (trigger != null && trigger.item != null)
            {
                targetItem = trigger.item;
                Debug.Log($"🔍 Найден связанный предмет: {targetItem.name}");
            }
            else
            {
                Debug.Log($"🔍 Используем прямой объект: {targetItem.name}");
            }

            // Проверяем, является ли объект телепорт-дверью
            TeleportDoor teleportDoor = targetItem.GetComponent<TeleportDoor>();
            if (teleportDoor != null)
            {
                Debug.Log("🚪 Взаимодействие с дверью телепортации. Используйте зажатие кнопки для телепортации.");
                return; // Не подбираем дверь!
            }

            // Проверяем, находится ли игрок в зоне покупки
            if (IsPlayerInShopZone())
            {
                TryPurchaseItem(targetItem);
            }
            else
            {
                // Проверяем, что объект можно подобрать (не телепорт-дверь и не зона телепортации)
                if (targetItem.GetComponent<TeleportDoor>() == null &&
                    targetItem.GetComponent<TeleportZone>() == null &&
                    targetItem.activeInHierarchy) // Предмет должен быть активен
                {
                    // Проверяем, что у объекта есть компонент Item
                    Debug.Log($"🔍 Проверка компонента Item у {targetItem.name}: {targetItem.TryGetComponent<Item>(out var itemComponent)}");
                    if (!itemComponent)
                    {
                        Debug.Log($"⚠ Объект {targetItem.name} не имеет компонента Item, пропускаем");
                        return;
                    }

                    // Проверяем тип предмета перед добавлением в инвентарь
                    if (targetItem.TryGetComponent<BuffItem>(out var buffItem))
                    {
                        // BuffItem применяется и уничтожается, не добавляется в инвентарь
                        buffItem.ApplyBuff(playerStats);

                        // Обновляем статы движения после применения бонуса
                        if (playerController != null)
                        {
                            playerController.UpdateMovementStats();
                            if (playerController.combat != null)
                            {
                                playerController.combat.OnBuffApplied();
                            }
                        }

                        // Уничтожаем BuffItem после применения (он одноразовый)
                        Object.Destroy(targetItem);
                        Debug.Log($"✅ Подобран и применен бонус: {targetItem.name} (предмет уничтожен)");
                    }
                    else
                    {
                        Debug.Log($"🔍 Пытаемся добавить {targetItem.name} в инвентарь...");
                        bool added = inventory.TryAdd(targetItem);
                        Debug.Log($"🔍 Результат добавления в инвентарь: {added}");

                        if (added)
                        {
                            // Остальные предметы добавляем в инвентарь
                            if (targetItem.TryGetComponent<Weapon>(out var weapon))
                            {
                                weapon.ApplyWeaponStats(playerStats);
                                // Обновляем только урон после применения оружия
                                if (playerController != null && playerController.combat != null)
                                {
                                    playerController.combat.OnBuffApplied();
                                }
                                Debug.Log($"✅ Подобрано и экипировано оружие: {targetItem.name}");
                            }
                            else
                            {
                                Debug.Log("✅ Подобрал " + targetItem.name);
                            }

                            targetItem.SetActive(false);
                        }
                        else
                        {
                            Debug.Log($"⚠ Не могу подобрать {targetItem.name}: слот {inventory.ActiveSlot} занят предметом {inventory.GetItem(inventory.ActiveSlot)?.name ?? "null"}");
                        }
                    }
                }
                else
                {
                    Debug.Log("ℹ Этот объект нельзя подобрать (телепорт-объект)");
                }
            }
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
            // Если это оружие, убираем бонус урона
            if (dropped.TryGetComponent<Weapon>(out var weapon))
            {
                weapon.RemoveWeaponStats(playerStats);

                // Обновляем только урон после снятия оружия
                if (playerController != null && playerController.combat != null)
                {
                    playerController.combat.OnBuffApplied();
                }

                Debug.Log($"⚔ Снято оружие: {dropped.name}");
            }

            // Активируем предмет
            dropped.SetActive(true);

            // Устанавливаем позицию перед игроком
            Vector3 dropPosition = cameraT.position + cameraT.forward * 1.5f;
            dropPosition.y = Mathf.Max(dropPosition.y, cameraT.position.y - 0.5f); // Не ниже уровня игрока
            dropped.transform.position = dropPosition;

            // Настраиваем Rigidbody для физики
            Rigidbody rb = dropped.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = dropped.AddComponent<Rigidbody>();
            }

            // Включаем физику, но ограничиваем скорость
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 2f; // Сопротивление воздуха
            rb.angularDamping = 5f; // Сопротивление вращению

            // Добавляем небольшую силу вперед
            rb.AddForce(cameraT.forward * 2f, ForceMode.Impulse);

            // Убеждаемся, что у предмета есть коллайдер
            Collider collider = dropped.GetComponent<Collider>();
            if (collider == null)
            {
                collider = dropped.AddComponent<BoxCollider>();
            }

            // Устанавливаем правильный слой для взаимодействия
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                dropped.layer = interactableLayer;
            }

            // Настраиваем основной коллайдер для физики
            collider.isTrigger = false; // НЕ триггер для физики
            collider.enabled = true;

            // Проверяем, есть ли уже триггер взаимодействия
            ItemInteractionTrigger existingTrigger = dropped.GetComponentInChildren<ItemInteractionTrigger>();
            if (existingTrigger == null || existingTrigger.gameObject == dropped)
            {
                // Удаляем старые триггеры взаимодействия
                ItemInteractionTrigger[] oldTriggers = dropped.GetComponentsInChildren<ItemInteractionTrigger>();
                foreach (var oldTrigger in oldTriggers)
                {
                    if (oldTrigger.gameObject != dropped)
                    {
                        Object.Destroy(oldTrigger.gameObject);
                    }
                }

                // Добавляем дополнительный триггер-коллайдер для взаимодействия
                GameObject interactionTrigger = new GameObject("InteractionTrigger");
                interactionTrigger.transform.SetParent(dropped.transform);
                interactionTrigger.transform.localPosition = Vector3.zero;
                interactionTrigger.layer = interactableLayer;

                BoxCollider triggerCollider = interactionTrigger.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
                triggerCollider.size = collider.bounds.size * 1.2f; // Немного больше основного коллайдера

                // Добавляем компонент для идентификации
                interactionTrigger.AddComponent<ItemInteractionTrigger>().item = dropped;

                Debug.Log($"✅ Создан новый триггер взаимодействия для {dropped.name}");
            }
            else
            {
                Debug.Log($"ℹ Триггер взаимодействия уже существует для {dropped.name}");
            }

            // Убеждаемся, что у предмета есть компонент Item
            if (!dropped.TryGetComponent<Item>(out var itemComponent))
            {
                Debug.LogWarning($"⚠ У выброшенного предмета {dropped.name} нет компонента Item!");
            }

            Debug.Log($"✅ Выбросил предмет {dropped.name} в позиции {dropPosition} (слой: {dropped.layer}, коллайдер: {collider.enabled})");
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
            if (playerController != null)
            {
                playerController.UpdateMovementStats();
                if (playerController.combat != null)
                {
                    playerController.combat.OnBuffApplied();
                }
            }

            // Скрываем цену перед деактивацией предмета
            if (itemObject.TryGetComponent<ItemPriceDisplay>(out var priceDisplay1))
            {
                priceDisplay1.HidePrice();
            }

            // Уничтожаем BuffItem после применения (он одноразовый)
            itemObject.SetActive(false);
            Debug.Log($"✅ Куплен и применен бонус: {itemName} за {purchasePrice} (предмет уничтожен)");
        }
        else if (itemObject.TryGetComponent<Weapon>(out var weapon))
        {
            // Добавляем оружие в инвентарь вместо мгновенного применения
            if (inventory.TryAdd(itemObject))
            {
                // Скрываем цену перед деактивацией предмета
                if (itemObject.TryGetComponent<ItemPriceDisplay>(out var priceDisplay2))
                {
                    priceDisplay2.HidePrice();
                }

                itemObject.SetActive(false);
                weapon.ApplyWeaponStats(playerStats);

                // Обновляем только урон после применения оружия
                if (playerController != null && playerController.combat != null)
                {
                    playerController.combat.OnBuffApplied();
                }

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
                // Скрываем цену перед деактивацией предмета
                if (itemObject.TryGetComponent<ItemPriceDisplay>(out var priceDisplay3))
                {
                    priceDisplay3.HidePrice();
                }

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
