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
    [SerializeField] private float spawnRadius = 15f; // Радиус спавна вокруг центра данжа

    [Header("Spawn Areas")]
    [SerializeField] private Transform dungeonCenter; // Центр данжа
    [SerializeField] private LayerMask groundLayer = 1; // Слой земли для размещения предметов
    [SerializeField] private float groundCheckDistance = 10f; // Расстояние для поиска земли

    [Header("Item Types")]
    [SerializeField] private ItemSpawnConfig[] spawnConfigs; // Конфигурация типов предметов

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = Color.green;

    private ItemFactory itemFactory;
    private DungeonItemManager itemManager;
    private List<GameObject> spawnedItems = new List<GameObject>();
    private Coroutine spawnCoroutine;

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

        if (spawnOnStart)
        {
            StartSpawning();
        }

        Debug.Log($"🏰 DungeonItemSpawner: Инициализирован. Центр: {dungeonCenter.position}, Радиус: {spawnRadius}");
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
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

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
        Debug.Log($"🏰 DungeonItemSpawner: Начинаем спавн предметов. Текущее количество: {spawnedItems.Count}");

        foreach (var config in spawnConfigs)
        {
            if (Random.value <= config.spawnChance)
            {
                int count = Random.Range(config.minCount, config.maxCount + 1);

                for (int i = 0; i < count; i++)
                {
                    Vector3 spawnPosition = GetRandomSpawnPosition();
                    if (spawnPosition != Vector3.zero)
                    {
                        SpawnItem(config, spawnPosition);
                    }
                }
            }
        }

        Debug.Log($"🏰 DungeonItemSpawner: Спавн завершен. Общее количество предметов: {spawnedItems.Count}");
    }

    private Vector3 GetRandomSpawnPosition()
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

    private void SpawnItem(ItemSpawnConfig config, Vector3 position)
    {
        ItemSpawnData spawnData = new ItemSpawnData
        {
            itemType = config.itemType,
            position = position,
            rotation = Quaternion.Euler(0, Random.Range(0, 360), 0),
            itemName = $"Dungeon {config.itemType}",
            price = Random.Range(5, 50),
            description = $"Предмет из данжа типа {config.itemType}",
            statType = StatType.Health,
            statValue = Random.Range(config.minValue, config.maxValue)
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

            Debug.Log($"🏰 DungeonItemSpawner: Создан предмет {item.name} в позиции {position}");
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

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = gizmoColor;

        // Рисуем радиус спавна
        Vector3 center = dungeonCenter != null ? dungeonCenter.position : transform.position;
        Gizmos.DrawWireSphere(center, spawnRadius);

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

        GUILayout.BeginArea(new Rect(10, 500, 300, 150));
        GUILayout.Label("=== DUNGEON ITEM SPAWNER ===");
        GUILayout.Label($"Items in Dungeon: {spawnedItems.Count}/{maxItemsInDungeon}");
        GUILayout.Label($"Spawn Interval: {spawnInterval}s");
        GUILayout.Label($"Spawn Radius: {spawnRadius}");

        if (GUILayout.Button("Force Spawn Items"))
        {
            ForceSpawnItems();
        }

        if (GUILayout.Button("Clear All Items"))
        {
            ClearAllItems();
        }

        GUILayout.EndArea();
    }
}
