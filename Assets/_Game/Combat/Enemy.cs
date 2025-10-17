using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Базовый класс для всех врагов
/// </summary>
public abstract class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float currentHealth;
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 1f;

    [Header("Health Bar")]
    [SerializeField] protected bool showHealthBar = false; // Отключен по умолчанию

    [Header("Debug")]
    [SerializeField] protected bool enableDebugLogs = true;

    protected bool isDead = false;
    protected float lastAttackTime;
    protected Transform playerTransform;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
        SetupHealthBar();
        FindPlayer();
    }

    protected virtual void Update()
    {
        if (isDead) return;

        UpdateHealthBar();
        UpdateEnemy();
    }

    /// <summary>
    /// Настройка health bar (упрощенная версия)
    /// </summary>
    protected virtual void SetupHealthBar()
    {
        // Health bar отключен для производительности
        // Можно включить в настройках если нужно
    }

    /// <summary>
    /// Обновление health bar (упрощенная версия)
    /// </summary>
    protected virtual void UpdateHealthBar()
    {
        // Health bar отключен для производительности
        // Можно включить в настройках если нужно
    }

    /// <summary>
    /// Поиск игрока
    /// </summary>
    protected virtual void FindPlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    /// <summary>
    /// Получение урона
    /// </summary>
    public virtual void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0, currentHealth);

        if (enableDebugLogs)
        {
            Debug.Log($"💥 {gameObject.name} получил {damageAmount} урона. Здоровье: {currentHealth}/{maxHealth}");
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Лечение
    /// </summary>
    public virtual void Heal(float healAmount)
    {
        if (isDead) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Min(maxHealth, currentHealth);

        if (enableDebugLogs)
        {
            Debug.Log($"💚 {gameObject.name} восстановил {healAmount} здоровья. Здоровье: {currentHealth}/{maxHealth}");
        }
    }

    /// <summary>
    /// Смерть врага
    /// </summary>
    protected virtual void Die()
    {
        isDead = true;
        currentHealth = 0;

        if (enableDebugLogs)
        {
            Debug.Log($"💀 {gameObject.name} умер!");
        }

        OnDeath();
    }

    /// <summary>
    /// Переопределяемый метод смерти
    /// </summary>
    protected abstract void OnDeath();

    /// <summary>
    /// Переопределяемый метод обновления врага
    /// </summary>
    protected abstract void UpdateEnemy();

    /// <summary>
    /// Получение текущего здоровья
    /// </summary>
    public float GetCurrentHealth() => currentHealth;

    /// <summary>
    /// Получение максимального здоровья
    /// </summary>
    public float GetMaxHealth() => maxHealth;

    /// <summary>
    /// Проверка, жив ли враг
    /// </summary>
    public bool IsDead() => isDead;

    /// <summary>
    /// Получение процента здоровья
    /// </summary>
    public float GetHealthPercentage() => currentHealth / maxHealth;

    /// <summary>
    /// Восстановление здоровья до максимума
    /// </summary>
    [ContextMenu("Reset Health")]
    public virtual void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;

        if (enableDebugLogs)
        {
            Debug.Log($"💚 {gameObject.name} здоровье восстановлено до максимума!");
        }
    }

    /// <summary>
    /// Установка максимального здоровья
    /// </summary>
    public virtual void SetMaxHealth(float newMaxHealth)
    {
        maxHealth = newMaxHealth;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"💚 {gameObject.name} максимальное здоровье изменено на {maxHealth}");
        }
    }
}
