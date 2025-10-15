using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private PlayerSettings settings;

    [Header("References")]
    [SerializeField] private Transform playerCameraT;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private PlayerWallet wallet;

    private CharacterController controller;
    private PlayerMovement movement;
    private PlayerCameraController cameraController;
    private InventorySystem inventory;
    private PlayerInteraction interaction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCameraT == null)
            Debug.LogError("❌ playerCameraT не назначен в инспекторе!");
        if (settings == null)
            Debug.LogWarning("⚠ PlayerSettings не назначен — будут использованы дефолтные значения!");

        // создаём подсистемы
        movement = new PlayerMovement(controller, settings);
        cameraController = new PlayerCameraController(playerCameraT, transform, settings);
        inventory = new InventorySystem(4);
        interaction = new PlayerInteraction(inventory, playerCameraT, settings);

        // привязка кошелька к взаимодействию
        if (wallet != null)
            interaction.SetWallet(wallet);
        else
        {
            // Автоматически создаём PlayerWallet если не назначен
            wallet = GetComponent<PlayerWallet>();
            if (wallet == null)
                wallet = gameObject.AddComponent<PlayerWallet>();
            interaction.SetWallet(wallet);
            Debug.Log("✅ PlayerWallet автоматически создан и привязан");
        }

        // привязка UI к инвентарю
        if (inventoryUI != null)
            inventoryUI.BindInventory(inventory);

        // курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        movement.Tick();
        cameraController.Tick();
    }

    // === INPUT SYSTEM CALLBACKS ===
    public void OnMove(InputValue value) => movement.SetMoveInput(value.Get<Vector2>());
    public void OnJump(InputValue value) => movement.TryJump(value.isPressed);
    public void OnLook(InputValue value) => cameraController.SetLookInput(value.Get<Vector2>());
    public void OnInteract() => interaction.TryInteract();
    public void OnDrop() => interaction.TryDrop();
    public void OnSell() => interaction.TrySell();
    public void OnInventory1() => inventory.SetActiveSlot(0);
    public void OnInventory2() => inventory.SetActiveSlot(1);
    public void OnInventory3() => inventory.SetActiveSlot(2);
    public void OnInventory4() => inventory.SetActiveSlot(3);

    // runtime-настройки
    public void SetMouseSensitivity(float value) => cameraController.SetSensitivity(value);
}
