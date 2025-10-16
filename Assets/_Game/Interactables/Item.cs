using UnityEngine;

// Базовый класс для всех предметов
public class Item : MonoBehaviour
{
    [Header("Basic Item Properties")]
    public Sprite icon; // иконка для инвентаря
    public int price = 10; // базовая цена предмета в игровой валюте
    public string itemName = "Item";
    [TextArea(3, 5)]
    public string description = "Item description";

    [Header("Item Type")]
    public ItemType itemType = ItemType.SellableItem;

    private void Start()
    {
        // Устанавливаем базовые значения если не заданы
        if (string.IsNullOrEmpty(itemName))
            itemName = gameObject.name;
    }
}

// Отдельный компонент для предметов с бонусами к статам
public class BuffItem : MonoBehaviour
{
    [Header("Buff Properties")]
    public StatType statType;
    public float statValue;
    public bool isPermanent = true; // постоянный или временный бонус
    public float duration = 0f; // длительность в секундах (0 = постоянный)

    public void ApplyBuff(PlayerStats playerStats)
    {
        playerStats.AddStatModifier(statType, statValue);
        Debug.Log($"✅ Применен бонус: {statType} +{statValue}");
    }
}

// Отдельный компонент для оружия
public class Weapon : MonoBehaviour
{
    [Header("Weapon Properties")]
    public float damage = 20f;
    public float attackSpeed = 1f;
    public float range = 2f;
    public bool isRanged = false;
    public GameObject projectilePrefab; // для дальнобойного оружия

    private float appliedDamageBonus = 0f;

    public void ApplyWeaponStats(PlayerStats playerStats)
    {
        // Убираем предыдущий бонус если он был
        if (appliedDamageBonus > 0)
        {
            playerStats.RemoveStatModifier(StatType.Damage, appliedDamageBonus);
        }

        // Применяем новый бонус
        playerStats.AddStatModifier(StatType.Damage, damage);
        appliedDamageBonus = damage;
        Debug.Log($"✅ Экипировано оружие: {gameObject.name} (урон: +{damage})");
    }

    public void RemoveWeaponStats(PlayerStats playerStats)
    {
        if (appliedDamageBonus > 0)
        {
            playerStats.RemoveStatModifier(StatType.Damage, appliedDamageBonus);
            appliedDamageBonus = 0f;
            Debug.Log($"⚔ Снято оружие: {gameObject.name}");
        }
    }
}

// Отдельный компонент для предметов для продажи
public class SellableItem : MonoBehaviour
{
    [Header("Sellable Item Properties")]
    public bool canBeSold = true;
}

// Enum для типов предметов
public enum ItemType
{
    SellableItem,
    BuffItem,
    Weapon
}