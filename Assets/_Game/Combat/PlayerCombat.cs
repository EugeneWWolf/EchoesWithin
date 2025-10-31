using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Система боя игрока
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 3f; // Увеличили радиус
    [SerializeField] private float attackCooldown = 0.5f; // Уменьшили перезарядку
    [SerializeField] private LayerMask enemyLayer = 1;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float attackHeight = 2f; // Высота атаки

    [Header("Attack Effects")]
    [SerializeField] private GameObject attackEffectPrefab;
    [SerializeField] private Transform attackPoint;

    private float lastAttackTime;
    private PlayerStats playerStats;
    private PlayerController playerController;

    private void Start()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            // Получаем PlayerStats через рефлексию
            var statsField = typeof(PlayerController).GetField("playerStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (statsField != null)
            {
                playerStats = statsField.GetValue(playerController) as PlayerStats;
            }
        }

        // Если нет точки атаки, используем позицию игрока
        if (attackPoint == null)
        {
            attackPoint = transform;
        }

        Debug.Log("⚔️ PlayerCombat инициализирован");
    }

    /// <summary>
    /// Попытка атаки
    /// </summary>
    public void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
        {
            if (enableDebugLogs)
            {
                Debug.Log("⏰ Атака на перезарядке");
            }
            return;
        }

        PerformAttack();
        lastAttackTime = Time.time;
    }

    /// <summary>
    /// Выполнение атаки
    /// </summary>
    private void PerformAttack()
    {
        // Получаем урон из статов
        float damage = GetPlayerDamage();

        // Используем более надежную систему обнаружения
        Vector3 attackCenter = attackPoint.position + transform.forward * (attackRange * 0.5f);
        Vector3 attackSize = new Vector3(attackRange, attackHeight, attackRange);

        // Ищем врагов в области атаки (сначала с layer mask)
        Collider[] enemies = Physics.OverlapBox(attackCenter, attackSize * 0.5f, transform.rotation, enemyLayer);

        // Если не найдено врагов с layer mask, ищем всех (fallback)
        if (enemies.Length == 0)
        {
            Collider[] allColliders = Physics.OverlapBox(attackCenter, attackSize * 0.5f, transform.rotation);
            List<Collider> enemyList = new List<Collider>();

            foreach (Collider col in allColliders)
            {
                // Проверяем, что это враг, но не игрок
                Enemy enemyComponent = col.GetComponent<Enemy>();
                if (enemyComponent != null && col.transform != transform && !col.transform.IsChildOf(transform))
                {
                    enemyList.Add(col);
                }
            }

            enemies = enemyList.ToArray();
        }

        bool hitSomething = false;

        if (enableDebugLogs)
        {
            Debug.Log($"⚔️ Атака! Найдено врагов: {enemies.Length}");
        }

        foreach (Collider enemy in enemies)
        {
            Enemy enemyComponent = enemy.GetComponent<Enemy>();
            if (enemyComponent != null && !enemyComponent.IsDead())
            {
                enemyComponent.TakeDamage(damage);
                hitSomething = true;

                if (enableDebugLogs)
                {
                    Debug.Log($"⚔️ Атакован {enemy.name} на {damage} урона");
                }
            }
        }

        // Эффект атаки
        if (hitSomething)
        {
            PlayAttackEffect();
        }
        else
        {
            if (enableDebugLogs)
            {
                Debug.Log("⚔️ Атака не попала ни в кого");
            }
        }
    }

    /// <summary>
    /// Получение урона игрока с учетом всех модификаторов
    /// </summary>
    private float GetPlayerDamage()
    {
        if (playerStats != null)
        {
            // Используем currentDamage, который уже включает все модификаторы
            float totalDamage = playerStats.currentDamage;

            if (enableDebugLogs)
            {
                Debug.Log($"⚔ Урон: базовый={playerStats.baseDamage}, модификатор={playerStats.damageModifier}, итого={totalDamage}");
            }

            return totalDamage;
        }

        // Базовый урон если нет статов
        return 10f;
    }

    /// <summary>
    /// Воспроизведение эффекта атаки
    /// </summary>
    private void PlayAttackEffect()
    {
        if (attackEffectPrefab != null)
        {
            GameObject effect = Instantiate(attackEffectPrefab, attackPoint.position, attackPoint.rotation);
            Destroy(effect, 1f);
        }
    }

    /// <summary>
    /// Проверка, можно ли атаковать
    /// </summary>
    public bool CanAttack()
    {
        return Time.time - lastAttackTime >= attackCooldown;
    }

    /// <summary>
    /// Получение времени до следующей атаки
    /// </summary>
    public float GetAttackCooldownTime()
    {
        return Mathf.Max(0, attackCooldown - (Time.time - lastAttackTime));
    }

    /// <summary>
    /// Установка урона (для тестирования)
    /// </summary>
    public void SetDamage(float damage)
    {
        if (playerStats != null)
        {
            playerStats.baseDamage = damage;
            playerStats.RecalculateStats();
        }
    }

    /// <summary>
    /// Визуализация области атаки в редакторе
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Vector3 attackCenter = attackPoint.position + transform.forward * (attackRange * 0.5f);
            Vector3 attackSize = new Vector3(attackRange, attackHeight, attackRange);
            Gizmos.DrawWireCube(attackCenter, attackSize);
        }
    }

    /// <summary>
    /// Получение информации о бое
    /// </summary>
    public string GetCombatInfo()
    {
        float damage = GetPlayerDamage();
        float cooldown = GetAttackCooldownTime();

        return $"Урон: {damage:F1} | Перезарядка: {cooldown:F1}s";
    }

    /// <summary>
    /// Принудительное обновление урона
    /// </summary>
    public void RefreshDamage()
    {
        if (enableDebugLogs)
        {
            float damage = GetPlayerDamage();
            Debug.Log($"⚔ PlayerCombat: Урон обновлен - {damage}");
        }
    }

    /// <summary>
    /// Обновление урона при применении баффов
    /// </summary>
    public void OnBuffApplied()
    {
        // Принудительно пересчитываем статы
        if (playerStats != null)
        {
            playerStats.RecalculateStats();
        }

        RefreshDamage();
    }
}
