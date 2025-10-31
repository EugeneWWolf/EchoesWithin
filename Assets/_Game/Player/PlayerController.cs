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

    [Header("UI")]
    [SerializeField] private PlayerHealthUI healthUI;

    [Header("Respawn Settings")]
    [SerializeField] private Vector3 respawnPosition = Vector3.zero;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private bool useInitialPositionAsRespawn = true;

    [Header("Damage Visual Effects")]
    [SerializeField] private bool enableDamageEffect = true;
    [SerializeField] private float damageEffectDuration = 0.2f;
    [SerializeField] private Color damageEffectColor = new Color(1f, 0f, 0f, 0.3f); // Красный полупрозрачный

    private CharacterController controller;
    private float currentHealth;
    private bool isDead = false;
    private Vector3 initialPosition;
    private Camera playerCamera;
    private Renderer[] playerRenderers;
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

        // Инициализируем здоровье
        currentHealth = playerStats.currentHealth;
        initialPosition = transform.position;

        // Устанавливаем позицию респавна
        if (useInitialPositionAsRespawn && respawnPosition == Vector3.zero)
        {
            respawnPosition = initialPosition;
        }

        // Подписываемся на обновление статов для синхронизации здоровья
        // (здоровье обновляется через текущий метод, так что просто инициализируем)

        Debug.Log($"❤️ Игрок инициализирован. Здоровье: {currentHealth}/{playerStats.currentHealth}");

        // Получаем камеру для эффекта урона
        if (playerCameraT != null)
        {
            playerCamera = playerCameraT.GetComponent<Camera>();
            if (playerCamera == null)
            {
                playerCamera = playerCameraT.GetComponentInChildren<Camera>();
            }
        }

        // Получаем рендереры для визуального эффекта урона
        playerRenderers = GetComponentsInChildren<Renderer>();

        // Привязываем Health UI
        if (healthUI != null)
        {
            healthUI.BindPlayer(this);
        }
        else
        {
            // Автоматически ищем Health UI в сцене
            healthUI = FindObjectOfType<PlayerHealthUI>();
            if (healthUI != null)
            {
                healthUI.BindPlayer(this);
                Debug.Log("✅ PlayerHealthUI автоматически найден и привязан");
            }
        }

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
        // Не обновляем движение если игрок мертв
        if (isDead) return;

        movement.Tick();
        cameraController.Tick();

        // Синхронизируем максимальное здоровье со статами (но не уменьшаем текущее здоровье)
        if (playerStats != null && currentHealth > playerStats.currentHealth)
        {
            currentHealth = playerStats.currentHealth;
        }
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

    /// <summary>
    /// Получение урона
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"💥 Игрок получил {damageAmount} урона. Здоровье: {currentHealth}/{playerStats.currentHealth}");

        // Визуальный эффект урона
        if (enableDamageEffect)
        {
            StartCoroutine(DamageVisualEffect());
        }

        // Обновляем UI здоровья
        if (healthUI != null)
        {
            healthUI.RefreshDisplay();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Лечение
    /// </summary>
    public void Heal(float healAmount)
    {
        if (isDead) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Min(playerStats.currentHealth, currentHealth);

        Debug.Log($"💚 Игрок восстановил {healAmount} здоровья. Здоровье: {currentHealth}/{playerStats.currentHealth}");

        // Обновляем UI здоровья
        if (healthUI != null)
        {
            healthUI.RefreshDisplay();
        }
    }

    /// <summary>
    /// Смерть игрока
    /// </summary>
    private void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0;

        Debug.Log("💀 Игрок умер! Game Over!");

        // Запускаем респавн через некоторое время
        StartCoroutine(RespawnCoroutine());
    }

    /// <summary>
    /// Корутина респавна
    /// </summary>
    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        Respawn();
    }

    /// <summary>
    /// Респавн игрока
    /// </summary>
    private void Respawn()
    {
        isDead = false;

        // Восстанавливаем здоровье
        currentHealth = playerStats.currentHealth;

        // Телепортируем игрока на позицию респавна
        controller.enabled = false; // Отключаем контроллер для телепортации
        transform.position = respawnPosition;
        controller.enabled = true; // Включаем обратно

        Debug.Log($"🔄 Игрок респавнился на позиции {respawnPosition}. Здоровье восстановлено!");
    }

    /// <summary>
    /// Установка позиции респавна
    /// </summary>
    public void SetRespawnPosition(Vector3 position)
    {
        respawnPosition = position;
        Debug.Log($"📍 Позиция респавна установлена: {position}");
    }

    /// <summary>
    /// Получение текущего здоровья
    /// </summary>
    public float GetCurrentHealth() => currentHealth;

    /// <summary>
    /// Получение максимального здоровья
    /// </summary>
    public float GetMaxHealth() => playerStats != null ? playerStats.currentHealth : 100f;

    /// <summary>
    /// Проверка, жив ли игрок
    /// </summary>
    public bool IsDead() => isDead;

    /// <summary>
    /// Получение процента здоровья
    /// </summary>
    public float GetHealthPercentage() => GetMaxHealth() > 0 ? currentHealth / GetMaxHealth() : 0f;

    /// <summary>
    /// Визуальный эффект при получении урона
    /// </summary>
    private IEnumerator DamageVisualEffect()
    {
        // Метод 1: Красное мигание рендереров (если есть)
        if (playerRenderers != null && playerRenderers.Length > 0)
        {
            Color[] originalColors = new Color[playerRenderers.Length];

            // Сохраняем оригинальные цвета и устанавливаем красный
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null && playerRenderers[i].material != null)
                {
                    originalColors[i] = playerRenderers[i].material.color;
                    playerRenderers[i].material.color = Color.Lerp(originalColors[i], Color.red, 0.5f);
                }
            }

            yield return new WaitForSeconds(damageEffectDuration);

            // Восстанавливаем оригинальные цвета
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null && playerRenderers[i].material != null)
                {
                    playerRenderers[i].material.color = originalColors[i];
                }
            }
        }

        // Метод 2: Эффект экрана (красный оттенок) через изменение цвета камеры
        // Это можно улучшить, добавив специальный материал для эффекта урона
        // Пока используем простой метод с рендерерами
    }
}
