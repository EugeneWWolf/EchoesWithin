using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Система спавна предметов в данже
/// Использует ItemFactory для создания предметов
/// </summary>
public class DungeonItemSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float spawnInterval = 30f; // Интервал между спавнами
    [SerializeField] private int maxItemsInDungeon = 20; // Максимальное количество предметов в данже
    [SerializeField] private int initialSpawnCount = 15; // Количество предметов при старте (если не заполняет лимит)
    [SerializeField] private bool fillToMaxOnStart = true; // Заполнять до максимума при старте

    [Header("Spawn Mode")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.UseNodes; // Режим спавна
    [SerializeField] private DungeonNodeGenerator nodeGenerator; // Генератор нодов (необязательно, если ноды расставлены вручную)
    [SerializeField] private Transform nodesParent; // Родительский объект с нодами (если ноды расставлены вручную)

    [Header("Spawn Areas (для Fallback режима)")]
    [SerializeField] private Transform dungeonCenter; // Центр данжа (используется если ноды не найдены)
    [SerializeField] private LayerMask groundLayer = 1; // Слой земли для размещения предметов
    [SerializeField] private float groundCheckDistance = 10f; // Расстояние для поиска земли
    [SerializeField] private float spawnRadius = 15f; // Радиус спавна вокруг центра данжа

    [Header("Item Types")]
    [SerializeField] private ItemSpawnConfig[] spawnConfigs; // Конфигурация типов предметов!

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;
    [SerializeField] private Color gizmoColor = Color.green;

    private ItemFactory itemFactory;
    private DungeonItemManager itemManager;
    private List<GameObject> spawnedItems = new List<GameObject>();
    private Coroutine spawnCoroutine;
    private List<DungeonSpawnNode> availableNodes = new List<DungeonSpawnNode>();
    private HashSet<DungeonSpawnNode> usedNodes = new HashSet<DungeonSpawnNode>(); // Ноды, где уже спавнились предметы

    public enum SpawnMode
    {
        UseNodes,      // Использовать ноды для спавна
        RandomSpawn,   // Случайный спавн (старый метод)
        Hybrid         // Пытаться использовать ноды, если не найдены - случайный спавн
    }

    [System.Serializable]
    public class ItemSpawnConfig
    {
        public ItemType itemType;
        public float spawnChance = 0.3f; // Шанс спавна (0-1)
        public int minCount = 1;
        public int maxCount = 3;
        public float minValue = 1f;
        public float maxValue = 10f;
    }

    private void Start()
    {
        // Находим ItemFactory
        itemFactory = FindObjectOfType<ItemFactory>();
        if (itemFactory == null)
        {
            Debug.LogError("❌ DungeonItemSpawner: ItemFactory не найден!");
            return;
        }

        // Находим DungeonItemManager
        itemManager = FindObjectOfType<DungeonItemManager>();
        if (itemManager == null)
        {
            Debug.LogWarning("⚠ DungeonItemSpawner: DungeonItemManager не найден, предметы не будут зарегистрированы");
        }

        // Устанавливаем центр данжа если не назначен
        if (dungeonCenter == null)
        {
            dungeonCenter = transform;
        }

        // Инициализируем ноды
        InitializeNodes();

        if (spawnOnStart)
        {
            StartSpawning();
        }

        Debug.Log($"🏰 DungeonItemSpawner: Инициализирован. Режим: {spawnMode}, Нодов найдено: {availableNodes.Count}");
    }

    /// <summary>
    /// Начинает процесс спавна предметов
    /// </summary>
    public void StartSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        spawnCoroutine = StartCoroutine(SpawnItemsCoroutine());
        Debug.Log("🏰 DungeonItemSpawner: Начат процесс спавна предметов");
    }

    /// <summary>
    /// Останавливает процесс спавна предметов
    /// </summary>
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        Debug.Log("🏰 DungeonItemSpawner: Процесс спавна остановлен");
    }

    private IEnumerator SpawnItemsCoroutine()
    {
        // Первый спавн сразу при старте - заполняем данж
        Debug.Log("🏰 DungeonItemSpawner: Начинаем начальный спавн предметов");
        CleanupDestroyedItems();

        if (fillToMaxOnStart)
        {
            // Заполняем до максимума
            Debug.Log($"🏰 DungeonItemSpawner: Заполняем данж до максимума ({maxItemsInDungeon} предметов)");
            yield return StartCoroutine(FillDungeonToTarget(maxItemsInDungeon));
        }
        else
        {
            // Спавним начальное количество
            int targetCount = Mathf.Min(initialSpawnCount, maxItemsInDungeon);
            Debug.Log($"🏰 DungeonItemSpawner: Заполняем данж до {targetCount} предметов");
            yield return StartCoroutine(FillDungeonToTarget(targetCount));
        }

        Debug.Log($"🏰 DungeonItemSpawner: Начальный спавн завершен. Предметов в данже: {spawnedItems.Count}/{maxItemsInDungeon}");

        // Затем спавним по интервалу
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            Debug.Log($"🏰 DungeonItemSpawner: Интервал спавна прошел ({spawnInterval}с), проверяем необходимость спавна");

            // Очищаем уничтоженные предметы из списка
            CleanupDestroyedItems();

            // Проверяем, нужно ли спавнить новые предметы
            if (spawnedItems.Count < maxItemsInDungeon)
            {
                SpawnRandomItems();
            }
            else
            {
                Debug.Log($"🏰 DungeonItemSpawner: Достигнуто максимальное количество предметов ({maxItemsInDungeon})");
            }
        }
    }

    /// <summary>
    /// Заполняет данж до целевого количества предметов
    /// </summary>
    private IEnumerator FillDungeonToTarget(int targetCount)
    {
        Debug.Log($"🏰 DungeonItemSpawner: Начинаем заполнение данжа до {targetCount} предметов");

        // Даем время на инициализацию нодов (если они еще не были найдены)
        if (availableNodes.Count == 0 && (spawnMode == SpawnMode.UseNodes || spawnMode == SpawnMode.Hybrid))
        {
            Debug.Log("🏰 DungeonItemSpawner: Ноды не найдены, пытаемся найти их снова...");
            InitializeNodes();
            yield return null; // Даем один кадр на обработку
        }

        int attempts = 0;
        int maxAttempts = targetCount * 20; // Ограничение попыток чтобы избежать бесконечного цикла
        int itemsNeeded = targetCount - spawnedItems.Count;
        int consecutiveFailures = 0; // Счетчик последовательных неудач

        // Очищаем список использованных нодов для начального заполнения
        usedNodes.Clear();

        while (spawnedItems.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            // Очищаем уничтоженные предметы
            CleanupDestroyedItems();

            // Если уже достигли цели, выходим
            if (spawnedItems.Count >= targetCount)
                break;

            itemsNeeded = targetCount - spawnedItems.Count;

            // Спавним предметы всех типов одновременно
            int itemsSpawnedThisIteration = 0;

            foreach (var config in spawnConfigs)
            {
                if (spawnedItems.Count >= targetCount)
                    break;

                // Для начального заполнения спавним больше предметов сразу
                // Распределяем предметы между типами пропорционально
                int itemsForThisType = Mathf.Min(
                    Random.Range(config.minCount, config.maxCount + 1) * 2, // Удваиваем для быстрого заполнения
                    itemsNeeded
                );

                for (int i = 0; i < itemsForThisType; i++)
                {
                    if (spawnedItems.Count >= targetCount)
                        break;

                    Vector3 spawnPosition = GetRandomSpawnPosition();
                    if (spawnPosition != Vector3.zero)
                    {
                        SpawnItem(config, spawnPosition);
                        itemsSpawnedThisIteration++;
                        consecutiveFailures = 0; // Сбрасываем счетчик неудач при успешном спавне
                    }
                    else
                    {
                        // Если не получилось найти позицию, пропускаем
                        // Но не прерываем весь цикл - пробуем следующий тип предмета
                    }
                }
            }

            // Обновляем счетчик последовательных неудач
            if (itemsSpawnedThisIteration == 0)
            {
                consecutiveFailures++;
            }
            else
            {
                consecutiveFailures = 0;
            }

            // Логируем прогресс периодически
            if (attempts % 5 == 0 || spawnedItems.Count >= targetCount)
            {
                Debug.Log($"🏰 DungeonItemSpawner: Прогресс заполнения: {spawnedItems.Count}/{targetCount} ({itemsSpawnedThisIteration} спавнено в этой итерации, попытка {attempts})");
            }

            // Если ничего не спавнилось за несколько попыток подряд, проверяем почему
            // Проверяем только если есть достаточно попыток И несколько последовательных неудач
            if (itemsSpawnedThisIteration == 0 && attempts > 15 && consecutiveFailures >= 10)
            {
                // Проверяем, есть ли доступные ноды/позиции для спавна
                bool hasAvailablePositions = false;

                if (spawnMode == SpawnMode.UseNodes || spawnMode == SpawnMode.Hybrid)
                {
                    // Проверяем, есть ли свободные ноды
                    if (availableNodes.Count > 0)
                    {
                        List<DungeonSpawnNode> freeNodes = availableNodes.FindAll(node =>
                            node != null && node.IsActive && !usedNodes.Contains(node));
                        hasAvailablePositions = freeNodes.Count > 0;

                        if (!hasAvailablePositions && usedNodes.Count > 0)
                        {
                            // Если все ноды использованы, можно очистить список использованных
                            Debug.Log($"🏰 DungeonItemSpawner: Все ноды использованы ({usedNodes.Count}), очищаем для повторного использования");
                            usedNodes.Clear();
                            hasAvailablePositions = availableNodes.Count > 0;
                        }
                    }
                }

                if (spawnMode == SpawnMode.RandomSpawn || spawnMode == SpawnMode.Hybrid)
                {
                    // Для случайного спавна всегда есть возможность (fallback всегда работает)
                    hasAvailablePositions = true;
                }

                // Если нет доступных позиций, выводим предупреждение
                if (!hasAvailablePositions)
                {
                    Debug.LogWarning($"🏰 DungeonItemSpawner: Не удается спавнить предметы. " +
                        $"Заполнено {spawnedItems.Count}/{targetCount} за {attempts} попыток. " +
                        $"Нет доступных нодов для спавна (найдено нодов: {availableNodes.Count}).");
                    break;
                }
                // Если позиции есть, но предметы не спавнятся - возможно, проблема с конфигурацией
                else if (spawnedItems.Count == 0)
                {
                    Debug.LogWarning($"🏰 DungeonItemSpawner: Не удается спавнить предметы. " +
                        $"Заполнено {spawnedItems.Count}/{targetCount} за {attempts} попыток. " +
                        $"Проверьте конфигурацию спавна (spawnConfigs, spawnChance).");
                    break;
                }
                // Если уже что-то спавнилось, не паникуем - продолжаем
            }

            // Небольшая задержка между итерациями (чтобы не заморозить игру)
            if (attempts % 10 == 0)
            {
                yield return null;
            }
        }

        Debug.Log($"🏰 DungeonItemSpawner: Заполнение завершено. Создано {spawnedItems.Count}/{targetCount} предметов за {attempts} попыток");

        if (attempts >= maxAttempts)
        {
            Debug.LogWarning($"🏰 DungeonItemSpawner: Достигнуто максимальное количество попыток ({maxAttempts}). Заполнено {spawnedItems.Count}/{targetCount} предметов");
        }
    }

    private void CleanupDestroyedItems()
    {
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            if (spawnedItems[i] == null)
            {
                spawnedItems.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Спавнит случайные предметы в данже
    /// </summary>
    public void SpawnRandomItems()
    {
        Debug.Log($"🏰 DungeonItemSpawner: Начинаем спавн предметов. Текущее количество: {spawnedItems.Count}/{maxItemsInDungeon}, Доступных нодов: {availableNodes.Count}, Использовано: {usedNodes.Count}");

        if (spawnConfigs == null || spawnConfigs.Length == 0)
        {
            Debug.LogWarning("🏰 DungeonItemSpawner: Нет конфигураций для спавна предметов!");
            return;
        }

        int itemsSpawnedThisCycle = 0;

        foreach (var config in spawnConfigs)
        {
            // Проверяем лимит перед каждым типом предметов
            if (spawnedItems.Count >= maxItemsInDungeon)
            {
                Debug.Log($"🏰 DungeonItemSpawner: Достигнут лимит предметов ({maxItemsInDungeon}), пропускаем {config.itemType}");
                break;
            }

            float roll = Random.value;
            Debug.Log($"🏰 DungeonItemSpawner: Проверка спавна {config.itemType}: roll={roll:F2}, chance={config.spawnChance:F2}");

            if (roll <= config.spawnChance)
            {
                int count = Random.Range(config.minCount, config.maxCount + 1);
                Debug.Log($"🏰 DungeonItemSpawner: Спавним {count} предметов типа {config.itemType}");

                for (int i = 0; i < count; i++)
                {
                    // Проверяем лимит перед каждым предметом
                    if (spawnedItems.Count >= maxItemsInDungeon)
                    {
                        Debug.Log($"🏰 DungeonItemSpawner: Достигнут лимит предметов ({maxItemsInDungeon}) во время спавна {config.itemType}");
                        break;
                    }

                    Vector3 spawnPosition = GetRandomSpawnPosition();
                    if (spawnPosition != Vector3.zero)
                    {
                        SpawnItem(config, spawnPosition);
                        itemsSpawnedThisCycle++;
                    }
                    else
                    {
                        Debug.LogWarning($"🏰 DungeonItemSpawner: Не удалось получить валидную позицию для спавна предмета {config.itemType}");
                    }
                }
            }
            else
            {
                Debug.Log($"🏰 DungeonItemSpawner: {config.itemType} не спавнится (roll {roll:F2} > chance {config.spawnChance:F2})");
            }
        }

        Debug.Log($"🏰 DungeonItemSpawner: Спавн завершен. Создано предметов в этом цикле: {itemsSpawnedThisCycle}, Общее количество: {spawnedItems.Count}/{maxItemsInDungeon}");
    }

    /// <summary>
    /// Инициализирует список доступных нодов для спавна
    /// </summary>
    private void InitializeNodes()
    {
        availableNodes.Clear();
        usedNodes.Clear();

        if (spawnMode == SpawnMode.RandomSpawn)
        {
            Debug.Log("🏰 DungeonItemSpawner: Режим случайного спавна, ноды не используются");
            return;
        }

        // Пытаемся найти ноды через генератор
        if (nodeGenerator != null)
        {
            availableNodes = nodeGenerator.GetAllNodes();
            Debug.Log($"🏰 DungeonItemSpawner: Найдено {availableNodes.Count} нодов через генератор");
        }

        // Если ноды не найдены, ищем в родительском объекте
        if (availableNodes.Count == 0 && nodesParent != null)
        {
            DungeonSpawnNode[] nodes = nodesParent.GetComponentsInChildren<DungeonSpawnNode>();
            availableNodes.AddRange(nodes);
            Debug.Log($"🏰 DungeonItemSpawner: Найдено {availableNodes.Count} нодов в родительском объекте");
        }

        // Если ноды все еще не найдены, ищем родительский объект "DungeonSpawnNodes"
        if (availableNodes.Count == 0)
        {
            GameObject spawnNodesParent = GameObject.Find("DungeonSpawnNodes");
            if (spawnNodesParent != null)
            {
                DungeonSpawnNode[] nodes = spawnNodesParent.GetComponentsInChildren<DungeonSpawnNode>();
                availableNodes.AddRange(nodes);
                Debug.Log($"🏰 DungeonItemSpawner: Найдено {availableNodes.Count} нодов через поиск объекта 'DungeonSpawnNodes'");
            }
        }

        // Если ноды все еще не найдены, ищем по всей сцене
        if (availableNodes.Count == 0)
        {
            DungeonSpawnNode[] allNodes = FindObjectsOfType<DungeonSpawnNode>(true); // Включая неактивные объекты
            availableNodes.AddRange(allNodes);
            Debug.Log($"🏰 DungeonItemSpawner: Найдено {availableNodes.Count} нодов в сцене (включая неактивные)");
        }

        // Сохраняем количество до фильтрации
        int totalNodesBeforeFilter = availableNodes.Count;

        // Фильтруем только активные ноды
        availableNodes.RemoveAll(node => node == null || !node.IsActive);

        int inactiveNodes = totalNodesBeforeFilter - availableNodes.Count;

        Debug.Log($"🏰 DungeonItemSpawner: Инициализировано {availableNodes.Count} активных нодов из {totalNodesBeforeFilter} найденных " +
            $"(неактивных: {inactiveNodes})");

        // Если ноды не найдены, предупреждаем
        if (availableNodes.Count == 0 && spawnMode != SpawnMode.RandomSpawn)
        {
            Debug.LogWarning($"⚠ DungeonItemSpawner: Не найдено активных нодов для спавна! " +
                $"Проверьте:\n" +
                $"1. Назначен ли nodeGenerator или nodesParent?\n" +
                $"2. Существует ли объект 'DungeonSpawnNodes' в сцене?\n" +
                $"3. Есть ли ноды в сцене и активны ли они?\n" +
                $"4. Попробуйте изменить Spawn Mode на 'Hybrid' для fallback на случайный спавн");
        }
    }

    /// <summary>
    /// Получает позицию для спавна в зависимости от режима
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        // Используем ноды если доступны и режим позволяет
        if ((spawnMode == SpawnMode.UseNodes || spawnMode == SpawnMode.Hybrid) && availableNodes.Count > 0)
        {
            return GetPositionFromNode();
        }

        // Fallback к случайному спавну
        if (spawnMode == SpawnMode.RandomSpawn || spawnMode == SpawnMode.Hybrid)
        {
            return GetRandomSpawnPositionFallback();
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Получает позицию из доступного нода
    /// </summary>
    private Vector3 GetPositionFromNode()
    {
        // Получаем список доступных нодов (не использованных и активных)
        List<DungeonSpawnNode> freeNodes = availableNodes.FindAll(node =>
            node != null && node.IsActive && !usedNodes.Contains(node));

        if (freeNodes.Count == 0)
        {
            // Если все ноды использованы, очищаем список использованных (для повторного использования)
            if (usedNodes.Count > 0)
            {
                Debug.Log($"🏰 DungeonItemSpawner: Все ноды использованы ({usedNodes.Count}), очищаем список использованных для повторного использования");
                usedNodes.Clear();
                freeNodes = availableNodes.FindAll(node => node != null && node.IsActive);
            }

            if (freeNodes.Count == 0)
            {
                Debug.LogWarning($"🏰 DungeonItemSpawner: Нет доступных нодов для спавна (всего нодов: {availableNodes.Count}, использовано: {usedNodes.Count})");
                return spawnMode == SpawnMode.Hybrid ? GetRandomSpawnPositionFallback() : Vector3.zero;
            }
        }

        // Выбираем случайный нод
        DungeonSpawnNode selectedNode = freeNodes[Random.Range(0, freeNodes.Count)];
        Vector3 spawnPos = selectedNode.GetSpawnPosition();

        // Добавляем нод в использованные только если позиция валидна
        if (spawnPos != Vector3.zero)
        {
            usedNodes.Add(selectedNode);
            // Логируем только периодически, чтобы не засорять консоль при массовом спавне
            if (usedNodes.Count % 10 == 0 || usedNodes.Count <= 3)
            {
                Debug.Log($"🏰 DungeonItemSpawner: Выбран нод #{usedNodes.Count} в позиции {spawnPos} (свободных нодов: {freeNodes.Count - 1})");
            }
        }
        else
        {
            Debug.LogWarning($"🏰 DungeonItemSpawner: Нод вернул невалидную позицию Vector3.zero");
        }

        return spawnPos;
    }

    /// <summary>
    /// Старый метод случайного спавна (fallback)
    /// </summary>
    private Vector3 GetRandomSpawnPositionFallback()
    {
        Vector3 center = dungeonCenter.position;

        // Генерируем случайную позицию в радиусе
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 randomPosition = center + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Ищем землю под позицией
        RaycastHit hit;
        if (Physics.Raycast(randomPosition + Vector3.up * 5f, Vector3.down, out hit, groundCheckDistance, groundLayer))
        {
            return hit.point + Vector3.up * 0.5f; // Немного поднимаем над землей
        }

        // Если земля не найдена, размещаем на уровне центра данжа
        return new Vector3(randomPosition.x, center.y, randomPosition.z);
    }

    /// <summary>
    /// Обновляет список доступных нодов (вызывается при необходимости)
    /// </summary>
    [ContextMenu("Refresh Nodes")]
    public void RefreshNodes()
    {
        InitializeNodes();
    }

    private void SpawnItem(ItemSpawnConfig config, Vector3 position)
    {
        // Дополнительная проверка лимита перед созданием предмета
        if (spawnedItems.Count >= maxItemsInDungeon)
        {
            Debug.LogWarning($"🏰 DungeonItemSpawner: Попытка создать предмет при достигнутом лимите ({maxItemsInDungeon})");
            return;
        }

        // Генерируем случайное значение для стата
        float randomStatValue = Random.Range(config.minValue, config.maxValue);

        ItemSpawnData spawnData = new ItemSpawnData
        {
            itemType = config.itemType,
            position = position,
            rotation = Quaternion.Euler(0, Random.Range(0, 360), 0),
            itemName = $"Dungeon {config.itemType}",
            price = Mathf.RoundToInt(randomStatValue), // Цена = значение стата
            description = $"Предмет из данжа типа {config.itemType}",
            statType = StatType.Health,
            statValue = randomStatValue
        };

        GameObject item = itemFactory.CreateItem(config.itemType, spawnData);
        if (item != null)
        {
            spawnedItems.Add(item);

            // Регистрируем предмет в менеджере
            if (itemManager != null)
            {
                itemManager.RegisterDungeonItem(item);
            }

            Debug.Log($"🏰 DungeonItemSpawner: Создан предмет {item.name} в позиции {position} (цена: {spawnData.price}, стат: {spawnData.statValue:F1}) (всего: {spawnedItems.Count}/{maxItemsInDungeon})");
        }
    }

    /// <summary>
    /// Очищает все предметы в данже
    /// </summary>
    [ContextMenu("Clear All Items")]
    public void ClearAllItems()
    {
        Debug.Log($"🏰 DungeonItemSpawner: Очистка всех предметов. Количество: {spawnedItems.Count}");

        foreach (GameObject item in spawnedItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }

        spawnedItems.Clear();
        Debug.Log("🏰 DungeonItemSpawner: Все предметы очищены");
    }

    /// <summary>
    /// Принудительно спавнит предметы
    /// </summary>
    [ContextMenu("Force Spawn Items")]
    public void ForceSpawnItems()
    {
        SpawnRandomItems();
    }

    /// <summary>
    /// Получает текущее количество предметов в данже
    /// </summary>
    public int GetCurrentItemCount()
    {
        return spawnedItems.Count;
    }

    /// <summary>
    /// Получает максимальное количество предметов в данже
    /// </summary>
    public int GetMaxItemCount()
    {
        return maxItemsInDungeon;
    }

    /// <summary>
    /// Проверяет, можно ли создать еще предметы
    /// </summary>
    public bool CanSpawnMoreItems()
    {
        return spawnedItems.Count < maxItemsInDungeon;
    }


    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = gizmoColor;

        // Рисуем радиус спавна (только если используется fallback режим)
        if (spawnMode == SpawnMode.RandomSpawn || (spawnMode == SpawnMode.Hybrid && availableNodes.Count == 0))
        {
            Vector3 center = dungeonCenter != null ? dungeonCenter.position : transform.position;
            Gizmos.DrawWireSphere(center, spawnRadius);
        }

        // Рисуем ноды если они используются
        if (availableNodes.Count > 0 && (spawnMode == SpawnMode.UseNodes || spawnMode == SpawnMode.Hybrid))
        {
            Gizmos.color = Color.cyan;
            foreach (var node in availableNodes)
            {
                if (node != null && node.IsActive)
                {
                    if (usedNodes.Contains(node))
                    {
                        Gizmos.color = Color.yellow;
                    }
                    else
                    {
                        Gizmos.color = Color.cyan;
                    }
                    Gizmos.DrawWireSphere(node.transform.position, 0.3f);
                }
            }
        }

        // Рисуем позиции спавненных предметов
        Gizmos.color = Color.red;
        foreach (GameObject item in spawnedItems)
        {
            if (item != null)
            {
                Gizmos.DrawWireCube(item.transform.position, Vector3.one * 0.5f);
            }
        }
    }

    private void OnGUI()
    {
        if (!showDebugGizmos) return;

        GUILayout.BeginArea(new Rect(10, 500, 300, 200));
        GUILayout.Label("=== DUNGEON ITEM SPAWNER ===");
        GUILayout.Label($"Spawn Mode: {spawnMode}");
        GUILayout.Label($"Available Nodes: {availableNodes.Count}");
        GUILayout.Label($"Used Nodes: {usedNodes.Count}");
        GUILayout.Label($"Items in Dungeon: {spawnedItems.Count}/{maxItemsInDungeon}");
        GUILayout.Label($"Can Spawn More: {CanSpawnMoreItems()}");
        GUILayout.Label($"Spawn Interval: {spawnInterval}s");

        // Показываем предупреждение если лимит превышен
        if (spawnedItems.Count > maxItemsInDungeon)
        {
            GUI.color = Color.red;
            GUILayout.Label($"⚠ ПРЕВЫШЕН ЛИМИТ! ({spawnedItems.Count} > {maxItemsInDungeon})");
            GUI.color = Color.white;
        }

        if (GUILayout.Button("Force Spawn Items"))
        {
            ForceSpawnItems();
        }

        if (GUILayout.Button("Clear All Items"))
        {
            ClearAllItems();
        }

        if (GUILayout.Button("Refresh Nodes"))
        {
            RefreshNodes();
        }

        if (GUILayout.Button("Debug Item Count"))
        {
            Debug.Log($"🏰 DungeonItemSpawner: Текущее количество предметов: {spawnedItems.Count}/{maxItemsInDungeon}");
        }

        if (GUILayout.Button("Debug Item Values"))
        {
            Debug.Log("🏰 DungeonItemSpawner: Тестирование значений предметов:");
            foreach (var config in spawnConfigs)
            {
                float testValue = Random.Range(config.minValue, config.maxValue);
                int price = Mathf.RoundToInt(testValue);
                Debug.Log($"  {config.itemType}: statValue={testValue:F1} (min={config.minValue}, max={config.maxValue}) -> price={price}");
            }
        }

        GUILayout.EndArea();
    }
}
