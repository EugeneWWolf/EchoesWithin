using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Вспомогательный скрипт для автоматического создания NavMesh в подземелье
/// Добавьте этот скрипт на объект в сцене и нажмите "Build NavMesh" в контекстном меню
/// </summary>
[ExecuteInEditMode]
public class DungeonNavMeshBuilder : MonoBehaviour
{
    [Header("Build Settings")]
    [SerializeField] private LayerMask walkableLayer = 1; // Слой для walkable поверхностей
    [SerializeField] private float agentRadius = 0.5f;
    [SerializeField] private float agentHeight = 2f;
    [SerializeField] private float maxSlope = 45f;
    [SerializeField] private float stepHeight = 0.3f;

    [Header("Area Settings")]
    [SerializeField] private Vector3 buildCenter = Vector3.zero;
    [SerializeField] private Vector3 buildSize = new Vector3(50f, 10f, 50f);
    [SerializeField] private bool useTransformAsCenter = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = Color.cyan;

    private NavMeshData navMeshData;
    private NavMeshDataInstance navMeshInstance;

    /// <summary>
    /// Построение NavMesh для подземелья
    /// </summary>
    [ContextMenu("Build NavMesh")]
    public void BuildNavMesh()
    {
        Debug.Log("🏗️ Начинаем построение NavMesh...");

        // Удаляем старый NavMesh, если есть
        if (navMeshInstance.valid)
        {
            NavMesh.RemoveNavMeshData(navMeshInstance);
        }

        // Создаем настройки построения NavMesh
        NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);
        buildSettings.agentRadius = agentRadius;
        buildSettings.agentHeight = agentHeight;
        buildSettings.agentSlope = maxSlope;
        buildSettings.agentClimb = stepHeight;

        // Определяем границы построения
        Vector3 center = useTransformAsCenter ? transform.position : buildCenter;
        Bounds bounds = new Bounds(center, buildSize);

        // Собираем источники для построения NavMesh (все объекты на walkable слое)
        List<NavMeshBuildSource> sources = new System.Collections.Generic.List<NavMeshBuildSource>();

        // Ищем все MeshFilter и TerrainCollider на walkable слое
        MeshFilter[] meshFilters = FindObjectsOfType<MeshFilter>();
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter.gameObject.layer == walkableLayer ||
                ((1 << meshFilter.gameObject.layer) & walkableLayer.value) != 0)
            {
                if (meshFilter.sharedMesh != null)
                {
                    NavMeshBuildSource source = new NavMeshBuildSource();
                    source.shape = NavMeshBuildSourceShape.Mesh;
                    source.sourceObject = meshFilter.sharedMesh;
                    source.transform = meshFilter.transform.localToWorldMatrix;
                    source.area = 0;
                    sources.Add(source);
                }
            }
        }

        // Также добавляем Terrain
        Terrain[] terrains = FindObjectsOfType<Terrain>();
        foreach (var terrain in terrains)
        {
            if (terrain.gameObject.layer == walkableLayer ||
                ((1 << terrain.gameObject.layer) & walkableLayer.value) != 0)
            {
                NavMeshBuildSource source = new NavMeshBuildSource();
                source.shape = NavMeshBuildSourceShape.Terrain;
                source.sourceObject = terrain.terrainData;
                source.transform = terrain.transform.localToWorldMatrix;
                source.area = 0;
                sources.Add(source);
            }
        }

        Debug.Log($"🏗️ Найдено {sources.Count} источников для NavMesh");

        // Строим NavMesh
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        navMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds,
            transform.position, transform.rotation);

        if (navMeshData != null)
        {
            navMeshInstance = NavMesh.AddNavMeshData(navMeshData, transform.position, transform.rotation);
            Debug.Log($"✅ NavMesh успешно построен! Покрытие: {navMeshData.sourceBounds}");
        }
        else
        {
            Debug.LogError("❌ Не удалось построить NavMesh. Проверьте настройки и наличие геометрии на walkable слое.");
        }
    }

    /// <summary>
    /// Удаление NavMesh
    /// </summary>
    [ContextMenu("Remove NavMesh")]
    public void RemoveNavMesh()
    {
        if (navMeshInstance.valid)
        {
            NavMesh.RemoveNavMeshData(navMeshInstance);
            navMeshInstance = default(NavMeshDataInstance);
            navMeshData = null;
            Debug.Log("🗑️ NavMesh удален");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 center = useTransformAsCenter ? transform.position : buildCenter;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, buildSize);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = useTransformAsCenter ? transform.position : buildCenter;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, buildSize + Vector3.one * 0.5f);
    }
}

