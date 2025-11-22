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
                // Логируем каждую секунду для отладки
                if (Time.frameCount % 60 == 0)
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
        Debug.Log($"💀💀💀 DungeonMonster УМЕР! GameObject: {gameObject.name}, Позиция: {transform.position}");

        currentState = MonsterState.Dead;
        deathTime = Time.time;
        isRespawning = false;

        Debug.Log($"💀 DungeonMonster: Установлено состояние Dead. Время смерти: {deathTime}, Респавн через {respawnTime} секунд");

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

        Debug.Log($"👹 DungeonMonster: CheckRespawn вызван. isDead: {isDead}, Время смерти: {deathTime:F2}, Текущее: {Time.time:F2}, Прошло: {timeSinceDeath:F2}с, Нужно: {respawnTime}с, Условие времени: {timeCondition}, isRespawning: {isRespawning}");

        if (isDead && timeCondition && !isRespawning)
        {
            Debug.Log($"👹 DungeonMonster: УСЛОВИЯ ВЫПОЛНЕНЫ! Запускаю респавн!");
            Respawn();
        }
        else
        {
            if (!isDead)
                Debug.Log($"  ❌ isDead = false");
            if (!timeCondition)
                Debug.Log($"  ❌ Время не прошло: {timeSinceDeath:F2} < {respawnTime}");
            if (isRespawning)
                Debug.Log($"  ❌ Уже респавнится");
        }
    }

    /// <summary>
    /// Респавн монстра
    /// </summary>
    private void Respawn()
    {
        Debug.Log($"👹 DungeonMonster: НАЧАЛО РЕСПАВНА! Позиция: {transform.position}, isDead: {isDead}");

        isRespawning = true;

        // Восстанавливаем здоровье
        currentHealth = maxHealth;
        isDead = false;

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

