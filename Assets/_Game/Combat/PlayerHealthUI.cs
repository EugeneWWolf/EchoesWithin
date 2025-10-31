using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI компонент для отображения здоровья игрока
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text healthText;

    [Header("Display Settings")]
    [SerializeField] private string healthFormat = "❤ {0:F0}/{1:F0}";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private Color mediumHealthColor = Color.yellow;
    [SerializeField] private float lowHealthThreshold = 0.3f; // 30% здоровья
    [SerializeField] private float mediumHealthThreshold = 0.6f; // 60% здоровья

    [Header("Fallback Update")]
    [SerializeField] private bool enableFallbackUpdate = true;
    [SerializeField] private float fallbackUpdateInterval = 0.1f;

    private PlayerController player;
    private float lastHealth = -1f;
    private float lastMaxHealth = -1f;
    private float lastFallbackCheck = 0f;

    public void BindPlayer(PlayerController playerController)
    {
        // Отписываемся от предыдущего игрока
        player = playerController;

        if (player != null)
        {
            UpdateDisplay();
            Debug.Log($"❤ PlayerHealthUI: Привязан к игроку. Здоровье: {player.GetCurrentHealth()}/{player.GetMaxHealth()}");
        }
        else
        {
            Debug.LogWarning("⚠ PlayerHealthUI: PlayerController не найден!");
        }
    }

    private void Update()
    {
        if (player == null) return;

        // Fallback update mechanism - проверяем изменения здоровья периодически
        if (enableFallbackUpdate && Time.time - lastFallbackCheck > fallbackUpdateInterval)
        {
            lastFallbackCheck = Time.time;

            float currentHealth = player.GetCurrentHealth();
            float maxHealth = player.GetMaxHealth();

            if (currentHealth != lastHealth || maxHealth != lastMaxHealth)
            {
                UpdateDisplay();
                lastHealth = currentHealth;
                lastMaxHealth = maxHealth;
            }
        }
    }

    private void UpdateDisplay()
    {
        if (player == null || healthText == null) return;

        float currentHealth = player.GetCurrentHealth();
        float maxHealth = player.GetMaxHealth();
        float healthPercentage = maxHealth > 0 ? currentHealth / maxHealth : 0f;

        // Обновляем текст
        string newText = string.Format(healthFormat, currentHealth, maxHealth);
        healthText.text = newText;

        // Обновляем сохраненные значения
        lastHealth = currentHealth;
        lastMaxHealth = maxHealth;

        // Меняем цвет в зависимости от процента здоровья
        if (healthPercentage <= lowHealthThreshold)
        {
            healthText.color = lowHealthColor;
        }
        else if (healthPercentage <= mediumHealthThreshold)
        {
            healthText.color = mediumHealthColor;
        }
        else
        {
            healthText.color = normalColor;
        }
    }

    /// <summary>
    /// Обновляет отображение вручную
    /// </summary>
    public void RefreshDisplay()
    {
        if (player != null)
        {
            UpdateDisplay();
        }
    }
}

