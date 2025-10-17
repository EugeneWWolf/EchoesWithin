using UnityEngine;

/// <summary>
/// Данные для спавна предмета
/// </summary>
[System.Serializable]
public struct ItemSpawnData
{
    [Header("Item Type")]
    public ItemType itemType;

    [Header("Position & Rotation")]
    public Vector3 position;
    public Quaternion rotation;

    [Header("Item Properties")]
    public string itemName;
    public int price;
    public string description;

    [Header("Stat Properties")]
    public StatType statType;
    public float statValue;

    /// <summary>
    /// Создает данные для спавна предмета с базовыми значениями
    /// </summary>
    public static ItemSpawnData Create(ItemType type, Vector3 pos)
    {
        return new ItemSpawnData
        {
            itemType = type,
            position = pos,
            rotation = Quaternion.identity,
            itemName = $"Dungeon {type}",
            price = Random.Range(5, 50),
            description = $"Предмет из данжа типа {type}",
            statType = StatType.Health,
            statValue = Random.Range(1f, 5f)
        };
    }

    /// <summary>
    /// Создает данные для спавна случайного предмета
    /// </summary>
    public static ItemSpawnData CreateRandom(Vector3 pos)
    {
        ItemType[] types = { ItemType.SellableItem, ItemType.BuffItem, ItemType.Weapon };
        ItemType randomType = types[Random.Range(0, types.Length)];

        return new ItemSpawnData
        {
            itemType = randomType,
            position = pos,
            rotation = Quaternion.identity,
            itemName = $"Random Dungeon Item",
            price = Random.Range(10, 100),
            description = $"Случайный предмет из данжа",
            statType = (StatType)Random.Range(0, System.Enum.GetValues(typeof(StatType)).Length),
            statValue = Random.Range(1f, 10f)
        };
    }
}
