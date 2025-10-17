using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Манекен для тестирования урона
/// </summary>
public class Dummy : Enemy
{
    [Header("Dummy Settings")]
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private float respawnTime = 3f;
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private GameObject damageTextPrefab;

    [Header("Health Bar")]
    [SerializeField] private bool showHealthBar = false; // Отключаем health bar для манекена

    [Header("Dummy Stats")]
    [SerializeField] private float baseMaxHealth = 100f;
    [SerializeField] private bool isInvincible = false;

    private float respawnTimer;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Renderer[] renderers;
    private Collider dummyCollider;

    protected override void Start()
    {
        // Сохраняем оригинальную позицию и поворот
        originalPosition = transform.position;
        originalRotation = transform.rotation;

        // Получаем компоненты
        renderers = GetComponentsInChildren<Renderer>();
        dummyCollider = GetComponent<Collider>();

        // Устанавливаем базовое здоровье
        maxHealth = baseMaxHealth;

        // Отключаем health bar для манекена
        showHealthBar = false;

        base.Start();

        Debug.Log($"🎯 Dummy инициализирован. Здоровье: {maxHealth}");
    }

    protected override void Update()
    {
        base.Update();

        // Обработка респавна
        if (isDead && respawnOnDeath)
        {
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= respawnTime)
            {
                Respawn();
            }
        }
    }

    public override void TakeDamage(float damageAmount)
    {
        if (isInvincible)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"🛡️ {gameObject.name} неуязвим к урону!");
            }
            return;
        }

        base.TakeDamage(damageAmount);

        // Показываем числа урона
        if (showDamageNumbers)
        {
            ShowDamageNumber(damageAmount);
        }

        // Эффект при получении урона
        StartCoroutine(DamageEffect());
    }

    protected override void OnDeath()
    {
        // Скрываем манекен
        SetVisibility(false);

        // Отключаем коллайдер
        if (dummyCollider != null)
        {
            dummyCollider.enabled = false;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"🎯 Манекен уничтожен! Респавн через {respawnTime} секунд");
        }

        // Сбрасываем таймер респавна
        respawnTimer = 0f;
    }

    protected override void UpdateEnemy()
    {
        // Манекен не двигается и не атакует
        // Просто стоит на месте
    }

    /// <summary>
    /// Респавн манекена
    /// </summary>
    private void Respawn()
    {
        // Восстанавливаем здоровье
        currentHealth = maxHealth;
        isDead = false;

        // Восстанавливаем позицию и поворот
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        // Показываем манекен
        SetVisibility(true);

        // Включаем коллайдер
        if (dummyCollider != null)
        {
            dummyCollider.enabled = true;
        }

        // Сбрасываем таймер
        respawnTimer = 0f;

        if (enableDebugLogs)
        {
            Debug.Log($"🎯 Манекен респавнился! Здоровье: {currentHealth}/{maxHealth}");
        }
    }

    /// <summary>
    /// Управление видимостью манекена
    /// </summary>
    private void SetVisibility(bool visible)
    {
        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }

    /// <summary>
    /// Показ числа урона
    /// </summary>
    private void ShowDamageNumber(float damage)
    {
        Vector3 spawnPosition = transform.position + Vector3.up * 2f;

        if (enableDebugLogs)
        {
            Debug.Log($"💥 Создаем текст урона: {damage} в позиции {spawnPosition}");
        }

        // Используем простую систему отображения урона
        GameObject damageTextObj = SimpleDamageText.CreateDamageText(spawnPosition, damage);

        if (damageTextObj != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"✅ Текст урона создан: {damageTextObj.name}");
            }
        }
        else
        {
            Debug.LogError("❌ Не удалось создать текст урона!");
        }
    }

    /// <summary>
    /// Эффект при получении урона
    /// </summary>
    private System.Collections.IEnumerator DamageEffect()
    {
        // Мигание красным
        if (renderers != null)
        {
            Color originalColor = Color.white;
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    originalColor = renderer.material.color;
                    renderer.material.color = Color.red;
                }
            }

            yield return new WaitForSeconds(0.1f);

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = originalColor;
                }
            }
        }
    }

    /// <summary>
    /// Переключение неуязвимости
    /// </summary>
    [ContextMenu("Toggle Invincibility")]
    public void ToggleInvincibility()
    {
        isInvincible = !isInvincible;
        Debug.Log($"🛡️ Манекен {(isInvincible ? "стал неуязвимым" : "стал уязвимым")}");
    }

    /// <summary>
    /// Установка максимального здоровья
    /// </summary>
    public new void SetMaxHealth(float newMaxHealth)
    {
        base.SetMaxHealth(newMaxHealth);

        if (enableDebugLogs)
        {
            Debug.Log($"🎯 Максимальное здоровье манекена изменено на {maxHealth}");
        }
    }

    /// <summary>
    /// Получение информации о манекене
    /// </summary>
    public string GetDummyInfo()
    {
        return $"Манекен: {currentHealth:F1}/{maxHealth:F1} HP ({(GetHealthPercentage() * 100):F1}%)";
    }
}
