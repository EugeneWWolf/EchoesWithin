using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Процедурная сетка комнат из одного префаба.
/// Сосед на сетке (gx±1, gz) и (gx, gz±1) совмещается с миром: +X и +Z соответственно.
/// </summary>
[DefaultExecutionOrder(-400)]
public class ProceduralDungeonGenerator : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedDungeonRooms";

    [Header("Prefab")]
    [Tooltip("Корень комнаты: дочерний объект Entrances с парами «Up/Down/Left/Right» + « Wall» / « Door»")]
    [SerializeField] private GameObject roomPrefab;

    [Header("Layout")]
    [SerializeField] [Min(1)] private int roomCount = 12;
    [Tooltip("Расстояние между пивотами соседних комнат при масштабе префаба 1× (как в ассете). Должно совпадать с реальным шагом дверей — иначе будут щели или наслоение.")]
    [SerializeField] private Vector2 cellSize = new Vector2(11f, 11f);
    [Tooltip("Уменьши комнаты без щелей: тот же множитель применяется к localScale экземпляра и к шагу сетки. Не трогай только cellSize — иначе пивоты разъедутся с геометрией.")]
    [SerializeField] [Min(0.01f)] private float roomInstanceUniformScale = 1f;
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool randomizeSeedOnPlay;

    [Header("Prefab orientation (если проёмы не совпадают с сеткой)")]
    [Tooltip("Поменять местами Up и Down в префабе относительно соседа по +Z / −Z")]
    [SerializeField] private bool flipPrefabUpDown;
    [Tooltip("Поменять местами Left и Right в префабе относительно соседа по −X / +X")]
    [SerializeField] private bool flipPrefabLeftRight;

    [Header("Точка входа игрока (после Generate)")]
    [Tooltip("Если в префабе комнаты есть дочерний объект с таким именем — берётся его мир-позиция и поворот (например пустышка «DungeonPlayerSpawn»)")]
    [SerializeField] private string playerSpawnChildName = "DungeonPlayerSpawn";
    [Tooltip("Иначе: локальная точка в стартовой комнате (0,0), по умолчанию ближе к центру пола Room.prefab")]
    [SerializeField] private Vector3 playerSpawnLocalInRoom = new Vector3(3f, 0.5f, -8.5f);

    [Header("Коллизии")]
    [Tooltip("У префабов StylizedHandPaintedDungeon часто нет коллайдеров — добавляем MeshCollider к каждому MeshFilter")]
    [SerializeField] private bool addMeshCollidersToRooms = true;
    [Tooltip("Назначить всей сгенерированной геометрии слой (создай слой в Edit → Project Settings → Tags and Layers, например «Dungeon»)")]
    [SerializeField] private bool assignDungeonCollisionLayer = true;
    [SerializeField] private string dungeonCollisionLayerName = "Dungeon";

    [Header("Lifecycle")]
    [SerializeField] private bool generateOnAwake = true;

    private Transform generatedRoot;
    private Transform dungeonEnterSpawn;
    private readonly HashSet<Vector2Int> placedCells = new HashSet<Vector2Int>();

    public IReadOnlyCollection<Vector2Int> PlacedCells => placedCells;
    public Transform GeneratedRoot => generatedRoot;

    /// <summary>Мир-точка телепорта в стартовую комнату (клетка 0,0). Создаётся при первой генерации.</summary>
    public Transform DungeonEnterSpawn => dungeonEnterSpawn;

    /// <summary>Слой, назначенный сгенерированным комнатам (для временного excludeLayers при телепорте). −1 если не используется.</summary>
    public static int DungeonCollisionLayerIndex { get; private set; } = -1;

    private void Awake()
    {
        if (generateOnAwake)
            Generate();
    }

    [ContextMenu("Clear Generated Rooms")]
    public void ClearGeneratedRooms()
    {
        EnsureGeneratedRoot();
        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            Destroy(generatedRoot.GetChild(i).gameObject);

        placedCells.Clear();
        DungeonCollisionLayerIndex = -1;
    }

    [ContextMenu("Regenerate Dungeon")]
    public void RegenerateDungeon()
    {
        ClearGeneratedRooms();
        Generate();
    }

    public void Generate()
    {
        if (roomPrefab == null)
        {
            Debug.LogError("ProceduralDungeonGenerator: не назначен roomPrefab.");
            return;
        }

        EnsureGeneratedRoot();
        ClearGeneratedRooms();

        int seed = randomizeSeedOnPlay ? Random.Range(int.MinValue, int.MaxValue) : randomSeed;
        Random.InitState(seed);

        BuildCells(roomCount, placedCells);

        RefreshDungeonCollisionLayerIndex();

        float s = roomInstanceUniformScale;
        Vector2 step = new Vector2(cellSize.x * s, cellSize.y * s);
        Vector3 prefabScale = roomPrefab.transform.localScale;

        GameObject startRoomInstance = null;

        foreach (Vector2Int cell in placedCells)
        {
            Vector3 worldPos = transform.position + new Vector3(cell.x * step.x, 0f, cell.y * step.y);
            GameObject instance = Instantiate(roomPrefab, worldPos, Quaternion.identity, generatedRoot);
            instance.transform.localScale = prefabScale * s;

            if (cell == Vector2Int.zero)
                startRoomInstance = instance;

            bool neighborPlusZ = placedCells.Contains(cell + new Vector2Int(0, 1));
            bool neighborMinusZ = placedCells.Contains(cell + new Vector2Int(0, -1));
            bool neighborPlusX = placedCells.Contains(cell + new Vector2Int(1, 0));
            bool neighborMinusX = placedCells.Contains(cell + new Vector2Int(-1, 0));

            if (flipPrefabUpDown)
                (neighborPlusZ, neighborMinusZ) = (neighborMinusZ, neighborPlusZ);
            if (flipPrefabLeftRight)
                (neighborPlusX, neighborMinusX) = (neighborMinusX, neighborPlusX);

            DungeonRoomEntranceApplier.Apply(instance.transform, neighborPlusZ, neighborMinusZ, neighborPlusX, neighborMinusX);

            if (addMeshCollidersToRooms)
                DungeonRoomMeshColliders.EnsureOnHierarchy(instance);

            if (assignDungeonCollisionLayer && DungeonCollisionLayerIndex >= 0)
                SetLayerRecursively(instance.transform, DungeonCollisionLayerIndex);
        }

        UpdateDungeonEnterSpawn(startRoomInstance);

        Debug.Log($"ProceduralDungeonGenerator: сгенерировано {placedCells.Count} комнат (seed={seed}).");
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
            return;

        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        var go = new GameObject(GeneratedRootName);
        go.transform.SetParent(transform, false);
        generatedRoot = go.transform;
    }

    private void EnsureDungeonEnterSpawnTransform()
    {
        if (dungeonEnterSpawn != null)
            return;

        var go = new GameObject("DungeonEnterSpawn");
        go.transform.SetParent(transform, false);
        dungeonEnterSpawn = go.transform;
    }

    private void UpdateDungeonEnterSpawn(GameObject startRoomInstance)
    {
        EnsureDungeonEnterSpawnTransform();

        if (startRoomInstance == null)
        {
            Debug.LogWarning("ProceduralDungeonGenerator: нет стартовой комнаты (0,0), точка входа не обновлена.");
            return;
        }

        Transform roomRoot = startRoomInstance.transform;
        Transform anchor = null;
        if (!string.IsNullOrWhiteSpace(playerSpawnChildName))
            anchor = FindChildTransformByBaseName(roomRoot, playerSpawnChildName.Trim());

        if (anchor != null)
        {
            dungeonEnterSpawn.position = anchor.position;
            dungeonEnterSpawn.rotation = anchor.rotation;
        }
        else
        {
            dungeonEnterSpawn.position = roomRoot.TransformPoint(playerSpawnLocalInRoom);
            dungeonEnterSpawn.rotation = roomRoot.rotation;
        }
    }

    private static Transform FindChildTransformByBaseName(Transform root, string baseName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (StripCloneSuffixStatic(t.name) == baseName)
                return t;
        }

        return null;
    }

    private void RefreshDungeonCollisionLayerIndex()
    {
        DungeonCollisionLayerIndex = -1;
        if (!assignDungeonCollisionLayer || string.IsNullOrWhiteSpace(dungeonCollisionLayerName))
            return;

        int idx = LayerMask.NameToLayer(dungeonCollisionLayerName.Trim());
        if (idx < 0)
        {
            Debug.LogWarning(
                $"ProceduralDungeonGenerator: слой «{dungeonCollisionLayerName}» не найден. Добавь слой в Tags & Layers или отключи assignDungeonCollisionLayer.");
            return;
        }

        DungeonCollisionLayerIndex = idx;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private static string StripCloneSuffixStatic(string instanceName)
    {
        const string suffix = " (Clone)";
        if (instanceName.EndsWith(suffix))
            return instanceName.Substring(0, instanceName.Length - suffix.Length);
        return instanceName;
    }

    private static void BuildCells(int targetCount, HashSet<Vector2Int> outCells)
    {
        outCells.Clear();
        if (targetCount <= 0)
            return;

        var stack = new Stack<Vector2Int>();
        var start = Vector2Int.zero;
        outCells.Add(start);
        stack.Push(start);

        Vector2Int[] dirs =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        while (outCells.Count < targetCount && stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            var freeNeighbors = new List<Vector2Int>(4);
            foreach (Vector2Int d in dirs)
            {
                Vector2Int n = current + d;
                if (!outCells.Contains(n))
                    freeNeighbors.Add(n);
            }

            if (freeNeighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int next = freeNeighbors[Random.Range(0, freeNeighbors.Count)];
            outCells.Add(next);
            stack.Push(next);
        }

        int guard = 0;
        while (outCells.Count < targetCount && guard++ < targetCount * 20)
        {
            Vector2Int? add = null;

            foreach (Vector2Int c in outCells)
            {
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int n = c + d;
                    if (!outCells.Contains(n))
                    {
                        add = n;
                        break;
                    }
                }
                if (add.HasValue)
                    break;
            }

            if (!add.HasValue)
                break;

            outCells.Add(add.Value);
            stack.Push(add.Value);
        }

        if (outCells.Count < targetCount)
            Debug.LogWarning($"ProceduralDungeonGenerator: удалось разместить только {outCells.Count} из {targetCount} комнат.");
    }
}

