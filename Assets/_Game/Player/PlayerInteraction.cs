using UnityEngine;

public class PlayerInteraction
{
    private readonly InventorySystem inventory;
    private readonly Transform cameraT;
    private readonly float interactDistance;
    private LayerMask interactLayer;

    public PlayerInteraction(InventorySystem inventory, Transform cameraT, PlayerSettings settings)
    {
        this.inventory = inventory;
        this.cameraT = cameraT;
        this.interactDistance = settings.interactDistance;
        this.interactLayer = LayerMask.GetMask("Interactable"); // можешь заменить на поле в settings
    }

    public void TryInteract()
    {
        Ray ray = new(cameraT.position, cameraT.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
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
