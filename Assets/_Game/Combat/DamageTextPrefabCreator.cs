using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Утилита для создания префаба текста урона
/// </summary>
public class DamageTextPrefabCreator : MonoBehaviour
{
    [ContextMenu("Create Damage Text Prefab")]
    public void CreateDamageTextPrefab()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("DamageTextCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // Настраиваем размер Canvas
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1f, 1f);

        // Создаем Text
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(canvasObj.transform);

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "100";

        // Настраиваем RectTransform
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Добавляем DamageText компонент
        DamageText damageText = canvasObj.AddComponent<DamageText>();

        // Добавляем CanvasGroup для анимации
        CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        Debug.Log("✅ Префаб текста урона создан! Сохраните его как префаб и назначьте в DummyCreator");
    }
}
