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
    [SerializeField] private Vector2 cellSize = new Vector2(11f, 11f);
    [SerializeField] private int randomSeed = 12345;
    [SerializeField] private bool randomizeSeedOnPlay;

    [Header("Prefab orientation (если проёмы не совпадают с сеткой)")]
    [Tooltip("Поменять местами Up и Down в префабе относительно соседа по +Z / −Z")]
    [SerializeField] private bool flipPrefabUpDown;
    [Tooltip("Поменять местами Left и Right в префабе относительно соседа по −X / +X")]
    [SerializeField] private bool flipPrefabLeftRight;

    [Header("Lifecycle")]
    [SerializeField] private bool generateOnAwake = true;

    private Transform generatedRoot;
    private readonly HashSet<Vector2Int> placedCells = new HashSet<Vector2Int>();

    public IReadOnlyCollection<Vector2Int> PlacedCells => placedCells;
    public Transform GeneratedRoot => generatedRoot;

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

        foreach (Vector2Int cell in placedCells)
        {
            Vector3 worldPos = transform.position + new Vector3(cell.x * cellSize.x, 0f, cell.y * cellSize.y);
            GameObject instance = Instantiate(roomPrefab, worldPos, Quaternion.identity, generatedRoot);

            bool neighborPlusZ = placedCells.Contains(cell + new Vector2Int(0, 1));
            bool neighborMinusZ = placedCells.Contains(cell + new Vector2Int(0, -1));
            bool neighborPlusX = placedCells.Contains(cell + new Vector2Int(1, 0));
            bool neighborMinusX = placedCells.Contains(cell + new Vector2Int(-1, 0));

            if (flipPrefabUpDown)
                (neighborPlusZ, neighborMinusZ) = (neighborMinusZ, neighborPlusZ);
            if (flipPrefabLeftRight)
                (neighborPlusX, neighborMinusX) = (neighborMinusX, neighborPlusX);

            DungeonRoomEntranceApplier.Apply(instance.transform, neighborPlusZ, neighborMinusZ, neighborPlusX, neighborMinusX);
        }

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
