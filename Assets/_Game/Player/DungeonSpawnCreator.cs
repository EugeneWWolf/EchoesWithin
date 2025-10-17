using UnityEngine;

public class DungeonSpawnCreator : MonoBehaviour
{
    [Header("Dungeon Settings")]
    [SerializeField] private float dungeonDepth = -10f;
    [SerializeField] private Vector3 dungeonOffset = Vector3.zero;
    [SerializeField] private bool createOnStart = true;

    [Header("Visual Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.red;

    private void Start()
    {
        if (createOnStart)
        {
            CreateDungeonSpawnPoint();
        }
    }

    [ContextMenu("Create Dungeon Spawn Point")]
    public void CreateDungeonSpawnPoint()
    {
        Debug.Log("🏗️ Создание точки спавна в данже...");

        // Создаем объект для точки спавна
        GameObject dungeonSpawn = new GameObject("DungeonSpawnPoint");

        // Позиционируем под землей
        Vector3 spawnPosition = transform.position + dungeonOffset;
        spawnPosition.y = dungeonDepth;
        dungeonSpawn.transform.position = spawnPosition;

        // Добавляем визуальный индикатор
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = "DungeonIndicator";
        indicator.transform.SetParent(dungeonSpawn.transform);
        indicator.transform.localPosition = Vector3.zero;
        indicator.transform.localScale = new Vector3(2f, 0.1f, 2f);

        // Настраиваем материал индикатора
        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        Material indicatorMaterial = new Material(Shader.Find("Standard"));
        indicatorMaterial.color = gizmoColor;
        indicatorMaterial.SetFloat("_Metallic", 0f);
        indicatorMaterial.SetFloat("_Smoothness", 0.5f);
        indicatorRenderer.material = indicatorMaterial;

        // Убираем коллайдер у индикатора
        DestroyImmediate(indicator.GetComponent<Collider>());

        // Находим TeleportDoor и назначаем точку спавна
        TeleportDoor teleportDoor = FindObjectOfType<TeleportDoor>();
        if (teleportDoor != null)
        {
            // Используем рефлексию для установки точки спавна
            var field = typeof(TeleportDoor).GetField("dungeonSpawnPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(teleportDoor, dungeonSpawn.transform);
                Debug.Log($"✅ Точка спавна в данже создана и назначена: {dungeonSpawn.transform.position}");
            }
        }
        else
        {
            Debug.LogError("❌ TeleportDoor не найден! Создайте объект с компонентом TeleportDoor.");
        }

        Debug.Log($"🏗️ Точка спавна в данже создана в позиции: {spawnPosition}");
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = gizmoColor;
        Vector3 spawnPosition = transform.position + dungeonOffset;
        spawnPosition.y = dungeonDepth;

        // Рисуем точку спавна
        Gizmos.DrawWireSphere(spawnPosition, 1f);
        Gizmos.DrawIcon(spawnPosition, "sv_icon_dot3_pix16_gizmo");

        // Рисуем линию от двери к данжу
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, spawnPosition);

        // Рисуем плоскость земли
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(10, 0.1f, 10));
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 170, 300, 100));
        GUILayout.Label("=== DUNGEON SPAWN CREATOR ===");

        if (GUILayout.Button("Create Dungeon Spawn Point"))
        {
            CreateDungeonSpawnPoint();
        }

        GUILayout.Label($"Dungeon Depth: {dungeonDepth}");
        GUILayout.Label($"Spawn Position: {transform.position + dungeonOffset + Vector3.up * dungeonDepth}");

        GUILayout.EndArea();
    }
}
