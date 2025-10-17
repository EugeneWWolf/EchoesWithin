using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Config/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("Base Stats")]
    public float baseSpeed = 5f;
    public float baseJumpHeight = 2f;
    public float baseGravity = -9.8f;
    public float baseDamage = 10f;
    public float baseHealth = 100f;

    [Header("Current Stats (calculated)")]
    public float currentSpeed;
    public float currentJumpHeight;
    public float currentGravity;
    public float currentDamage;
    public float currentHealth;

    [Header("Stat Modifiers")]
    public float speedModifier = 0f;
    public float jumpModifier = 0f;
    public float gravityModifier = 0f;
    public float damageModifier = 0f;
    public float healthModifier = 0f;

    public void RecalculateStats()
    {
        currentSpeed = Mathf.Max(0.1f, baseSpeed + speedModifier); // Минимум 0.1 для движения
        currentJumpHeight = Mathf.Max(0.1f, baseJumpHeight + jumpModifier); // Минимум 0.1 для прыжка
        currentGravity = Mathf.Max(-50f, baseGravity + gravityModifier); // Максимум -50 для гравитации
        currentDamage = Mathf.Max(0f, baseDamage + damageModifier);
        currentHealth = Mathf.Max(1f, baseHealth + healthModifier); // Минимум 1 HP
    }

    public void AddStatModifier(StatType statType, float value)
    {
        switch (statType)
        {
            case StatType.Speed:
                speedModifier += value;
                break;
            case StatType.JumpHeight:
                jumpModifier += value;
                break;
            case StatType.Gravity:
                gravityModifier += value;
                break;
            case StatType.Damage:
                damageModifier += value;
                break;
            case StatType.Health:
                healthModifier += value;
                break;
        }
        RecalculateStats();
    }

    public void RemoveStatModifier(StatType statType, float value)
    {
        switch (statType)
        {
            case StatType.Speed:
                speedModifier -= value;
                break;
            case StatType.JumpHeight:
                jumpModifier -= value;
                break;
            case StatType.Gravity:
                gravityModifier -= value;
                break;
            case StatType.Damage:
                damageModifier -= value;
                break;
            case StatType.Health:
                healthModifier -= value;
                break;
        }
        RecalculateStats();
    }
}

public enum StatType
{
    Speed,
    JumpHeight,
    Gravity,
    Damage,
    Health
}
