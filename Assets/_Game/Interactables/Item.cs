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
        // Сохраняем старые значения для сравнения
        float oldSpeed = playerStats.currentSpeed;
        float oldJump = playerStats.currentJumpHeight;
        float oldDamage = playerStats.currentDamage;
        float oldHealth = playerStats.currentHealth;

        playerStats.AddStatModifier(statType, statValue);

        // Логируем изменения
        Debug.Log($"🧪 Применен бонус: {statType} +{statValue}");
        Debug.Log($"📊 Статы до: Speed={oldSpeed:F1}, Jump={oldJump:F1}, Damage={oldDamage:F1}, Health={oldHealth:F1}");
        Debug.Log($"📊 Статы после: Speed={playerStats.currentSpeed:F1}, Jump={playerStats.currentJumpHeight:F1}, Damage={playerStats.currentDamage:F1}, Health={playerStats.currentHealth:F1}");

        // Показываем конкретное изменение
        switch (statType)
        {
            case StatType.Speed:
                Debug.Log($"🏃 Скорость: {oldSpeed:F1} → {playerStats.currentSpeed:F1} (+{playerStats.currentSpeed - oldSpeed:F1})");
                break;
            case StatType.JumpHeight:
                Debug.Log($"🦘 Прыжок: {oldJump:F1} → {playerStats.currentJumpHeight:F1} (+{playerStats.currentJumpHeight - oldJump:F1})");
                break;
            case StatType.Damage:
                Debug.Log($"⚔ Урон: {oldDamage:F1} → {playerStats.currentDamage:F1} (+{playerStats.currentDamage - oldDamage:F1})");
                break;
            case StatType.Health:
                Debug.Log($"❤ Здоровье: {oldHealth:F1} → {playerStats.currentHealth:F1} (+{playerStats.currentHealth - oldHealth:F1})");
                break;
        }
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