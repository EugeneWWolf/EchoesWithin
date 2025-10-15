using UnityEngine;

public class PlayerInteraction
{
    private readonly InventorySystem inventory;
    private readonly Transform cameraT;
    private readonly float interactDistance;
    private LayerMask interactLayer;
    private PlayerWallet wallet;

    // Кэшированные объекты для оптимизации
    private Ray ray;
    private RaycastHit hit;
    private GameObject lastHitObject;
    private float lastRaycastTime;
    private const float RAYCAST_COOLDOWN = 0.1f; // Ограничиваем частоту raycast

    public PlayerInteraction(InventorySystem inventory, Transform cameraT, PlayerSettings settings)
    {
        this.inventory = inventory;
        this.cameraT = cameraT;
        this.interactDistance = settings.interactDistance;
        this.interactLayer = LayerMask.GetMask("Interactable"); // можешь заменить на поле в settings
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
}
