using UnityEngine;

/// <summary>
/// Цвет лута-зелья/похлёбки по типу стата — одинаково для данжа и лавки.
/// </summary>
public static class BuffLootVisuals
{
    public static Color GetTintColor(StatType statType)
    {
        switch (statType)
        {
            case StatType.Speed:
                return Color.blue;
            case StatType.JumpHeight:
                return Color.green;
            case StatType.Damage:
                return Color.red;
            case StatType.Health:
                return Color.yellow;
            case StatType.Gravity:
                return Color.magenta;
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Красит все рендеры в иерархии (инстанс материала через .material).
    /// </summary>
    public static void ApplyTintToRenderers(GameObject root, StatType statType)
    {
        if (root == null)
            return;
        Color c = GetTintColor(statType);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;
            r.material.color = c;
        }
    }
}
