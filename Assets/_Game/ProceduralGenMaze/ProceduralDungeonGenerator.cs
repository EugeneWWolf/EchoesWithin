using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

/// <summary>
/// Процедурная сетка комнат из одного префаба.
/// Сосед на сетке (gx±1, gz) и (gx, gz±1) совмещается с миром: +X и +Z соответственно.
/// </summary>
[DefaultExecutionOrder(-400)]
public class ProceduralDungeonGenerator : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedDungeonRooms";

    // Только оси XZ — вдвое меньше Raycast'ов при генерации нод, чем 8 направлений.
    private static readonly Vector3[] HorizontalWallProbeDirections =
    {
        Vector3.forward,
        Vector3.back,
        Vector3.right,
        Vector3.left
    };

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

    [Header("Зона выхода из данжа")]
    [Tooltip("После Generate создать зону выхода (простую или из префаба) с TeleportZone")]
    [SerializeField] private bool spawnDungeonExitPrefabAfterGenerate;
    [Tooltip("Если включено — спавнится только пустышка: TeleportZone + синий диск на полу + надпись (без префаба двери). Игнорирует dungeonExitZonePrefab.")]
    [SerializeField] private bool useSimpleProceduralDungeonExit = true;
    [Tooltip("Префаб выхода (если простой режим выкл): корень с TeleportZone + триггер, или меш — тогда TeleportZone добавится на корень.")]
    [SerializeField] private GameObject dungeonExitZonePrefab;
    [Tooltip("Простой выход: высота зоны (Y) и при выкл. «как диск» — ширина/глубина (X/Z). Пивот объекта на полу.")]
    [SerializeField] private Vector3 simpleExitTriggerSize = new Vector3(1.5f, 2.2f, 1.2f);
    [Tooltip("Смещение центра BoxCollider. Y игнорируется, если включено выравнивание по полу — иначе при маленькой высоте бокс уходит под/над пол.")]
    [SerializeField] private Vector3 simpleExitTriggerCenter = new Vector3(0f, 1.1f, 0f);
    [Tooltip("Радиус синего диска на полу")]
    [SerializeField] private float simpleExitMarkerRadius = 0.75f;
    [SerializeField] private Color simpleExitMarkerColor = new Color(0.15f, 0.55f, 1f, 0.92f);
    [SerializeField] private string simpleExitSignText = "Выход";
    [SerializeField] private float simpleExitSignHeight = 1.85f;
    [Tooltip("Центр триггера по Y = половина высоты: нижняя грань на пивоте (пол). Устраняет провал сквозь пол при смене высоты бокса.")]
    [SerializeField] private bool simpleExitTriggerBottomAlignedToFloor = true;
    [Tooltip("X и Z размера триггера = диаметру диска (2× радиус маркера), как визуальный круг.")]
    [SerializeField] private bool simpleExitTriggerFootprintMatchesMarker = true;
    [Tooltip("Случайная точка на полу (TryGetRandomFloorPosition). Если выкл — точка входа + локальное смещение")]
    [SerializeField] private bool placeDungeonExitAtRandomFloor = true;
    [Tooltip("Когда случайный пол не используется: смещение в локальных осях DungeonEnterSpawn")]
    [SerializeField] private Vector3 exitZoneOffsetLocalFromEnterSpawn = new Vector3(4f, 0f, -5f);
    [Tooltip("Куда вести игрока после выхода (TeleportZone). Если пусто — ищется объект с именем ReturnSpawnZone в сцене.")]
    [SerializeField] private Transform dungeonSurfaceReturnPoint;
    [Tooltip("Если в префабе выхода нет TeleportZone (например только дверь) — добавляется на корень с триггером этого размера.")]
    [SerializeField] private Vector3 runtimeExitTriggerSize = new Vector3(2.6f, 3.2f, 1.8f);
    [SerializeField] private Vector3 runtimeExitTriggerCenter = new Vector3(0f, 1.55f, 0f);

    [Header("Коллизии")]
    [Tooltip("У префабов StylizedHandPaintedDungeon часто нет коллайдеров — добавляем MeshCollider к каждому MeshFilter")]
    [SerializeField] private bool addMeshCollidersToRooms = true;
    [Tooltip("Не ставить MeshCollider на меши под этими родителями (имя без « (Clone)»). В Room.prefab потолок — под «Cellar»; иначе луч TryGetRandomFloorPosition бьёт в верх потолка как в «пол».")]
    [SerializeField] private bool skipMeshCollidersUnderNamedAncestors = true;
    [SerializeField] private string[] skipMeshColliderAncestorNames = { "Cellar", "Ceiling" };
    [Tooltip("Назначить всей сгенерированной геометрии слой (создай слой в Edit → Project Settings → Tags and Layers, например «Dungeon»)")]
    [SerializeField] private bool assignDungeonCollisionLayer = true;
    [SerializeField] private string dungeonCollisionLayerName = "Dungeon";

    [Header("Ноды спавна предметов (DungeonSpawnNode)")]
    [Tooltip("После генерации комнат создаётся родитель с DungeonSpawnNode на полу (имя по умолчанию ProceduralDungeonSpawnNodes) — для DungeonItemSpawner и патруля.")]
    [SerializeField] private bool createItemSpawnNodesAfterGenerate = true;
    [SerializeField] [Min(0)] private int itemSpawnNodeCount = 48;
    [Tooltip("Минимальное расстояние между нодами по XZ, чтобы не кучковались.")]
    [SerializeField] [Min(0.1f)] private float itemSpawnNodeMinSeparation = 2f;
    [SerializeField] private string itemSpawnNodesRootName = "ProceduralDungeonSpawnNodes";
    [Tooltip("Случайная точка на полу может быть у стены; лут с шириной торчит в меш. Лучи по горизонтали отбраковывают такие ноды.")]
    [SerializeField] private bool validateItemSpawnNodeWallClearance = true;
    [SerializeField] [Min(0.05f)] private float itemSpawnWallHorizontalProbeDistance = 0.42f;
    [SerializeField] [Min(0.02f)] private float itemSpawnWallProbeOriginYOffset = 0.22f;
    [Tooltip("Попадание в геометрию: если |dot(нормаль, вверх)| ниже — это боковая стена слишком близко (нода отклоняется).")]
    [SerializeField] [Range(0.15f, 0.95f)] private float itemSpawnWallRejectMaxAbsUpNormalDot = 0.5f;

    [Header("Lifecycle")]
    [SerializeField] private bool generateOnAwake = true;

    [Header("Случайная точка в данже (TeleportZone)")]
    [Tooltip("Какие слои участвуют в Raycast «вниз» при поиске пола. Должен включать слой пола/данжа.")]
    [SerializeField] private LayerMask randomFloorRaycastMask = ~0;
    [Tooltip("Насколько выше верхней границы bounds комнат начинать луч")]
    [SerializeField] private float randomFloorRaycastStartAboveBounds = 2f;
    [Tooltip("Минимум dot(нормаль, вверх) для «пола» при выборе попадания среди нескольких коллайдеров по одному лучу")]
    [SerializeField] [Range(0.2f, 0.99f)] private float randomFloorMinUpNormalDot = 0.45f;

    [Header("NavMesh для агентов (после генерации)")]
    [Tooltip("Печёт walkable NavMesh только по дочерним комнатам под GeneratedDungeonRooms. Без этого NavMeshAgent в процедурном данже не ходит и «висит».")]
    [SerializeField] private bool bakeNavMeshForGeneratedRooms = true;
    [SerializeField] private bool navMeshFromPhysicsColliders = true;
    [Tooltip("Меньше — не выкидывает узкие коридоры при маленьком roomInstanceUniformScale.")]
    [SerializeField] private float navMeshMinRegionArea = 0.05f;

    private Transform generatedRoot;
    private Transform itemSpawnNodesRoot;
    private RaycastHit[] randomFloorRaycastHits;
    private Transform dungeonEnterSpawn;
    private readonly HashSet<Vector2Int> placedCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, Transform> roomRootByCell = new Dictionary<Vector2Int, Transform>();

    public IReadOnlyCollection<Vector2Int> PlacedCells => placedCells;
    public Transform GeneratedRoot => generatedRoot;

    /// <summary>Родитель процедурно созданных DungeonSpawnNode; null если выключено или до первой генерации.</summary>
    public Transform ItemSpawnNodesRoot => itemSpawnNodesRoot;

    /// <summary>После полной генерации комнат, коллайдеров, точки входа и (опционально) выхода.</summary>
    public event System.Action OnAfterDungeonGenerated;

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
        ClearRuntimeNavMeshSurfaceData();
        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
            Destroy(generatedRoot.GetChild(i).gameObject);

        itemSpawnNodesRoot = null;
        placedCells.Clear();
        roomRootByCell.Clear();
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
            {
                if (skipMeshCollidersUnderNamedAncestors && skipMeshColliderAncestorNames != null &&
                    skipMeshColliderAncestorNames.Length > 0)
                    DungeonRoomMeshColliders.EnsureOnHierarchy(instance, skipMeshColliderAncestorNames);
                else
                    DungeonRoomMeshColliders.EnsureOnHierarchy(instance);
            }

            if (assignDungeonCollisionLayer && DungeonCollisionLayerIndex >= 0)
                SetLayerRecursively(instance.transform, DungeonCollisionLayerIndex);

            roomRootByCell[cell] = instance.transform;
        }

        UpdateDungeonEnterSpawn(startRoomInstance);

        if (spawnDungeonExitPrefabAfterGenerate &&
            (useSimpleProceduralDungeonExit || dungeonExitZonePrefab != null))
            SpawnDungeonExitZone(startRoomInstance);

        CreateItemSpawnNodesIfEnabled();

        BakeNavMeshForGeneratedGeometryIfEnabled();

        OnAfterDungeonGenerated?.Invoke();

        Debug.Log($"ProceduralDungeonGenerator: сгенерировано {placedCells.Count} комнат (seed={seed}).");
    }

    private void CreateItemSpawnNodesIfEnabled()
    {
        itemSpawnNodesRoot = null;

        if (!createItemSpawnNodesAfterGenerate || itemSpawnNodeCount <= 0)
            return;

        if (generatedRoot == null || generatedRoot.childCount == 0)
            return;

        Physics.SyncTransforms();

        var rootGo = new GameObject(string.IsNullOrWhiteSpace(itemSpawnNodesRootName)
            ? "ProceduralDungeonSpawnNodes"
            : itemSpawnNodesRootName.Trim());
        rootGo.transform.SetParent(generatedRoot, false);
        itemSpawnNodesRoot = rootGo.transform;

        float sep = itemSpawnNodeMinSeparation;
        float sepSqr = sep * sep;
        var placed = new List<Vector3>(itemSpawnNodeCount);
        int maxTries = Mathf.Max(itemSpawnNodeCount * 40, 120);
        int tries = 0;

        while (placed.Count < itemSpawnNodeCount && tries < maxTries)
        {
            tries++;
            if (!TryGetRandomFloorPosition(out Vector3 pos, out _, 18))
                continue;

            if (validateItemSpawnNodeWallClearance && !IsFloorPointClearOfNearbyVerticalWalls(pos))
                continue;

            bool tooClose = false;
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = placed[i].x - pos.x;
                float dz = placed[i].z - pos.z;
                if (dx * dx + dz * dz < sepSqr)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            placed.Add(pos);
            var nodeGo = new GameObject($"SpawnNode_{placed.Count - 1}");
            nodeGo.transform.SetParent(itemSpawnNodesRoot, false);
            nodeGo.transform.position = pos;
            nodeGo.AddComponent<DungeonSpawnNode>();
        }

        if (placed.Count < itemSpawnNodeCount)
        {
            Debug.LogWarning(
                $"ProceduralDungeonGenerator: создано {placed.Count} нодов спавна из запрошенных {itemSpawnNodeCount} " +
                $"(попыток: {tries}). Увеличь попытки или уменьши itemSpawnNodeMinSeparation.");
        }
        else
        {
            Debug.Log($"ProceduralDungeonGenerator: создано {placed.Count} нодов спавна предметов.");
        }
    }

    /// <summary>
    /// Та же проверка, что для нод спавна лута: можно вызывать из спавнера для fallback-позиций.
    /// </summary>
    public bool IsLootStandPointClearOfNearbyWalls(Vector3 floorPoint)
    {
        if (!validateItemSpawnNodeWallClearance)
            return true;
        return IsFloorPointClearOfNearbyVerticalWalls(floorPoint);
    }

    /// <summary>
    /// Отсекает точки у вертикальной геометрии данжа: горизонтальные лучи не должны быстро встречать «боковые» нормали.
    /// </summary>
    private bool IsFloorPointClearOfNearbyVerticalWalls(Vector3 floorPoint)
    {
        int mask = randomFloorRaycastMask.value == 0 ? Physics.DefaultRaycastLayers : randomFloorRaycastMask.value;
        Vector3 origin = floorPoint + Vector3.up * itemSpawnWallProbeOriginYOffset;
        float maxDist = itemSpawnWallHorizontalProbeDistance;

        foreach (Vector3 dir in HorizontalWallProbeDirections)
        {
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, mask, QueryTriggerInteraction.Ignore))
                continue;

            float upDot = Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up));
            if (upDot < itemSpawnWallRejectMaxAbsUpNormalDot)
                return false;
        }

        return true;
    }

    private void ClearRuntimeNavMeshSurfaceData()
    {
        if (generatedRoot == null)
            return;

        NavMeshSurface surface = generatedRoot.GetComponent<NavMeshSurface>();
        if (surface != null)
            surface.RemoveData();
    }

    private void BakeNavMeshForGeneratedGeometryIfEnabled()
    {
        if (!bakeNavMeshForGeneratedRooms)
            return;
        if (generatedRoot == null || generatedRoot.childCount == 0)
            return;

        Physics.SyncTransforms();

        NavMeshSurface surface = generatedRoot.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = generatedRoot.gameObject.AddComponent<NavMeshSurface>();

        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = navMeshFromPhysicsColliders
            ? NavMeshCollectGeometry.PhysicsColliders
            : NavMeshCollectGeometry.RenderMeshes;

        if (assignDungeonCollisionLayer && DungeonCollisionLayerIndex >= 0)
            surface.layerMask = 1 << DungeonCollisionLayerIndex;
        else
            surface.layerMask = randomFloorRaycastMask.value != 0
                ? randomFloorRaycastMask
                : (LayerMask)Physics.DefaultRaycastLayers;

        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.minRegionArea = navMeshMinRegionArea;

        surface.BuildNavMesh();

        if (surface.navMeshData != null)
            Debug.Log("ProceduralDungeonGenerator: NavMesh процедурного данжа пересобран.");
        else
        {
            Debug.LogWarning(
                "ProceduralDungeonGenerator: NavMesh bake не создал NavMeshData. Проверь слой коллайдеров комнат, " +
                "что на полах есть MeshCollider (addMeshCollidersToRooms) и что navMeshFromPhysicsColliders совпадает с типом геометрии.");
        }
    }

    /// <summary>
    /// Подтягивает Y к полу данжа (те же слои и нормаль, что у спавн-нод).
    /// </summary>
    public bool TrySnapPositionToDungeonFloor(ref Vector3 worldPosition, float rayStartAbove = 6f, float rayLength = 80f)
    {
        int mask = randomFloorRaycastMask.value == 0 ? Physics.DefaultRaycastLayers : randomFloorRaycastMask.value;
        Vector3 origin = worldPosition + Vector3.up * rayStartAbove;
        EnsureRandomFloorHitBuffer();
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            randomFloorRaycastHits,
            rayStartAbove + rayLength,
            mask,
            QueryTriggerInteraction.Ignore);
        if (!TryPickFloorHit(randomFloorRaycastHits, count, randomFloorMinUpNormalDot, out RaycastHit floorHit))
            return false;

        worldPosition = floorHit.point + Vector3.up * 0.06f;
        return true;
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

    /// <summary>
    /// Случайная точка на полу внутри сгенерированного данжа (по объединённым bounds рендереров, луч сверху вниз).
    /// </summary>
    public bool TryGetRandomFloorPosition(out Vector3 worldPosition, out Quaternion worldRotation, int maxAttempts = 40)
    {
        worldPosition = default;
        worldRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (generatedRoot == null || generatedRoot.childCount == 0)
            return false;

        if (!TryComputeRenderersWorldBounds(generatedRoot, out Bounds bounds))
            return false;

        int mask = randomFloorRaycastMask.value == 0 ? Physics.DefaultRaycastLayers : randomFloorRaycastMask.value;
        float rayLength = bounds.size.y + randomFloorRaycastStartAboveBounds + 8f;
        EnsureRandomFloorHitBuffer();

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);
            Vector3 origin = new Vector3(x, bounds.max.y + randomFloorRaycastStartAboveBounds, z);
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, randomFloorRaycastHits, rayLength, mask, QueryTriggerInteraction.Ignore);
            if (TryPickFloorHit(randomFloorRaycastHits, count, randomFloorMinUpNormalDot, out RaycastHit floorHit))
            {
                worldPosition = floorHit.point + Vector3.up * 0.06f;
                return true;
            }
        }

        worldPosition = new Vector3(bounds.center.x, bounds.min.y + 0.5f, bounds.center.z);
        return true;
    }

    /// <summary>
    /// Точка на полу в комнате, максимально удалённой от стартовой клетки (0,0) по числу шагов по сетке соседей;
    /// затем случайная точка на полу внутри bounds этой комнаты. При неудаче — случайная точка по всему данжу с отступом от входа.
    /// </summary>
    public bool TryGetRandomFloorPositionFarthestFromEntrance(
        Vector3 entranceWorldPosition,
        float minHorizontalDistanceFromEntrance,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        worldPosition = default;
        worldRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (TryGetFarthestCellsByGraphDistanceFromStart(out List<Vector2Int> farCells) && farCells.Count > 0)
        {
            ShuffleCellList(farCells);
            foreach (Vector2Int cell in farCells)
            {
                if (!roomRootByCell.TryGetValue(cell, out Transform roomRoot) || roomRoot == null)
                    continue;

                if (TrySampleFloorInRoomAvoiding(
                        roomRoot,
                        entranceWorldPosition,
                        minHorizontalDistanceFromEntrance,
                        out worldPosition,
                        out worldRotation,
                        72))
                    return true;

                if (minHorizontalDistanceFromEntrance > 0.01f &&
                    TrySampleFloorInRoomAvoiding(roomRoot, entranceWorldPosition, 0f, out worldPosition, out worldRotation, 48))
                    return true;
            }
        }

        if (TryGetRandomFloorPositionAvoidingHorizontal(entranceWorldPosition, minHorizontalDistanceFromEntrance, out worldPosition, out worldRotation, 96))
            return true;

        return TryGetRandomFloorPosition(out worldPosition, out worldRotation, 40);
    }

    private bool TryGetFarthestCellsByGraphDistanceFromStart(out List<Vector2Int> tiedFarthest)
    {
        tiedFarthest = new List<Vector2Int>();
        if (placedCells == null || placedCells.Count == 0)
            return false;

        Vector2Int origin = Vector2Int.zero;
        if (!placedCells.Contains(origin))
        {
            foreach (Vector2Int c in placedCells)
            {
                origin = c;
                break;
            }
        }

        var dist = new Dictionary<Vector2Int, int>();
        var q = new Queue<Vector2Int>();
        dist[origin] = 0;
        q.Enqueue(origin);

        Vector2Int[] dirs =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue();
            int d = dist[c];
            foreach (Vector2Int o in dirs)
            {
                Vector2Int n = c + o;
                if (!placedCells.Contains(n) || dist.ContainsKey(n))
                    continue;
                dist[n] = d + 1;
                q.Enqueue(n);
            }
        }

        int best = -1;
        foreach (KeyValuePair<Vector2Int, int> kv in dist)
        {
            if (kv.Value > best)
                best = kv.Value;
        }

        if (best < 0)
            return false;

        foreach (KeyValuePair<Vector2Int, int> kv in dist)
        {
            if (kv.Value == best)
                tiedFarthest.Add(kv.Key);
        }

        return tiedFarthest.Count > 0;
    }

    private static void ShuffleCellList(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private bool TrySampleFloorInRoomAvoiding(
        Transform roomWorldRoot,
        Vector3 avoidWorld,
        float minHorizontalDistance,
        out Vector3 worldPosition,
        out Quaternion worldRotation,
        int maxAttempts)
    {
        worldPosition = default;
        worldRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (!TryComputeRenderersWorldBounds(roomWorldRoot, out Bounds bounds))
            return false;

        float minSqr = minHorizontalDistance * minHorizontalDistance;
        int mask = randomFloorRaycastMask.value == 0 ? Physics.DefaultRaycastLayers : randomFloorRaycastMask.value;
        float rayLength = bounds.size.y + randomFloorRaycastStartAboveBounds + 8f;
        EnsureRandomFloorHitBuffer();

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);
            if (minHorizontalDistance > 0.01f)
            {
                float dx = x - avoidWorld.x;
                float dz = z - avoidWorld.z;
                if (dx * dx + dz * dz < minSqr)
                    continue;
            }

            Vector3 origin = new Vector3(x, bounds.max.y + randomFloorRaycastStartAboveBounds, z);
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, randomFloorRaycastHits, rayLength, mask, QueryTriggerInteraction.Ignore);
            if (TryPickFloorHit(randomFloorRaycastHits, count, randomFloorMinUpNormalDot, out RaycastHit floorHit))
            {
                worldPosition = floorHit.point + Vector3.up * 0.06f;
                return true;
            }
        }

        return false;
    }

    private bool TryGetRandomFloorPositionAvoidingHorizontal(
        Vector3 avoidWorld,
        float minHorizontalDistance,
        out Vector3 worldPosition,
        out Quaternion worldRotation,
        int maxAttempts)
    {
        worldPosition = default;
        worldRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (generatedRoot == null || generatedRoot.childCount == 0)
            return false;

        if (!TryComputeRenderersWorldBounds(generatedRoot, out Bounds bounds))
            return false;

        float minSqr = minHorizontalDistance * minHorizontalDistance;
        int mask = randomFloorRaycastMask.value == 0 ? Physics.DefaultRaycastLayers : randomFloorRaycastMask.value;
        float rayLength = bounds.size.y + randomFloorRaycastStartAboveBounds + 8f;
        EnsureRandomFloorHitBuffer();

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);
            if (minHorizontalDistance > 0.01f)
            {
                float dx = x - avoidWorld.x;
                float dz = z - avoidWorld.z;
                if (dx * dx + dz * dz < minSqr)
                    continue;
            }

            Vector3 origin = new Vector3(x, bounds.max.y + randomFloorRaycastStartAboveBounds, z);
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, randomFloorRaycastHits, rayLength, mask, QueryTriggerInteraction.Ignore);
            if (TryPickFloorHit(randomFloorRaycastHits, count, randomFloorMinUpNormalDot, out RaycastHit floorHit))
            {
                worldPosition = floorHit.point + Vector3.up * 0.06f;
                return true;
            }
        }

        return false;
    }

    private void SpawnDungeonExitZone(GameObject startRoomInstance)
    {
        Vector3 pos;
        Quaternion rot;

        if (placeDungeonExitAtRandomFloor)
        {
            if (!TryGetRandomFloorPosition(out pos, out rot, 48))
                TryExitFallbackNearEnter(startRoomInstance, out pos, out rot);
        }
        else
            TryExitFallbackNearEnter(startRoomInstance, out pos, out rot);

        GameObject exitObj = useSimpleProceduralDungeonExit
            ? CreateSimpleProceduralExitZone(pos, rot)
            : Instantiate(dungeonExitZonePrefab, pos, rot, generatedRoot);

        // Простой выход всегда на Default: иначе после SetLayerRecursively(..., Dungeon) маркер/текст не рисуются,
        // если камера не включает слой Dungeon.
        if (useSimpleProceduralDungeonExit)
            SetLayerRecursively(exitObj.transform, 0);
        else if (assignDungeonCollisionLayer && DungeonCollisionLayerIndex >= 0)
            SetLayerRecursively(exitObj.transform, DungeonCollisionLayerIndex);

        ConfigureSpawnedDungeonExit(exitObj);
    }

    private GameObject CreateSimpleProceduralExitZone(Vector3 worldPos, Quaternion worldRot)
    {
        var go = new GameObject("DungeonExit_Simple");
        go.transform.SetParent(generatedRoot, false);
        go.transform.SetPositionAndRotation(worldPos, worldRot);

        Vector3 size = simpleExitTriggerSize;
        if (simpleExitTriggerFootprintMatchesMarker)
        {
            float d = Mathf.Max(0.15f, simpleExitMarkerRadius * 2f);
            size.x = d;
            size.z = d;
        }

        size.x = Mathf.Max(0.05f, size.x);
        size.y = Mathf.Max(0.35f, size.y);
        size.z = Mathf.Max(0.05f, size.z);

        Vector3 center = simpleExitTriggerBottomAlignedToFloor
            ? new Vector3(simpleExitTriggerCenter.x, size.y * 0.5f, simpleExitTriggerCenter.z)
            : simpleExitTriggerCenter;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        box.center = center;

        var zone = go.AddComponent<TeleportZone>();
        zone.ApplyProceduralExitVisuals(
            true,
            simpleExitMarkerRadius,
            simpleExitMarkerColor,
            true,
            simpleExitSignText,
            simpleExitSignHeight);

        return go;
    }

    private void ConfigureSpawnedDungeonExit(GameObject exitRoot)
    {
        if (exitRoot == null)
            return;

        TeleportZone zone = exitRoot.GetComponent<TeleportZone>();
        if (zone == null)
            zone = exitRoot.GetComponentInChildren<TeleportZone>(true);

        if (zone == null)
        {
            BoxCollider box = exitRoot.GetComponent<BoxCollider>();
            if (box == null)
                box = exitRoot.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = runtimeExitTriggerSize;
            box.center = runtimeExitTriggerCenter;
            zone = exitRoot.AddComponent<TeleportZone>();
        }

        Transform surface = dungeonSurfaceReturnPoint != null
            ? dungeonSurfaceReturnPoint
            : FindReturnSpawnZoneTransform();
        if (surface != null)
            zone.SetReturnSpawnPoint(surface);
        else
            Debug.LogWarning(
                "ProceduralDungeonGenerator: не задан dungeonSurfaceReturnPoint и в сцене нет объекта «ReturnSpawnZone» — TeleportZone не сможет вернуть игрока на поверхность.");
    }

    private static Transform FindReturnSpawnZoneTransform()
    {
        GameObject found = GameObject.Find("ReturnSpawnZone");
        return found != null ? found.transform : null;
    }

    private void TryExitFallbackNearEnter(GameObject startRoomInstance, out Vector3 pos, out Quaternion rot)
    {
        if (dungeonEnterSpawn != null)
        {
            pos = dungeonEnterSpawn.TransformPoint(exitZoneOffsetLocalFromEnterSpawn);
            rot = dungeonEnterSpawn.rotation;
            return;
        }

        if (startRoomInstance != null)
        {
            pos = startRoomInstance.transform.TransformPoint(exitZoneOffsetLocalFromEnterSpawn);
            rot = startRoomInstance.transform.rotation;
            return;
        }

        pos = transform.position + exitZoneOffsetLocalFromEnterSpawn;
        rot = Quaternion.identity;
    }

    private static bool TryComputeRenderersWorldBounds(Transform root, out Bounds merged)
    {
        merged = default;
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!any)
            {
                merged = r.bounds;
                any = true;
            }
            else
                merged.Encapsulate(r.bounds);
        }

        return any && merged.size.sqrMagnitude > 0.0001f;
    }

    private void EnsureRandomFloorHitBuffer()
    {
        if (randomFloorRaycastHits == null || randomFloorRaycastHits.Length < 32)
            randomFloorRaycastHits = new RaycastHit[32];
    }

    /// <summary>
    /// Среди попаданий луча вниз выбирает самую низкую точку с «половой» нормалью (исключает верх декора / потолок с той же нормалью вверх, если ниже есть второй слой).
    /// </summary>
    private static bool TryPickFloorHit(RaycastHit[] hits, int count, float minUpNormalDot, out RaycastHit best)
    {
        best = default;
        if (hits == null || count <= 0)
            return false;

        bool any = false;
        float bestY = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            if (h.normal.y < minUpNormalDot)
                continue;
            if (!any || h.point.y < bestY)
            {
                any = true;
                bestY = h.point.y;
                best = h;
            }
        }

        return any;
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
