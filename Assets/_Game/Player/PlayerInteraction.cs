using UnityEngine;

public class PlayerInteraction
{
    private readonly InventorySystem inventory;
    private readonly Transform cameraT;
    private readonly float interactDistance;
    private LayerMask interactLayer;

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
}
