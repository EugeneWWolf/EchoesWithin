using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Генератор нодов для спавна предметов в данже
/// Автоматически размещает ноды на walkable поверхностях
/// </summary>
[ExecuteInEditMode]
public class DungeonNodeGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private float gridSize = 2f; // Размер сетки для размещения нодов
    [SerializeField] private float nodeSpacing = 2f; // Расстояние между нодами
    [SerializeField] private bool generateOnStart = false;

    [Header("Walkable Detection")]
    [SerializeField] private LayerMask walkableLayer = 1; // Слой для walkable поверхностей
    [SerializeField] private float groundCheckDistance = 5f; // Расстояние для проверки земли
    [SerializeField] private float maxSlopeAngle = 45f; // Максимальный угол наклона для walkable
    [SerializeField] private float minWalkableHeight = 0.1f; // Минимальная высота свободного пространства над нодом
    [SerializeField] private float maxWalkableHeight = 3f; // Максимальная высота для проверки препятствий

    [Header("Area Settings")]
    [SerializeField] private Vector3 generationCenter = Vector3.zero; // Центр области генерации
    [SerializeField] private Vector3 generationSize = new Vector3(20f, 10f, 20f); // Размер области генерации
    [SerializeField] private bool useTransformAsCenter = true; // Использовать позицию трансформа как центр

    [Header("Advanced")]
    [SerializeField] private float nodeOffsetHeight = 0.5f; // Высота размещения нода над землей
    [SerializeField] private int maxIterations = 100; // Максимальное количество попыток найти точку
    [SerializeField] private float minDistanceFromWalls = 0.5f; // Минимальное расстояние от стен

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = Color.green;
    [SerializeField] private Color invalidGizmoColor = Color.red;

    private List<DungeonSpawnNode> generatedNodes = new List<DungeonSpawnNode>();
    private GameObject nodesParent;

    [ContextMenu("Generate Nodes")]
    public void GenerateNodes()
    {
        Debug.Log("🏗️ DungeonNodeGenerator: Начинаем генерацию нодов...");

        // Очищаем старые ноды
        ClearAllNodes();

        // Создаем родительский объект для нодов
        if (nodesParent == null)
        {
            nodesParent = new GameObject("DungeonSpawnNodes");
            nodesParent.transform.SetParent(transform);
            nodesParent.transform.localPosition = Vector3.zero;
        }

        // Определяем центр генерации
        Vector3 center = useTransformAsCenter ? transform.position : generationCenter;
        Vector3 size = generationSize;

        // Вычисляем количество нодов по осям
        int nodesX = Mathf.CeilToInt(size.x / nodeSpacing);
        int nodesZ = Mathf.CeilToInt(size.z / nodeSpacing);

        int validNodesCount = 0;
        int totalAttempts = 0;

        // Генерируем ноды в сетке
        for (int x = 0; x < nodesX; x++)
        {
            for (int z = 0; z < nodesZ; z++)
            {
                Vector3 gridPosition = center + new Vector3(
                    (x - nodesX / 2f) * nodeSpacing,
                    0,
                    (z - nodesZ / 2f) * nodeSpacing
                );

                // Пытаемся найти валидную позицию
                Vector3 validPosition = FindValidPosition(gridPosition);

                if (validPosition != Vector3.zero)
                {
                    CreateNode(validPosition);
                    validNodesCount++;
                }

                totalAttempts++;
            }
        }

        Debug.Log($"🏗️ DungeonNodeGenerator: Генерация завершена. Создано {validNodesCount} валидных нодов из {totalAttempts} попыток.");
    }

    private Vector3 FindValidPosition(Vector3 startPosition)
    {
        // Проверяем стартовую позицию
        if (IsValidWalkablePosition(startPosition))
        {
            return startPosition + Vector3.up * nodeOffsetHeight;
        }

        // Если стартовая позиция невалидна, ищем в радиусе
        float searchRadius = nodeSpacing * 0.5f;
        int attempts = 0;

        while (attempts < maxIterations)
        {
            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            Vector3 testPosition = startPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (IsValidWalkablePosition(testPosition))
            {
                return testPosition + Vector3.up * nodeOffsetHeight;
            }

            attempts++;
        }

        return Vector3.zero; // Не найдена валидная позиция
    }

    private bool IsValidWalkablePosition(Vector3 position)
    {
        // Проверяем, есть ли земля под позицией
        RaycastHit groundHit;
        bool hasGround = Physics.Raycast(
            position + Vector3.up * groundCheckDistance,
            Vector3.down,
            out groundHit,
            groundCheckDistance * 2f,
            walkableLayer
        );

        if (!hasGround)
        {
            return false;
        }

        // Проверяем угол наклона
        float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        if (slopeAngle > maxSlopeAngle)
        {
            return false;
        }

        // Проверяем, нет ли препятствий над землей (стены, потолок)
        float checkHeight = minWalkableHeight;
        Vector3 checkPosition = groundHit.point + Vector3.up * checkHeight;

        // Проверяем свободное пространство
        Collider[] obstacles = Physics.OverlapSphere(checkPosition, minDistanceFromWalls);

        // Игнорируем сам коллайдер земли
        foreach (var obstacle in obstacles)
        {
            if (obstacle != groundHit.collider)
            {
                // Проверяем, не слишком ли близко к препятствию
                float distance = Vector3.Distance(checkPosition, obstacle.bounds.center);
                if (distance < minDistanceFromWalls)
                {
                    return false;
                }
            }
        }

        // Проверяем, нет ли препятствий сверху
        RaycastHit ceilingHit;
        if (Physics.Raycast(checkPosition, Vector3.up, out ceilingHit, maxWalkableHeight))
        {
            // Если препятствие слишком близко сверху
            if (ceilingHit.distance < minWalkableHeight)
            {
                return false;
            }
        }

        return true;
    }

    private void CreateNode(Vector3 position)
    {
        GameObject nodeObject = new GameObject($"SpawnNode_{generatedNodes.Count}");
        nodeObject.transform.SetParent(nodesParent.transform);
        nodeObject.transform.position = position;

        DungeonSpawnNode node = nodeObject.AddComponent<DungeonSpawnNode>();
        generatedNodes.Add(node);
    }

    [ContextMenu("Clear All Nodes")]
    public void ClearAllNodes()
    {
        Debug.Log($"🏗️ DungeonNodeGenerator: Очистка всех нодов. Количество: {generatedNodes.Count}");

        if (nodesParent != null)
        {
            if (Application.isPlaying)
            {
                Destroy(nodesParent);
            }
            else
            {
                DestroyImmediate(nodesParent);
            }
            nodesParent = null;
        }

        generatedNodes.Clear();
        Debug.Log("🏗️ DungeonNodeGenerator: Все ноды очищены");
    }

    [ContextMenu("Get All Nodes")]
    public List<DungeonSpawnNode> GetAllNodes()
    {
        // Обновляем список из сцены
        if (nodesParent != null)
        {
            generatedNodes.Clear();
            DungeonSpawnNode[] nodes = nodesParent.GetComponentsInChildren<DungeonSpawnNode>();
            generatedNodes.AddRange(nodes);
        }

        return new List<DungeonSpawnNode>(generatedNodes);
    }

    private void Start()
    {
        if (generateOnStart && Application.isPlaying)
        {
            GenerateNodes();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 center = useTransformAsCenter ? transform.position : generationCenter;
        Vector3 size = generationSize;

        // Рисуем область генерации
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, size);

        // Рисуем сетку
        int nodesX = Mathf.CeilToInt(size.x / nodeSpacing);
        int nodesZ = Mathf.CeilToInt(size.z / nodeSpacing);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
        for (int x = 0; x <= nodesX; x++)
        {
            Vector3 start = center + new Vector3((x - nodesX / 2f) * nodeSpacing, 0, -size.z / 2f);
            Vector3 end = center + new Vector3((x - nodesX / 2f) * nodeSpacing, 0, size.z / 2f);
            Gizmos.DrawLine(start, end);
        }

        for (int z = 0; z <= nodesZ; z++)
        {
            Vector3 start = center + new Vector3(-size.x / 2f, 0, (z - nodesZ / 2f) * nodeSpacing);
            Vector3 end = center + new Vector3(size.x / 2f, 0, (z - nodesZ / 2f) * nodeSpacing);
            Gizmos.DrawLine(start, end);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = useTransformAsCenter ? transform.position : generationCenter;
        Vector3 size = generationSize;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size + Vector3.one * 0.5f);
    }
}

