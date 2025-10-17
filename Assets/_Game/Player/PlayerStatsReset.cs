using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Скрипт для сброса статов игрока к нормальным значениям
/// </summary>
public class PlayerStatsReset : MonoBehaviour
{
    [Header("Reset Settings")]
    [SerializeField] private bool resetOnStart = true;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    [Header("Input System")]
    [SerializeField] private InputAction resetAction;

    [Header("Default Values")]
    [SerializeField] private float defaultSpeed = 5f;
    [SerializeField] private float defaultJumpHeight = 2f;
    [SerializeField] private float defaultGravity = -9.8f;
    [SerializeField] private float defaultDamage = 10f;
    [SerializeField] private float defaultHealth = 100f;

    private PlayerStats playerStats;

    private void Start()
    {
        playerStats = FindObjectOfType<PlayerController>()?.GetComponent<PlayerController>()?.GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            // Пытаемся найти PlayerStats через рефлексию
            var playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                var statsField = typeof(PlayerController).GetField("playerStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (statsField != null)
                {
                    playerStats = statsField.GetValue(playerController) as PlayerStats;
                }
            }
        }

        if (playerStats == null)
        {
            Debug.LogError("❌ PlayerStatsReset: PlayerStats не найден!");
            return;
        }

        // Настраиваем Input Action
        if (resetAction == null)
        {
            resetAction = new InputAction("ResetStats", InputActionType.Button, "<Keyboard>/r");
        }
        resetAction.performed += _ => ResetToDefaults();
        resetAction.Enable();

        if (resetOnStart)
        {
            ResetToDefaults();
        }
    }

    private void OnDestroy()
    {
        if (resetAction != null)
        {
            resetAction.Disable();
        }
    }

    /// <summary>
    /// Сбрасывает все статы к значениям по умолчанию
    /// </summary>
    [ContextMenu("Reset Stats to Defaults")]
    public void ResetToDefaults()
    {
        if (playerStats == null)
        {
            Debug.LogError("❌ PlayerStatsReset: PlayerStats не найден!");
            return;
        }

        Debug.Log("🔄 PlayerStatsReset: Сброс статов к значениям по умолчанию");

        // Сбрасываем базовые значения
        playerStats.baseSpeed = defaultSpeed;
        playerStats.baseJumpHeight = defaultJumpHeight;
        playerStats.baseGravity = defaultGravity;
        playerStats.baseDamage = defaultDamage;
        playerStats.baseHealth = defaultHealth;

        // Сбрасываем модификаторы
        playerStats.speedModifier = 0f;
        playerStats.jumpModifier = 0f;
        playerStats.gravityModifier = 0f;
        playerStats.damageModifier = 0f;
        playerStats.healthModifier = 0f;

        // Пересчитываем статы
        playerStats.RecalculateStats();

        Debug.Log("✅ PlayerStatsReset: Статы сброшены к значениям по умолчанию");
        Debug.Log($"📊 Speed: {playerStats.currentSpeed}, Jump: {playerStats.currentJumpHeight}, Gravity: {playerStats.currentGravity}");
    }

    /// <summary>
    /// Сбрасывает только модификаторы, оставляя базовые значения
    /// </summary>
    [ContextMenu("Reset Modifiers Only")]
    public void ResetModifiersOnly()
    {
        if (playerStats == null)
        {
            Debug.LogError("❌ PlayerStatsReset: PlayerStats не найден!");
            return;
        }

        Debug.Log("🔄 PlayerStatsReset: Сброс только модификаторов");

        // Сбрасываем только модификаторы
        playerStats.speedModifier = 0f;
        playerStats.jumpModifier = 0f;
        playerStats.gravityModifier = 0f;
        playerStats.damageModifier = 0f;
        playerStats.healthModifier = 0f;

        // Пересчитываем статы
        playerStats.RecalculateStats();

        Debug.Log("✅ PlayerStatsReset: Модификаторы сброшены");
    }

    /// <summary>
    /// Устанавливает конкретные значения статов
    /// </summary>
    public void SetStats(float speed, float jumpHeight, float gravity, float damage, float health)
    {
        if (playerStats == null)
        {
            Debug.LogError("❌ PlayerStatsReset: PlayerStats не найден!");
            return;
        }

        Debug.Log($"🔄 PlayerStatsReset: Установка статов - Speed: {speed}, Jump: {jumpHeight}, Gravity: {gravity}");

        playerStats.baseSpeed = speed;
        playerStats.baseJumpHeight = jumpHeight;
        playerStats.baseGravity = gravity;
        playerStats.baseDamage = damage;
        playerStats.baseHealth = health;

        // Сбрасываем модификаторы
        playerStats.speedModifier = 0f;
        playerStats.jumpModifier = 0f;
        playerStats.gravityModifier = 0f;
        playerStats.damageModifier = 0f;
        playerStats.healthModifier = 0f;

        // Пересчитываем статы
        playerStats.RecalculateStats();

        Debug.Log("✅ PlayerStatsReset: Статы установлены");
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 350, 300, 100));
        GUILayout.Label("=== PLAYER STATS RESET ===");

        if (GUILayout.Button($"Reset Stats ({resetKey})"))
        {
            ResetToDefaults();
        }

        if (GUILayout.Button("Reset Modifiers Only"))
        {
            ResetModifiersOnly();
        }

        if (playerStats != null)
        {
            GUILayout.Label($"Speed: {playerStats.currentSpeed:F1}");
            GUILayout.Label($"Jump: {playerStats.currentJumpHeight:F1}");
            GUILayout.Label($"Gravity: {playerStats.currentGravity:F1}");
        }

        GUILayout.EndArea();
    }
}
