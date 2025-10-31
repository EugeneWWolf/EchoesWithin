using UnityEngine;

/// <summary>
/// Компонент-маркер для точки спавна предметов в данже
/// Используется для обозначения допустимых мест для размещения предметов
/// </summary>
[ExecuteInEditMode]
public class DungeonSpawnNode : MonoBehaviour
{
    [Header("Node Settings")]
    [SerializeField] private bool isActive = true; // Активен ли этот нод для спавна
    [SerializeField] private float spawnRadius = 0.5f; // Радиус спавна вокруг нода (для множественных предметов)

    [Header("Visual Settings")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float gizmoSize = 0.3f;

    /// <summary>
    /// Проверяет, активен ли этот нод
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Получает позицию спавна с небольшим случайным смещением в пределах радиуса
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        if (spawnRadius > 0f)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            return transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        }
        return transform.position;
    }

    /// <summary>
    /// Получает точную позицию нода
    /// </summary>
    public Vector3 GetExactPosition()
    {
        return transform.position;
    }

    /// <summary>
    /// Активирует нод
    /// </summary>
    public void Activate()
    {
        isActive = true;
    }

    /// <summary>
    /// Деактивирует нод
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
    }

    /// <summary>
    /// Устанавливает активность нода
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = isActive ? gizmoColor : Color.gray;

        // Рисуем сферу на позиции нода
        Gizmos.DrawWireSphere(transform.position, gizmoSize);

        // Рисуем линию вверх для лучшей видимости
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * (gizmoSize * 2));

        // Если есть радиус спавна, рисуем круг
        if (spawnRadius > 0f)
        {
            Gizmos.color = isActive ? new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, gizmoSize * 1.5f);
    }
}