/// <summary>
/// Настройка проёмов префаба комнаты.
/// Сетка: сосед (gx, gz+1) стоит в мире по +Z — совпадает с проёмом «Up» в Room.prefab; (gx, gz−1) → «Down»; (gx+1, gz) → «Right»; (gx−1, gz) → «Left».
/// Есть сосед → открытый проход: стена выкл, дверь выкл. Нет соседа → тупик: стена вкл, дверь выкл.
/// </summary>
public static class DungeonRoomEntranceApplier
{
    private const string CloneSuffix = " (Clone)";

    public static void Apply(
        Transform roomRoot,
        bool neighborAlongWorldPlusZ,
        bool neighborAlongWorldMinusZ,
        bool neighborAlongWorldPlusX,
        bool neighborAlongWorldMinusX)
    {
        Transform entrances = roomRoot.Find("Entrances");
        if (entrances == null)
        {
            foreach (Transform t in roomRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Entrances")
                {
                    entrances = t;
                    break;
                }
            }
        }

        if (entrances == null)
        {
            Debug.LogWarning($"DungeonRoomEntranceApplier: у «{roomRoot.name}» не найден объект Entrances.");
            return;
        }

        foreach (Transform t in entrances.GetComponentsInChildren<Transform>(true))
        {
            switch (StripCloneSuffix(t.name))
            {
                case "Up Wall":
                    t.gameObject.SetActive(!neighborAlongWorldPlusZ);
                    break;
                case "Up Door":
                    t.gameObject.SetActive(false);
                    break;
                case "Down Wall":
                    t.gameObject.SetActive(!neighborAlongWorldMinusZ);
                    break;
                case "Down Door":
                    t.gameObject.SetActive(false);
                    break;
                case "Right Wall":
                    t.gameObject.SetActive(!neighborAlongWorldPlusX);
                    break;
                case "Right Door":
                    t.gameObject.SetActive(false);
                    break;
                case "Left Wall":
                    t.gameObject.SetActive(!neighborAlongWorldMinusX);
                    break;
                case "Left Door":
                    t.gameObject.SetActive(false);
                    break;
            }
        }
    }

    private static string StripCloneSuffix(string instanceName)
    {
        if (instanceName.EndsWith(CloneSuffix))
            return instanceName.Substring(0, instanceName.Length - CloneSuffix.Length);
        return instanceName;
    }
}
