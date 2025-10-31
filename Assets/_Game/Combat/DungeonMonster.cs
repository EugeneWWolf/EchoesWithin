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

    [Header("Respawn Settings")]
    [SerializeField] private float respawnTime = 10f;
    [SerializeField] private Vector3 spawnPosition;

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

    private enum PatrolMode
    {
        UseNodes,      // Патрулирование по нодам (если назначены)
        WanderArea     // Блуждание по случайным точкам в области
    }

    private MonsterState currentState = MonsterState.Patrolling;

    protected override void Start()
    {
        base.Start();

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }

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
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

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
        if (isRespawning) return;

        switch (currentState)
        {
            case MonsterState.Patrolling:
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
                UpdateChasing();
                break;

            case MonsterState.Dead:
                CheckRespawn();
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

        // Также проверяем через raycast
        RaycastHit groundHit;
        if (Physics.Raycast(currentPos + Vector3.up * 10f, Vector3.down, out groundHit, 50f))
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

        // Фильтруем только активные ноды
        patrolNodes.RemoveAll(node => node == null || !node.IsActive);

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
        if (wanderPoints.Count == 0 || !agent.isOnNavMesh) return;

        // Выбираем следующую случайную точку
        currentWanderPointIndex = Random.Range(0, wanderPoints.Count);
        Vector3 targetPoint = wanderPoints[currentWanderPointIndex];

        agent.speed = patrolSpeed;
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

        if (!agent.isOnNavMesh)
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

            // Проверяем, есть ли земля под нодом
            if (Physics.Raycast(targetPosition + Vector3.up * 2f, Vector3.down, out groundHit, 20f))
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
    /// Проверка обнаружения игрока
    /// </summary>
    private void CheckForPlayer()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= detectionRange)
        {
            // Проверяем, виден ли игрок (в пределах угла обзора и нет препятствий)
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

            if (angleToPlayer <= fieldOfViewAngle / 2f)
            {
                // Проверяем, нет ли препятствий между монстром и игроком
                RaycastHit hit;
                Vector3 rayStart = transform.position + Vector3.up * 1f; // Немного выше от земли
                Vector3 rayEnd = playerTransform.position + Vector3.up * 1f;

                if (Physics.Raycast(rayStart, directionToPlayer, out hit, detectionRange))
                {
                    // Проверяем, попал ли луч в игрока
                    PlayerController player = hit.collider.GetComponent<PlayerController>();
                    if (player != null || hit.collider.transform == playerTransform || hit.collider.transform.IsChildOf(playerTransform))
                    {
                        StartChasing();
                        return;
                    }
                }
            }
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
        agent.speed = chaseSpeed;
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
            // Игрок исчез, возвращаемся к патрулированию
            ReturnToPatrolling();
            return;
        }

        // Если агент не на NavMesh, пытаемся восстановить его позицию
        if (!agent.isOnNavMesh)
        {
            // Пытаемся разместить на NavMesh
            TryPlaceOnNavMesh();

            // Если все еще не на NavMesh, возвращаемся к патрулированию
            if (!agent.isOnNavMesh)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning("⚠ DungeonMonster: Агент не на NavMesh, возвращаюсь к патрулированию");
                }
                ReturnToPatrolling();
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log("👹 DungeonMonster: Агент восстановлен на NavMesh во время преследования");
            }
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > chaseRange)
        {
            // Игрок слишком далеко, возвращаемся к патрулированию
            ReturnToPatrolling();
            return;
        }

        // Двигаемся к игроку (проверяем, что позиция игрока на NavMesh)
        Vector3 targetPosition = playerTransform.position;

        // Пытаемся найти ближайшую точку на NavMesh к позиции игрока
        NavMeshHit playerHit;
        if (NavMesh.SamplePosition(targetPosition, out playerHit, 10f, NavMesh.AllAreas))
        {
            // Нашли точку на NavMesh, используем её
            if (agent.destination != playerHit.position)
            {
                agent.SetDestination(playerHit.position);
            }
        }
        else
        {
            // Если позиция игрока не на NavMesh, используем прямую позицию (NavMeshAgent попытается найти путь)
            if (agent.destination != targetPosition)
            {
                agent.SetDestination(targetPosition);
            }
        }

        // Проверяем, можем ли атаковать
        if (distanceToPlayer <= attackRange && Time.time - lastAttackTime >= attackCooldown)
        {
            // Здесь можно добавить атаку, если нужно
            lastAttackTime = Time.time;
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

    protected override void OnDeath()
    {
        currentState = MonsterState.Dead;
        deathTime = Time.time;
        isRespawning = false;

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
        if (Time.time - deathTime >= respawnTime && !isRespawning)
        {
            Respawn();
        }
    }

    /// <summary>
    /// Респавн монстра
    /// </summary>
    private void Respawn()
    {
        isRespawning = true;

        // Восстанавливаем здоровье
        currentHealth = maxHealth;
        isDead = false;

        // Возвращаемся на стартовую позицию
        transform.position = spawnPosition;

        // Показываем монстра
        SetVisibility(true);

        // Восстанавливаем агента
        if (agent != null)
        {
            agent.isStopped = false;

            // Проверяем, что позиция спавна на NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPosition, out hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                transform.position = hit.position;
            }
            else
            {
                // Если позиция спавна не на NavMesh, пытаемся найти ближайшую точку
                agent.Warp(spawnPosition);
                transform.position = spawnPosition;

                if (enableDebugLogs)
                {
                    Debug.LogWarning($"⚠ DungeonMonster: Позиция спавна не на NavMesh");
                }
            }
        }

        // Возвращаемся к патрулированию
        currentState = MonsterState.Patrolling;
        isChasing = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = nodeReachDistance;

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

        isRespawning = false;

        if (enableDebugLogs)
        {
            Debug.Log("👹 DungeonMonster респавнился!");
        }
    }

    /// <summary>
    /// Установка видимости монстра
    /// </summary>
    private void SetVisibility(bool visible)
    {
        if (renderers != null)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        if (colliders != null)
        {
            foreach (var collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = visible;
                }
            }
        }

        // Также управляем компонентом Enemy
        if (agent != null)
        {
            agent.enabled = visible;
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

