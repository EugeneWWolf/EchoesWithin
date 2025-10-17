using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private PlayerSettings settings;

    [Header("References")]
    [SerializeField] private Transform playerCameraT;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private PlayerWallet wallet;
    [SerializeField] private MoneyUI moneyUI;

    [Header("Player Stats")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Combat")]
    [SerializeField] public PlayerCombat combat;

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

        // Инициализируем статы игрока
        if (playerStats == null)
        {
            playerStats = ScriptableObject.CreateInstance<PlayerStats>();
            Debug.LogWarning("⚠ PlayerStats не назначен — создан дефолтный экземпляр!");
        }
        playerStats.RecalculateStats();

        // Инициализируем боевую систему
        if (combat == null)
        {
            combat = GetComponent<PlayerCombat>();
            if (combat == null)
            {
                combat = gameObject.AddComponent<PlayerCombat>();
                Debug.Log("✅ PlayerCombat автоматически создан и добавлен");
            }
        }

        // создаём подсистемы
        movement = new PlayerMovement(controller, settings, playerStats);
        cameraController = new PlayerCameraController(playerCameraT, transform, settings);
        inventory = new InventorySystem(4);
        interaction = new PlayerInteraction(inventory, playerCameraT, settings, playerStats);

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

        // Привязываем ссылку на PlayerController к взаимодействию
        interaction.SetPlayerController(this);

        // привязка UI к инвентарю
        if (inventoryUI != null)
            inventoryUI.BindInventory(inventory);

        // привязка UI денег к кошельку
        if (moneyUI != null && wallet != null)
            moneyUI.BindWallet(wallet);
        else if (moneyUI != null)
            Debug.LogWarning("⚠ MoneyUI назначен, но PlayerWallet не найден!");

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
    public void OnInteractHold(InputValue value)
    {
        if (value.isPressed)
            interaction.StartHoldInteract();
        else
            interaction.StopHoldInteract();
    }
    public void OnResetStats(InputValue value)
    {
        if (value.isPressed)
            ResetStatsToDefaults();
    }
    public void OnAttack(InputValue value)
    {
        if (value.isPressed && combat != null)
            combat.TryAttack();
    }
    public void OnDrop() => interaction.TryDrop();
    public void OnSell() => interaction.TrySell();
    public void OnInventory1() => SetActiveInventorySlot(0);
    public void OnInventory2() => SetActiveInventorySlot(1);
    public void OnInventory3() => SetActiveInventorySlot(2);
    public void OnInventory4() => SetActiveInventorySlot(3);

    private void SetActiveInventorySlot(int slotIndex)
    {
        // Снимаем статы с предыдущего оружия
        GameObject previousItem = inventory.GetItem(inventory.ActiveSlot);
        if (previousItem != null && previousItem.TryGetComponent<Weapon>(out var previousWeapon))
        {
            previousWeapon.RemoveWeaponStats(playerStats);
            Debug.Log($"⚔ Снято оружие со слота {inventory.ActiveSlot}: {previousItem.name}");
        }

        inventory.SetActiveSlot(slotIndex);

        // Применяем статы нового оружия
        GameObject currentItem = inventory.GetItem(slotIndex);
        if (currentItem != null && currentItem.TryGetComponent<Weapon>(out var currentWeapon))
        {
            currentWeapon.ApplyWeaponStats(playerStats);
            Debug.Log($"⚔ Экипировано оружие в слот {slotIndex}: {currentItem.name}");
        }
        else
        {
            Debug.Log($"ℹ Слот {slotIndex} пуст или не содержит оружие");
        }

        // Обновляем только урон в боевой системе
        if (combat != null)
        {
            combat.RefreshDamage();
        }
    }

    // runtime-настройки
    public void SetMouseSensitivity(float value) => cameraController.SetSensitivity(value);

    // Обновление статов движения (для внешних вызовов)
    public void UpdateMovementStats()
    {
        Debug.Log($"🔄 PlayerController: Обновляем статы движения - Speed: {playerStats.currentSpeed:F1}, Jump: {playerStats.currentJumpHeight:F1}");
        movement.ForceUpdateStats();
        Debug.Log($"✅ PlayerController: Статы движения обновлены");
    }

    // Диагностика урона
    [ContextMenu("Debug Damage")]
    public void DebugDamage()
    {
        if (playerStats != null)
        {
            Debug.Log($"📊 Текущий урон: {playerStats.currentDamage} (базовый: {playerStats.baseDamage}, модификатор: {playerStats.damageModifier})");
        }

        if (combat != null)
        {
            Debug.Log($"⚔ Урон в боевой системе: {combat.GetCombatInfo()}");
        }
    }

    // Методы для сброса статов
    public void ResetStatsToDefaults()
    {
        if (playerStats == null) return;

        Debug.Log("🔄 PlayerController: Сброс статов к значениям по умолчанию");

        // Сбрасываем базовые значения
        playerStats.baseSpeed = 5f;
        playerStats.baseJumpHeight = 2f;
        playerStats.baseGravity = -9.8f;
        playerStats.baseDamage = 10f;
        playerStats.baseHealth = 100f;

        // Сбрасываем модификаторы
        playerStats.speedModifier = 0f;
        playerStats.jumpModifier = 0f;
        playerStats.gravityModifier = 0f;
        playerStats.damageModifier = 0f;
        playerStats.healthModifier = 0f;

        // Пересчитываем статы
        playerStats.RecalculateStats();

        // Обновляем статы движения
        movement.ForceUpdateStats();

        Debug.Log("✅ PlayerController: Статы сброшены к значениям по умолчанию");
    }

    public void ResetModifiersOnly()
    {
        if (playerStats == null) return;

        Debug.Log("🔄 PlayerController: Сброс только модификаторов");

        // Сбрасываем только модификаторы
        playerStats.speedModifier = 0f;
        playerStats.jumpModifier = 0f;
        playerStats.gravityModifier = 0f;
        playerStats.damageModifier = 0f;
        playerStats.healthModifier = 0f;

        // Пересчитываем статы
        playerStats.RecalculateStats();

        // Обновляем статы движения
        movement.ForceUpdateStats();

        Debug.Log("✅ PlayerController: Модификаторы статов сброшены");
    }
}
