using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Менеджер для управления системой предметов в данже
/// Координирует работу ItemFactory и DungeonItemSpawner
/// </summary>
public class DungeonItemManager : MonoBehaviour
{
    [Header("Manager Settings")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool enableDebugUI = true;

    [Header("References")]
    [SerializeField] private ItemFactory itemFactory;
    [SerializeField] private DungeonItemSpawner[] dungeonSpawners;

    [Header("Global Settings")]
    [SerializeField] private int globalMaxItems = 50; // Общий лимит предметов во всех данжах
    [SerializeField] private float cleanupInterval = 60f; // Интервал очистки старых предметов

    private List<GameObject> allDungeonItems = new List<GameObject>();
    private float lastCleanupTime;

    private void Start()
    {
        if (initializeOnStart)
        {
            InitializeManager();
        }
    }

    private void Update()
    {
        // Периодическая очистка старых предметов
        if (Time.time - lastCleanupTime > cleanupInterval)
        {
            CleanupOldItems();
            lastCleanupTime = Time.time;
        }
    }

    /// <summary>
    /// Инициализирует менеджер и все связанные системы
    /// </summary>
    public void InitializeManager()
    {
        Debug.Log("🏰 DungeonItemManager: Инициализация менеджера предметов данжа");

        // Находим ItemFactory если не назначен
        if (itemFactory == null)
        {
            itemFactory = FindObjectOfType<ItemFactory>();
            if (itemFactory == null)
            {
                Debug.LogError("❌ DungeonItemManager: ItemFactory не найден!");
                return;
            }
        }

        // Находим все DungeonItemSpawner если не назначены
        if (dungeonSpawners == null || dungeonSpawners.Length == 0)
        {
            dungeonSpawners = FindObjectsOfType<DungeonItemSpawner>();
        }

        // Запускаем все спавнеры
        foreach (var spawner in dungeonSpawners)
        {
            if (spawner != null)
            {
                spawner.StartSpawning();
            }
        }

        Debug.Log($"🏰 DungeonItemManager: Инициализирован. Спавнеров: {dungeonSpawners.Length}");
    }

    /// <summary>
    /// Регистрирует предмет как предмет данжа
    /// </summary>
    public void RegisterDungeonItem(GameObject item)
    {
        if (item != null && !allDungeonItems.Contains(item))
        {
            allDungeonItems.Add(item);
            Debug.Log($"🏰 DungeonItemManager: Зарегистрирован предмет {item.name}");
        }
    }

    /// <summary>
    /// Удаляет предмет из регистра
    /// </summary>
    public void UnregisterDungeonItem(GameObject item)
    {
        if (allDungeonItems.Contains(item))
        {
            allDungeonItems.Remove(item);
            Debug.Log($"🏰 DungeonItemManager: Удален предмет {item.name}");
        }
    }

    /// <summary>
    /// Очищает старые предметы для освобождения памяти
    /// </summary>
    private void CleanupOldItems()
    {
        int cleanedCount = 0;

        for (int i = allDungeonItems.Count - 1; i >= 0; i--)
        {
            if (allDungeonItems[i] == null)
            {
                allDungeonItems.RemoveAt(i);
                cleanedCount++;
            }
        }

        if (cleanedCount > 0)
        {
            Debug.Log($"🏰 DungeonItemManager: Очищено {cleanedCount} уничтоженных предметов");
        }
    }

    /// <summary>
    /// Получает статистику по предметам в данже
    /// </summary>
    public DungeonItemStats GetStats()
    {
        int totalItems = allDungeonItems.Count;
        int activeItems = 0;
        int sellableItems = 0;
        int buffItems = 0;
        int weapons = 0;

        foreach (GameObject item in allDungeonItems)
        {
            if (item != null && item.activeInHierarchy)
            {
                activeItems++;

                if (item.GetComponent<SellableItem>() != null) sellableItems++;
                if (item.GetComponent<BuffItem>() != null) buffItems++;
                if (item.GetComponent<Weapon>() != null) weapons++;
            }
        }

        return new DungeonItemStats
        {
            totalItems = totalItems,
            activeItems = activeItems,
            sellableItems = sellableItems,
            buffItems = buffItems,
            weapons = weapons
        };
    }

    /// <summary>
    /// Очищает все предметы во всех данжах
    /// </summary>
    [ContextMenu("Clear All Dungeon Items")]
    public void ClearAllDungeonItems()
    {
        Debug.Log($"🏰 DungeonItemManager: Очистка всех предметов данжа. Количество: {allDungeonItems.Count}");

        foreach (GameObject item in allDungeonItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }

        allDungeonItems.Clear();

        // Очищаем предметы в каждом спавнере
        foreach (var spawner in dungeonSpawners)
        {
            if (spawner != null)
            {
                spawner.ClearAllItems();
            }
        }

        Debug.Log("🏰 DungeonItemManager: Все предметы данжа очищены");
    }

    /// <summary>
    /// Принудительно спавнит предметы во всех данжах
    /// </summary>
    [ContextMenu("Force Spawn in All Dungeons")]
    public void ForceSpawnInAllDungeons()
    {
        Debug.Log("🏰 DungeonItemManager: Принудительный спавн во всех данжах");

        foreach (var spawner in dungeonSpawners)
        {
            if (spawner != null)
            {
                spawner.ForceSpawnItems();
            }
        }
    }

    private void OnGUI()
    {
        if (!enableDebugUI) return;

        DungeonItemStats stats = GetStats();

        GUILayout.BeginArea(new Rect(320, 10, 300, 200));
        GUILayout.Label("=== DUNGEON ITEM MANAGER ===");
        GUILayout.Label($"Total Items: {stats.totalItems}");
        GUILayout.Label($"Active Items: {stats.activeItems}");
        GUILayout.Label($"Sellable: {stats.sellableItems}");
        GUILayout.Label($"Buff Items: {stats.buffItems}");
        GUILayout.Label($"Weapons: {stats.weapons}");
        GUILayout.Label($"Spawners: {dungeonSpawners.Length}");

        if (GUILayout.Button("Force Spawn All"))
        {
            ForceSpawnInAllDungeons();
        }

        if (GUILayout.Button("Clear All Items"))
        {
            ClearAllDungeonItems();
        }

        GUILayout.EndArea();
    }
}

/// <summary>
/// Статистика предметов в данже
/// </summary>
[System.Serializable]
public struct DungeonItemStats
{
    public int totalItems;
    public int activeItems;
    public int sellableItems;
    public int buffItems;
    public int weapons;
}
