using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Монстр подземелья, который патрулирует между нодами и преследует игрока
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DungeonMonster : Enemy
{
    [Header("Patrol Settings")]
    [SerializeField] private DungeonNodeGenerator nodeGenerator; // Опционально - можно использовать ноды
    [SerializeField] private PatrolMode patrolMode = PatrolMode.WanderArea;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waitTimeAtNode = 2f;
    [SerializeField] private float nodeReachDistance = 0.5f;

    [Header("Wander Area Settings (if not using nodes)")]
    [SerializeField] private Vector3 patrolCenter = Vector3.zero;
    [SerializeField] private Vector3 patrolAreaSize = new Vector3(20f, 5f, 20f);
    [SerializeField] private float wanderPointDistance = 5f; // Расстояние между точками патрулирования
    [SerializeField] private bool useTransformAsCenter = true;

    [Header("Chase Settings")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float chaseRange = 15f;
    [SerializeField] private float fieldOfViewAngle = 120f;

    [Header("Процедурный данж")]
    [Tooltip("Если задан — патруль по DungeonSpawnNode внутри сгенерированного данжа (родитель ItemSpawnNodesRoot).")]
    [SerializeField] private ProceduralDungeonGenerator proceduralDungeon;
    [SerializeField] private bool useProceduralSpawnNodesForPatrol = true;
    [Tooltip("При старте и после Regenerate — телепорт на случайную точку пола в данже (не у входа).")]
    [SerializeField] private bool relocateIntoProceduralDungeonOnReady = true;
    [SerializeField] private float minDistanceFromDungeonEnterWhenRelocating = 7f;
    [Tooltip("Сначала комната с максимальным расстоянием по сетке от старта (0,0), затем отступ от точки входа по XZ.")]
    [SerializeField] private bool spawnMonsterInGraphFarthestRoomFromEntrance = true;

    [Header("Поведение в лабиринте")]
    [Tooltip("Луч «пол под нодом» / привязка к земле: только эти слои (0 = авто: слой данжа из ProceduralDungeonGenerator, иначе все слои по умолчанию). Иначе луч цепляется за лут на Interactable и ломает NavMesh-цель.")]
    [SerializeField] private LayerMask patrolGroundRaycastMask;
    [Tooltip("Обнаружение без обзора и луча — «звук» в коридоре. 0 = выключено.")]
    [SerializeField] private float hearingDetectionRange = 6.5f;
    [Tooltip("Если нет зрения и слуха, но недавно видел — идёт сюда, пока не истечёт таймер.")]
    [SerializeField] private bool useLastKnownPositionWhenLosBlocked = true;
    [SerializeField] private float loseTargetAfterSecondsWithoutPerception = 3.5f;
    [SerializeField] private float hitEnrageDuration = 2f;
    [SerializeField] private float hitEnrageSpeedMultiplier = 1.35f;

    [Header("Respawn Settings")]
    [SerializeField] private float respawnTime = 10f;
    [SerializeField] private Vector3 spawnPosition;

    [Header("Damage Display")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private float damageTextHeight = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color patrolGizmoColor = Color.green;
    [SerializeField] private Color chaseGizmoColor = Color.red;

    private NavMeshAgent agent;
    private List<DungeonSpawnNode> patrolNodes = new List<DungeonSpawnNode>();
    private List<Vector3> wanderPoints = new List<Vector3>(); // Точки для патрулирования без нодов
    private int currentPatrolNodeIndex = 0;
    private int currentWanderPointIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;
    private bool isChasing = false;
    private float deathTime = 0f;
    private bool isRespawning = false;
    private Renderer[] renderers;
    private Collider[] colliders;

    private enum MonsterState
    {
        Patrolling,
        Chasing,
        Dead
    }

    private float lastTimePerceivedPlayer;
    private Vector3 lastKnownPlayerPosition;
    private float enragedUntil;

    private enum PatrolMode
    {
        UseNodes,      // Патрулирование по нодам (если назначены)
        WanderArea     // Блуждание по случайным точкам в области
    }

    private int PatrolGroundPhysicsMask =>
        patrolGroundRaycastMask.value != 0
            ? patrolGroundRaycastMask.value
            : (ProceduralDungeonGenerator.DungeonCollisionLayerIndex is >= 0 and <= 31
                ? 1 << ProceduralDungeonGenerator.DungeonCollisionLayerIndex
                : Physics.DefaultRaycastLayers);

    private MonsterState currentState = MonsterState.Patrolling;

    private void OnEnable()
    {
        if (proceduralDungeon == null)
            proceduralDungeon = FindObjectOfType<ProceduralDungeonGenerator>();

        if (proceduralDungeon != null)
            proceduralDungeon.OnAfterDungeonGenerated += OnDungeonGenerated;
    }

    private void OnDisable()
    {
        if (proceduralDungeon != null)
            proceduralDungeon.OnAfterDungeonGenerated -= OnDungeonGenerated;
    }

    private void OnDungeonGenerated()
    {
        if (proceduralDungeon == null)
            return;

        if (relocateIntoProceduralDungeonOnReady && TryComputeRelocateFloorPosition(out Vector3 floorPos))
        {
            spawnPosition = floorPos;
            if (!isDead)
                ApplyWorldRelocate(floorPos);
        }

        if (!isDead)
            RefreshPatrolAfterDungeonChange();
    }

    private void RefreshPatrolAfterDungeonChange()
    {
        if (patrolMode != PatrolMode.UseNodes)
            return;

        FindPatrolNodes();
        isWaiting = false;
        waitTimer = 0f;
        if (!isDead && currentState == MonsterState.Patrolling && patrolNodes.Count > 0 && agent != null && agent.isOnNavMesh)
            MoveToNextNode();
    }

    private bool TryComputeRelocateFloorPosition(out Vector3 floorPos)
    {
        floorPos = default;
        if (proceduralDungeon == null)
            return false;

        Vector3 enterPos = proceduralDungeon.DungeonEnterSpawn != null
            ? proceduralDungeon.DungeonEnterSpawn.position
            : transform.position;

        if (spawnMonsterInGraphFarthestRoomFromEntrance &&
            proceduralDungeon.TryGetRandomFloorPositionFarthestFromEntrance(
                enterPos,
                minDistanceFromDungeonEnterWhenRelocating,
                out floorPos,
                out _))
            return true;

        float minSqr = minDistanceFromDungeonEnterWhenRelocating * minDistanceFromDungeonEnterWhenRelocating;

        for (int i = 0; i < 64; i++)
        {
            if (!proceduralDungeon.TryGetRandomFloorPosition(out Vector3 p, out _, 40))
                break;
            if ((p - enterPos).sqrMagnitude >= minSqr)
            {
                floorPos = p;
                return true;
            }
        }

        if (proceduralDungeon.TryGetRandomFloorPosition(out Vector3 fallback, out _, 32))
        {
            floorPos = fallback;
            return true;
        }

        return false;
    }

    private bool TryRelocateToProceduralDungeonFloor()
    {
        if (agent == null)
            return false;
        if (!TryComputeRelocateFloorPosition(out Vector3 p))
            return false;
        ApplyWorldRelocate(p);
        return true;
    }

    private void ApplyWorldRelocate(Vector3 worldPos)
    {
        Vector3 pos = worldPos;
        if (proceduralDungeon != null)
            proceduralDungeon.TrySnapPositionToDungeonFloor(ref pos);

        spawnPosition = pos;
        transform.position = pos;
        agent.Warp(pos);
        TryPlaceOnNavMesh();
    }

    protected override void Start()
    {
        base.Start();

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

        if (proceduralDungeon == null)
            proceduralDungeon = FindObjectOfType<ProceduralDungeonGenerator>();

        if (relocateIntoProceduralDungeonOnReady && proceduralDungeon != null)
            TryRelocateToProceduralDungeonFloor();

        // Сохраняем начальную позицию для респавна
        spawnPosition = transform.position;

        // Инициализируем патрулирование в зависимости от режима
        if (patrolMode == PatrolMode.UseNodes)
        {
            // Пытаемся найти ноды для патрулирования
            FindPatrolNodes();
        }
        else
        {
            // Создаём точки для патрулирования в области
            GenerateWanderPoints();
        }

        // Получаем компоненты для скрытия при смерти
        // includeInactive = true, чтобы найти renderers даже в неактивных дочерних объектах
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        // Если renderers не найдены, пытаемся найти в дочерних объектах
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        // Настраиваем NavMeshAgent
        agent.speed = patrolSpeed;
        agent.stoppingDistance = nodeReachDistance;

        // Убеждаемся, что агент размещен на NavMesh (не критично, попробуем позже)
        TryPlaceOnNavMesh();

        if (enableDebugLogs)
        {
            if (patrolMode == PatrolMode.UseNodes)
            {
                Debug.Log($"👹 DungeonMonster инициализирован. Режим: UseNodes, нодов: {patrolNodes.Count}");
            }
            else
            {
                Debug.Log($"👹 DungeonMonster инициализирован. Режим: WanderArea, точек блуждания: {wanderPoints.Count}");
            }
        }

        // Начинаем патрулирование
        if (patrolMode == PatrolMode.UseNodes && patrolNodes.Count > 0)
        {
            MoveToNextNode();
        }
        else if (patrolMode == PatrolMode.WanderArea && wanderPoints.Count > 0)
        {
            MoveToNextWanderPoint();
        }
    }

    protected override void UpdateEnemy()
    {
        if (isRespawning)
        {
            // Логируем, если респавн застрял
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log($"⚠ DungeonMonster: isRespawning = true, но респавн не завершился!");
            }
            return;
        }

        // Если монстр мертв, но состояние не Dead, переключаемся на Dead
        if (isDead && currentState != MonsterState.Dead)
        {
            currentState = MonsterState.Dead;
            if (enableDebugLogs)
                Debug.Log($"💀 DungeonMonster: Обнаружен мертвый монстр, переключаю состояние на Dead");
        }

        switch (currentState)
        {
            case MonsterState.Patrolling:
                // Не патрулируем, если мертвы
                if (isDead)
                {
                    currentState = MonsterState.Dead;
                    break;
                }

                if (patrolMode == PatrolMode.UseNodes && patrolNodes.Count > 0)
                {
                    UpdatePatrolling();
                }
                else if (patrolMode == PatrolMode.WanderArea)
                {
                    UpdateWandering();
                }
                CheckForPlayer();
                break;

            case MonsterState.Chasing:
                // Не преследуем, если мертвы
                if (isDead)
                {
                    currentState = MonsterState.Dead;
                    break;
                }

                UpdateChasing();
                break;

            case MonsterState.Dead:
                if (enableDebugLogs && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"💀 DungeonMonster: UpdateEnemy - Состояние Dead. isDead: {isDead}, Время смерти: {deathTime:F2}, Прошло: {Time.time - deathTime:F1}с, Нужно: {respawnTime}с, isRespawning: {isRespawning}, currentState: {currentState}");
                }
                CheckRespawn();
                break;

            default:
                // Логируем неизвестное состояние
                if (Time.frameCount % 120 == 0)
                {
                    Debug.LogWarning($"⚠ DungeonMonster: Неизвестное состояние: {currentState}");
                }
                break;
        }
    }

    /// <summary>
    /// Попытка разместить агента на NavMesh
    /// </summary>
    private void TryPlaceOnNavMesh()
    {
        if (agent == null || agent.isOnNavMesh) return;

        Vector3 currentPos = transform.position;

        // Пытаемся найти NavMesh в нескольких местах:
        // 1. Текущая позиция
        // 2. Найденная через raycast позиция земли
        // 3. Выше и ниже текущей позиции

        Vector3[] searchPositions = new Vector3[]
        {
            currentPos,                              // Текущая позиция
            currentPos + Vector3.up * 5f,           // Выше
            currentPos + Vector3.down * 5f,          // Ниже
        };

        // Также проверяем через raycast (маска — пол данжа, не мелкий лут)
        RaycastHit groundHit;
        if (Physics.Raycast(currentPos + Vector3.up * 10f, Vector3.down, out groundHit, 50f, PatrolGroundPhysicsMask,
                QueryTriggerInteraction.Ignore))
        {
            searchPositions = new Vector3[]
            {
                currentPos,
                groundHit.point + Vector3.up * 0.5f, // Найденная земля
                groundHit.point + Vector3.up * 2f,  // Чуть выше земли
                currentPos + Vector3.up * 5f,
                currentPos + Vector3.down * 5f,
            };
        }

        NavMeshHit hit;
        float searchRadius = 30f; // Увеличиваем радиус поиска

        // Пробуем найти NavMesh в каждой позиции
        foreach (Vector3 searchPos in searchPositions)
        {
            if (NavMesh.SamplePosition(searchPos, out hit, searchRadius, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                transform.position = hit.position;

                if (enableDebugLogs)
                {
                    Debug.Log($"✅ DungeonMonster: Агент размещен на NavMesh (позиция: {hit.position:F2})");
                }
                return; // Успешно разместили
            }
        }

        // Если ничего не нашли, выводим предупреждение (но не каждый кадр)
        if (Time.frameCount % 120 == 0 && enableDebugLogs) // Каждые ~2 секунды
        {
            Debug.LogWarning($"⚠ DungeonMonster: NavMesh не найден в радиусе {searchRadius} единиц. " +
                $"Текущая позиция: {currentPos:F2}. " +
                $"Убедитесь, что NavMesh построен в подземелье (Window → AI → Navigation → Bake)");
        }
    }

    /// <summary>
    /// Поиск нодов для патрулирования
    /// </summary>
    private void FindPatrolNodes()
    {
        patrolNodes.Clear();

        if (proceduralDungeon != null && useProceduralSpawnNodesForPatrol)
        {
            Transform procRoot = proceduralDungeon.ItemSpawnNodesRoot;
            if (procRoot != null)
            {
                DungeonSpawnNode[] procNodes = procRoot.GetComponentsInChildren<DungeonSpawnNode>(true);
                patrolNodes.AddRange(procNodes);
                patrolNodes.RemoveAll(node =>
                    node == null || !node.IsActive || !node.gameObject.activeInHierarchy);

                if (patrolNodes.Count > 0)
                {
                    if (enableDebugLogs)
                        Debug.Log($"✅ DungeonMonster: Патруль по {patrolNodes.Count} нодам процедурного данжа.");
                    return;
                }
            }

            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    "⚠ DungeonMonster: Процедурный патруль включён, но активных нодов нет — " +
                    "старые ноды из сцены не используются. Проверь itemSpawnNodeCount на ProceduralDungeonGenerator.");
            }
            return;
        }

        // Сначала пытаемся получить ноды через генератор
        if (nodeGenerator != null)
        {
            patrolNodes = nodeGenerator.GetAllNodes();
        }

        // Если через генератор не нашли, ищем в сцене
        if (patrolNodes.Count == 0)
        {
            // Ищем родительский объект DungeonSpawnNodes
            GameObject spawnNodesParent = GameObject.Find("DungeonSpawnNodes");
            if (spawnNodesParent != null)
            {
                DungeonSpawnNode[] nodes = spawnNodesParent.GetComponentsInChildren<DungeonSpawnNode>();
                patrolNodes.AddRange(nodes);

                if (enableDebugLogs)
                {
                    Debug.Log($"👹 DungeonMonster: Найдено {patrolNodes.Count} нодов через поиск в сцене");
                }
            }
            else
            {
                // Если не нашли родительский объект, ищем все ноды в сцене
                DungeonSpawnNode[] allNodes = FindObjectsOfType<DungeonSpawnNode>();
                patrolNodes.AddRange(allNodes);

                if (enableDebugLogs)
                {
                    Debug.Log($"👹 DungeonMonster: Найдено {patrolNodes.Count} нодов через поиск всех объектов в сцене");
                }
            }
        }

        patrolNodes.RemoveAll(node =>
            node == null || !node.IsActive || !node.gameObject.activeInHierarchy);

        if (patrolNodes.Count == 0)
        {
            Debug.LogWarning($"⚠ DungeonMonster: Не найдено активных нодов для патрулирования!");
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"✅ DungeonMonster: Найдено {patrolNodes.Count} активных нодов для патрулирования");
        }
    }

    /// <summary>
    /// Генерация точек для блуждания
    /// </summary>
    private void GenerateWanderPoints()
    {
        wanderPoints.Clear();

        Vector3 center = useTransformAsCenter ? spawnPosition : patrolCenter;
        Vector3 size = patrolAreaSize;

        // Генерируем точки в области патрулирования на NavMesh
        int pointsPerAxis = Mathf.CeilToInt(Mathf.Max(size.x, size.z) / wanderPointDistance);
        int maxAttempts = pointsPerAxis * pointsPerAxis * 3; // Увеличиваем попытки

        float searchRadius = 15f; // Увеличиваем радиус поиска NavMesh

        for (int i = 0; i < maxAttempts && wanderPoints.Count < 20; i++) // Максимум 20 точек
        {
            // Случайная позиция в области (сохраняем Y координату центра)
            Vector3 randomPos = center + new Vector3(
                Random.Range(-size.x / 2f, size.x / 2f),
                0, // Сохраняем высоту центра (для подземелья это важно)
                Random.Range(-size.z / 2f, size.z / 2f)
            );

            // Проверяем, есть ли NavMesh в этой точке с увеличенным радиусом
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, searchRadius, NavMesh.AllAreas))
            {
                wanderPoints.Add(hit.position);
            }
        }

        if (enableDebugLogs)
        {
            if (wanderPoints.Count > 0)
            {
                Debug.Log($"✅ DungeonMonster: Создано {wanderPoints.Count} точек для блуждания в области центром {center:F2}");
            }
            else
            {
                Debug.LogWarning($"⚠ DungeonMonster: Не удалось создать точки для блуждания в области {center:F2} размера {size:F2}. " +
                    $"Проверьте, что NavMesh построен в этой области! (Window → AI → Navigation → Bake)");
            }
        }
    }

    /// <summary>
    /// Обновление блуждания по области
    /// </summary>
    private void UpdateWandering()
    {
        // Если агент не на NavMesh, пытаемся восстановить его позицию
        if (!agent.isOnNavMesh)
        {
            if (Time.frameCount % 60 == 0)
            {
                TryPlaceOnNavMesh();
            }

            if (!agent.isOnNavMesh)
            {
                CheckForPlayer();
                return;
            }

            // Если точек нет, попробуем создать их снова
            if (wanderPoints.Count == 0)
            {
                GenerateWanderPoints();
            }
        }

        // Если нет точек, пытаемся создать их
        if (wanderPoints.Count == 0)
        {
            if (Time.frameCount % 60 == 0)
            {
                GenerateWanderPoints();
            }
            CheckForPlayer();
            return;
        }

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                MoveToNextWanderPoint();
            }
        }
        else if (!agent.pathPending && agent.hasPath)
        {
            float remainingDistance = agent.remainingDistance;
            if (remainingDistance < nodeReachDistance)
            {
                isWaiting = true;
                waitTimer = waitTimeAtNode;

                if (enableDebugLogs)
                {
                    Debug.Log($"👹 DungeonMonster: Достиг точки патрулирования {currentWanderPointIndex}, ожидание {waitTimeAtNode} сек.");
                }
            }
        }
        else if (!agent.pathPending)
        {
            // Если нет пути, двигаемся к следующей точке
            MoveToNextWanderPoint();
        }
    }

    /// <summary>
    /// Движение к следующей точке блуждания
    /// </summary>
    private void MoveToNextWanderPoint()
    {
        if (wanderPoints.Count == 0) return;

        if (agent == null || !agent.isOnNavMesh)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("⚠ DungeonMonster: Агент не на NavMesh, невозможно двигаться к точке блуждания");
            }
            return;
        }

        // Выбираем следующую случайную точку
        currentWanderPointIndex = Random.Range(0, wanderPoints.Count);
        Vector3 targetPoint = wanderPoints[currentWanderPointIndex];

        // Проверяем, что точка доступна на NavMesh
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(targetPoint, out hit, 5f, NavMesh.AllAreas))
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"⚠ DungeonMonster: Точка блуждания {targetPoint} не на NavMesh, пропускаю");
            }
            return;
        }

        targetPoint = hit.position;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
        agent.SetDestination(targetPoint);

        if (enableDebugLogs)
        {
            Debug.Log($"👹 DungeonMonster: Движусь к точке блуждания {currentWanderPointIndex} (позиция: {targetPoint})");
        }
    }

    /// <summary>
    /// Обновление патрулирования по нодам
    /// </summary>
    private void UpdatePatrolling()
    {
        // Если нет нодов, пытаемся найти их снова (на случай, если они сгенерированы позже)
        if (patrolNodes.Count == 0)
        {
            // Проверяем не чаще раза в секунду
            if (Time.frameCount % 60 == 0)
            {
                FindPatrolNodes();
            }

            if (patrolNodes.Count == 0)
            {
                // Просто проверяем игрока, патрулирование не требуется
                CheckForPlayer();
                return;
            }
        }

        // Если агент не на NavMesh, пытаемся восстановить его позицию
        if (!agent.isOnNavMesh)
        {
            // Пытаемся разместить на NavMesh (не чаще раза в секунду)
            if (Time.frameCount % 60 == 0)
            {
                TryPlaceOnNavMesh();
            }

            // Если все еще не на NavMesh, просто проверяем игрока
            if (!agent.isOnNavMesh)
            {
                CheckForPlayer();
                return;
            }

            // Если удалось восстановить, возобновляем патрулирование
            if (patrolNodes.Count > 0)
            {
                MoveToNextNode();
            }
        }

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                MoveToNextNode();
            }
        }
        else if (!agent.pathPending && agent.hasPath)
        {
            // Проверяем, достигли ли нода
            float remainingDistance = agent.remainingDistance;
            if (remainingDistance < nodeReachDistance)
            {
                // Достигли нода, ждем
                isWaiting = true;
                waitTimer = waitTimeAtNode;

                if (enableDebugLogs)
                {
                    Debug.Log($"👹 DungeonMonster: Достиг нода {currentPatrolNodeIndex}, ожидание {waitTimeAtNode} сек.");
                }
            }
        }
    }

    /// <summary>
    /// Движение к следующему ноду
    /// </summary>
    private void MoveToNextNode()
    {
        if (patrolNodes.Count == 0) return;

        if (agent == null || !agent.isOnNavMesh)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("⚠ DungeonMonster: Агент не на NavMesh, невозможно двигаться к ноду");
            }
            return;
        }

        currentPatrolNodeIndex = (currentPatrolNodeIndex + 1) % patrolNodes.Count;
        DungeonSpawnNode targetNode = patrolNodes[currentPatrolNodeIndex];

        if (targetNode != null && targetNode.IsActive)
        {
            agent.speed = patrolSpeed;

            // Получаем позицию нода
            Vector3 targetPosition = targetNode.GetExactPosition();

            // Если нод находится над землёй, находим точку на земле под ним
            RaycastHit groundHit;
            Vector3 searchPosition = targetPosition;

            // Проверяем, есть ли земля под нодом (не первый же коллайдер лута)
            if (Physics.Raycast(targetPosition + Vector3.up * 2f, Vector3.down, out groundHit, 20f,
                    PatrolGroundPhysicsMask, QueryTriggerInteraction.Ignore))
            {
                searchPosition = groundHit.point + Vector3.up * 0.5f; // Немного поднимаем от земли
            }

            // Проверяем, что целевая позиция на NavMesh (расширяем радиус поиска)
            NavMeshHit hit;
            if (NavMesh.SamplePosition(searchPosition, out hit, 15f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);

                if (enableDebugLogs)
                {
                    Debug.Log($"👹 DungeonMonster: Движусь к ноду {currentPatrolNodeIndex} (позиция на NavMesh: {hit.position})");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"⚠ DungeonMonster: Нода {currentPatrolNodeIndex} не на NavMesh (позиция нода: {targetPosition}, поиск от: {searchPosition}), пропускаю");
            }
        }
    }

    /// <summary>
    /// Зрение: дистанция, FOV и луч до игрока (не сквозь стену).
    /// </summary>
    private bool HasPlayerVisualContact(out RaycastHit hit)
    {
        hit = default;
        if (playerTransform == null)
            return false;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > detectionRange)
            return false;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > fieldOfViewAngle / 2f)
            return false;

        Vector3 rayStart = transform.position + Vector3.up * 1f;
        if (!Physics.Raycast(rayStart, directionToPlayer, out hit, detectionRange))
            return false;

        PlayerController player = hit.collider.GetComponent<PlayerController>();
        return player != null || hit.collider.transform == playerTransform ||
               hit.collider.transform.IsChildOf(playerTransform);
    }

    /// <summary>
    /// Проверка обнаружения игрока
    /// </summary>
    private void CheckForPlayer()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (hearingDetectionRange > 0.01f && distanceToPlayer <= hearingDetectionRange)
        {
            lastKnownPlayerPosition = playerTransform.position;
            lastTimePerceivedPlayer = Time.time;
            StartChasing();
            return;
        }

        if (HasPlayerVisualContact(out _))
        {
            lastKnownPlayerPosition = playerTransform.position;
            lastTimePerceivedPlayer = Time.time;
            StartChasing();
        }
    }

    /// <summary>
    /// Начало преследования игрока
    /// </summary>
    private void StartChasing()
    {
        if (currentState == MonsterState.Chasing) return;

        currentState = MonsterState.Chasing;
        isChasing = true;
        if (playerTransform != null)
        {
            lastKnownPlayerPosition = playerTransform.position;
            lastTimePerceivedPlayer = Time.time;
        }

        agent.speed = chaseSpeed * (Time.time < enragedUntil ? hitEnrageSpeedMultiplier : 1f);
        agent.stoppingDistance = attackRange;

        if (enableDebugLogs)
        {
            Debug.Log("👹 DungeonMonster: Обнаружил игрока! Начинаю преследование!");
        }
    }

    /// <summary>
    /// Обновление преследования
    /// </summary>
    private void UpdateChasing()
    {
        if (playerTransform == null)
        {
            ReturnToPatrolling();
            return;
        }

        if (!agent.isOnNavMesh)
        {
            TryPlaceOnNavMesh();
            if (!agent.isOnNavMesh)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("⚠ DungeonMonster: Агент не на NavMesh, возвращаюсь к патрулированию");
                ReturnToPatrolling();
                return;
            }

            if (enableDebugLogs)
                Debug.Log("👹 DungeonMonster: Агент восстановлен на NavMesh во время преследования");
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > chaseRange)
        {
            ReturnToPatrolling();
            return;
        }

        bool hear = hearingDetectionRange > 0.01f && distanceToPlayer <= hearingDetectionRange;
        bool sight = HasPlayerVisualContact(out _);

        if (sight || hear)
        {
            lastTimePerceivedPlayer = Time.time;
            lastKnownPlayerPosition = playerTransform.position;
        }
        else if (useLastKnownPositionWhenLosBlocked)
        {
            if (Time.time - lastTimePerceivedPlayer > loseTargetAfterSecondsWithoutPerception)
            {
                ReturnToPatrolling();
                return;
            }
        }
        else
        {
            ReturnToPatrolling();
            return;
        }

        Vector3 moveGoal = sight || hear ? playerTransform.position : lastKnownPlayerPosition;

        NavMeshHit playerHit;
        if (NavMesh.SamplePosition(moveGoal, out playerHit, 10f, NavMesh.AllAreas))
        {
            if (agent.destination != playerHit.position)
                agent.SetDestination(playerHit.position);
        }
        else if (agent.destination != moveGoal)
        {
            agent.SetDestination(moveGoal);
        }

        float spd = chaseSpeed * (Time.time < enragedUntil ? hitEnrageSpeedMultiplier : 1f);
        agent.speed = spd;
        agent.stoppingDistance = attackRange;

        if (distanceToPlayer <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            AttackPlayer();
            lastAttackTime = Time.time;
        }
    }

    /// <summary>
    /// Атака игрока
    /// </summary>
    private void AttackPlayer()
    {
        if (playerTransform == null) return;

        // Находим PlayerController для нанесения урона
        PlayerController player = playerTransform.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage(damage);

            if (enableDebugLogs)
            {
                Debug.Log($"👹 DungeonMonster атаковал игрока на {damage} урона!");
            }
        }
    }

    /// <summary>
    /// Возврат к патрулированию
    /// </summary>
    private void ReturnToPatrolling()
    {
        currentState = MonsterState.Patrolling;
        isChasing = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = nodeReachDistance;

        // Возобновляем патрулирование в зависимости от режима
        if (patrolMode == PatrolMode.UseNodes && patrolNodes.Count > 0)
        {
            MoveToNextNode();
        }
        else if (patrolMode == PatrolMode.WanderArea && wanderPoints.Count > 0)
        {
            MoveToNextWanderPoint();
        }

        if (enableDebugLogs)
        {
            Debug.Log("👹 DungeonMonster: Игрок потерян. Возвращаюсь к патрулированию.");
        }
    }

    public override void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        base.TakeDamage(damageAmount);

        if (hitEnrageDuration > 0f && hitEnrageSpeedMultiplier > 1f)
            enragedUntil = Time.time + hitEnrageDuration;

        if (currentState == MonsterState.Chasing && agent != null && agent.isOnNavMesh)
            agent.speed = chaseSpeed * (Time.time < enragedUntil ? hitEnrageSpeedMultiplier : 1f);

        // Показываем числа урона
        if (showDamageNumbers)
        {
            ShowDamageNumber(damageAmount);
        }

        // Эффект при получении урона
        StartCoroutine(DamageEffect());
    }

    /// <summary>
    /// Показ числа урона
    /// </summary>
    private void ShowDamageNumber(float damage)
    {
        Vector3 spawnPosition = transform.position + Vector3.up * damageTextHeight;

        if (enableDebugLogs)
        {
            Debug.Log($"💥 Создаем текст урона для монстра: {damage} в позиции {spawnPosition}");
        }

        // Используем простую систему отображения урона
        GameObject damageTextObj = SimpleDamageText.CreateDamageText(spawnPosition, damage);

        if (damageTextObj != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"✅ Текст урона для монстра создан: {damageTextObj.name}");
            }
        }
        else
        {
            Debug.LogError("❌ Не удалось создать текст урона для монстра!");
        }
    }

    /// <summary>
    /// Эффект при получении урона
    /// </summary>
    private System.Collections.IEnumerator DamageEffect()
    {
        // Мигание красным
        if (renderers != null && renderers.Length > 0)
        {
            Color[] originalColors = new Color[renderers.Length];

            // Сохраняем оригинальные цвета и устанавливаем красный
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                {
                    originalColors[i] = renderers[i].material.color;
                    renderers[i].material.color = Color.red;
                }
            }

            yield return new WaitForSeconds(0.1f);

            // Восстанавливаем оригинальные цвета
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                {
                    renderers[i].material.color = originalColors[i];
                }
            }
        }
    }

    protected override void OnDeath()
    {
        currentState = MonsterState.Dead;
        deathTime = Time.time;
        isRespawning = false;

        if (enableDebugLogs)
        {
            Debug.Log($"💀💀💀 DungeonMonster УМЕР! GameObject: {gameObject.name}, Позиция: {transform.position}");
            Debug.Log($"💀 DungeonMonster: Установлено состояние Dead. Время смерти: {deathTime}, Респавн через {respawnTime} секунд");
        }

        // Останавливаем агента
        if (agent != null)
        {
            agent.isStopped = true;
        }

        // Скрываем монстра
        SetVisibility(false);

        if (enableDebugLogs)
        {
            Debug.Log($"💀 DungeonMonster умер! Респавн через {respawnTime} секунд.");
        }
    }

    /// <summary>
    /// Проверка респавна
    /// </summary>
    private void CheckRespawn()
    {
        float timeSinceDeath = Time.time - deathTime;
        bool timeCondition = timeSinceDeath >= respawnTime;

        if (isDead && timeCondition && !isRespawning)
        {
            if (enableDebugLogs)
                Debug.Log("👹 DungeonMonster: условия респавна выполнены, запуск Respawn().");
            Respawn();
        }
    }

    /// <summary>
    /// Респавн монстра
    /// </summary>
    private void Respawn()
    {
        if (enableDebugLogs)
        {
            Debug.Log($"👹 DungeonMonster: НАЧАЛО РЕСПАВНА! Позиция: {transform.position}, isDead: {isDead}");
        }

        isRespawning = true;

        // Восстанавливаем здоровье
        currentHealth = maxHealth;
        isDead = false;

        if (enableDebugLogs)
            Debug.Log($"👹 DungeonMonster: Здоровье восстановлено: {currentHealth}/{maxHealth}, isDead: {isDead}");

        // Убеждаемся, что GameObject активен перед поиском компонентов
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // Активируем все дочерние объекты перед поиском компонентов
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
            }
        }

        // Переинициализируем renderers и colliders (могут быть null после смерти)
        // includeInactive = true, чтобы найти renderers даже в неактивных дочерних объектах
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        if (enableDebugLogs)
        {
            Debug.Log($"👹 DungeonMonster: Респавн. Renderers: {renderers?.Length ?? 0}, Colliders: {colliders?.Length ?? 0}");

            // Детальная информация о найденных компонентах
            if (renderers != null && renderers.Length > 0)
            {
                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        Debug.Log($"  - Renderer: {r.GetType().Name} на {r.gameObject.name}, enabled: {r.enabled}, active: {r.gameObject.activeSelf}");
                    }
                }
            }

            if (colliders != null && colliders.Length > 0)
            {
                foreach (var c in colliders)
                {
                    if (c != null)
                    {
                        Debug.Log($"  - Collider: {c.GetType().Name} на {c.gameObject.name}, enabled: {c.enabled}, active: {c.gameObject.activeSelf}");
                        if (c is MeshCollider mc)
                        {
                            Debug.Log($"    MeshCollider mesh: {(mc.sharedMesh != null ? mc.sharedMesh.name : "NULL")}");
                        }
                    }
                }
            }
        }

        // Восстанавливаем агента ПЕРЕД установкой позиции
        if (agent != null)
        {
            // Включаем агента, если он был отключен
            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            // Останавливаем агента перед перемещением
            agent.isStopped = true;
            agent.ResetPath();

            // Проверяем, что позиция спавна на NavMesh и размещаем агента правильно
            NavMeshHit hit;
            Vector3 respawnPos = spawnPosition;

            if (NavMesh.SamplePosition(spawnPosition, out hit, 5f, NavMesh.AllAreas))
            {
                respawnPos = hit.position;
            }
            else
            {
                // Если позиция спавна не на NavMesh, пытаемся найти ближайшую точку
                if (NavMesh.FindClosestEdge(spawnPosition, out hit, NavMesh.AllAreas))
                {
                    respawnPos = hit.position;
                }

                if (enableDebugLogs)
                {
                    Debug.LogWarning($"⚠ DungeonMonster: Позиция спавна не на NavMesh, используется ближайшая точка: {respawnPos}");
                }
            }

            // Используем Warp для правильного размещения на NavMesh
            agent.Warp(respawnPos);
            transform.position = respawnPos;

            // Ждем один кадр, чтобы агент правильно разместился на NavMesh
            // Это важно для правильной работы NavMeshAgent
        }
        else
        {
            // Если агента нет, просто устанавливаем позицию
            transform.position = spawnPosition;
        }

        // Показываем монстра ПОСЛЕ правильного размещения
        SetVisibility(true);

        // Восстанавливаем настройки агента
        if (agent != null)
        {
            // Проверяем, что агент на NavMesh перед началом движения
            if (!agent.isOnNavMesh)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"⚠ DungeonMonster: Агент не на NavMesh после респавна! Пытаемся восстановить...");
                }
                TryPlaceOnNavMesh();
            }

            // Начинаем движение только если агент на NavMesh
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                agent.stoppingDistance = nodeReachDistance;

                // Возвращаемся к патрулированию
                currentState = MonsterState.Patrolling;
                isChasing = false;
                isWaiting = false;
                waitTimer = 0f;

                // Начинаем патрулирование в зависимости от режима
                if (patrolMode == PatrolMode.UseNodes && patrolNodes.Count > 0)
                {
                    currentPatrolNodeIndex = 0;
                    MoveToNextNode();
                }
                else if (patrolMode == PatrolMode.WanderArea)
                {
                    if (wanderPoints.Count == 0)
                    {
                        GenerateWanderPoints();
                    }
                    if (wanderPoints.Count > 0)
                    {
                        currentWanderPointIndex = 0;
                        MoveToNextWanderPoint();
                    }
                }
            }
            else
            {
                // Если агент не на NavMesh, останавливаем его
                agent.isStopped = true;
                if (enableDebugLogs)
                {
                    Debug.LogError($"❌ DungeonMonster: Не удалось разместить агента на NavMesh! Монстр остановлен.");
                }
            }
        }

        isRespawning = false;

        if (enableDebugLogs)
        {
            Debug.Log($"👹 DungeonMonster респавнился на позиции {transform.position}! Видим: {IsVisible()}, Агент на NavMesh: {agent?.isOnNavMesh ?? false}");
        }
    }

    /// <summary>
    /// Проверка видимости монстра
    /// </summary>
    private bool IsVisible()
    {
        if (renderers == null || renderers.Length == 0) return false;
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.enabled) return true;
        }
        return false;
    }

    /// <summary>
    /// Установка видимости монстра
    /// </summary>
    private void SetVisibility(bool visible)
    {
        // Убеждаемся, что GameObject активен
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // Переинициализируем renderers и colliders на случай, если они изменились
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        if (renderers != null)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;

                    // Также активируем GameObject renderer'а, если он в дочернем объекте
                    if (renderer.gameObject != gameObject && !renderer.gameObject.activeSelf)
                    {
                        renderer.gameObject.SetActive(visible);
                    }
                }
            }
        }

        if (colliders != null)
        {
            foreach (var collider in colliders)
            {
                if (collider != null)
                {
                    // Для MeshCollider нужно убедиться, что mesh назначен
                    if (collider is MeshCollider meshCollider)
                    {
                        if (meshCollider.sharedMesh == null && visible)
                        {
                            if (enableDebugLogs)
                            {
                                Debug.LogWarning($"⚠ DungeonMonster: MeshCollider на {collider.gameObject.name} не имеет mesh!");
                            }
                        }
                        else
                        {
                            meshCollider.enabled = visible;
                        }
                    }
                    else
                    {
                        collider.enabled = visible;
                    }

                    // Также активируем GameObject collider'а, если он в дочернем объекте
                    if (collider.gameObject != gameObject && !collider.gameObject.activeSelf)
                    {
                        collider.gameObject.SetActive(visible);
                    }
                }
            }
        }

        // Дополнительная проверка: убеждаемся, что все дочерние объекты активны
        if (visible)
        {
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"👹 DungeonMonster: Активирован дочерний объект {child.name}");
                    }
                }
            }
        }

        // НЕ отключаем агента при скрытии - это может вызвать проблемы с NavMesh
        // Вместо этого останавливаем его через isStopped
        if (agent != null)
        {
            if (visible)
            {
                // При показе убеждаемся, что агент включен
                if (!agent.enabled)
                {
                    agent.enabled = true;
                }
            }
            // При скрытии не отключаем агент, только останавливаем
            // agent.enabled остается true, чтобы сохранить состояние NavMesh
        }

        if (enableDebugLogs)
        {
            int enabledRenderers = 0;
            int enabledColliders = 0;

            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null && r.enabled) enabledRenderers++;
                }
            }

            if (colliders != null)
            {
                foreach (var c in colliders)
                {
                    if (c != null && c.enabled) enabledColliders++;
                }
            }

            Debug.Log($"👹 DungeonMonster: SetVisibility({visible}). " +
                     $"Renderers: {renderers?.Length ?? 0} (включено: {enabledRenderers}), " +
                     $"Colliders: {colliders?.Length ?? 0} (включено: {enabledColliders}), " +
                     $"Active: {gameObject.activeSelf}, Visible: {IsVisible()}");
        }
    }

    /// <summary>
    /// Установка времени респавна
    /// </summary>
    public void SetRespawnTime(float time)
    {
        respawnTime = time;
    }

    /// <summary>
    /// Получение текущего состояния
    /// </summary>
    public string GetCurrentState()
    {
        return currentState.ToString();
    }

    /// <summary>
    /// Визуализация в редакторе
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Рисуем радиус обнаружения
        Gizmos.color = currentState == MonsterState.Chasing ? chaseGizmoColor : patrolGizmoColor;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (hearingDetectionRange > 0.01f)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, hearingDetectionRange);
        }

        // Рисуем направление взгляда
        Vector3 viewDirection = transform.forward * detectionRange;
        Gizmos.DrawRay(transform.position + Vector3.up * 1f, viewDirection);

        // Рисуем поле зрения
        float halfAngle = fieldOfViewAngle / 2f;
        Vector3 leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, halfAngle, 0) * transform.forward * detectionRange;
        Gizmos.DrawRay(transform.position + Vector3.up * 1f, leftBoundary);
        Gizmos.DrawRay(transform.position + Vector3.up * 1f, rightBoundary);

        // Рисуем линию к игроку, если он обнаружен
        if (playerTransform != null && currentState == MonsterState.Chasing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 1f, playerTransform.position + Vector3.up * 1f);
        }

        // Рисуем позицию спавна
        if (spawnPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnPosition, 0.5f);
            Gizmos.DrawLine(transform.position, spawnPosition);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Рисуем радиус преследования
        Gizmos.color = new Color(chaseGizmoColor.r, chaseGizmoColor.g, chaseGizmoColor.b, 0.3f);
        Gizmos.DrawWireSphere(transform.position, chaseRange);
    }
}

