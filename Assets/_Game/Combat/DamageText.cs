using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Компонент для отображения текста урона
/// </summary>
public class DamageText : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private Vector3 moveDirection = Vector3.up;

    private Text damageText;
    private CanvasGroup canvasGroup;
    private float startTime;

    private void Awake()
    {
        damageText = GetComponent<Text>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        startTime = Time.time;
    }

    private void Start()
    {
        StartCoroutine(AnimateText());
    }

    /// <summary>
    /// Устанавливает текст урона
    /// </summary>
    public void SetDamage(float damage)
    {
        if (damageText != null)
        {
            damageText.text = damage.ToString("F0");

            // Цвет в зависимости от урона
            if (damage >= 50)
                damageText.color = Color.red;
            else if (damage >= 25)
                damageText.color = Color.yellow;
            else
                damageText.color = Color.white;
        }
        else
        {
            // Если damageText не найден, ищем его
            damageText = GetComponentInChildren<Text>();
            if (damageText != null)
            {
                damageText.text = damage.ToString("F0");

                // Цвет в зависимости от урона
                if (damage >= 50)
                    damageText.color = Color.red;
                else if (damage >= 25)
                    damageText.color = Color.yellow;
                else
                    damageText.color = Color.white;
            }
        }
    }

    /// <summary>
    /// Анимация текста
    /// </summary>
    private IEnumerator AnimateText()
    {
        Vector3 startPosition = transform.position;

        while (Time.time - startTime < lifetime)
        {
            float elapsed = Time.time - startTime;
            float progress = elapsed / lifetime;

            // Движение вверх
            transform.position = startPosition + moveDirection * moveSpeed * elapsed;

            // Затухание
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - progress;
            }

            yield return null;
        }

        // Уничтожаем объект
        Destroy(gameObject);
    }

    /// <summary>
    /// Создает текст урона в указанной позиции
    /// </summary>
    public static GameObject CreateDamageText(Vector3 position, float damage, GameObject prefab = null)
    {
        GameObject damageTextObj;

        if (prefab != null)
        {
            damageTextObj = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            // Создаем простой текст урона
            damageTextObj = CreateSimpleDamageText(position);
        }

        // Настраиваем урон
        DamageText damageTextComponent = damageTextObj.GetComponent<DamageText>();
        if (damageTextComponent == null)
        {
            damageTextComponent = damageTextObj.AddComponent<DamageText>();
        }

        damageTextComponent.SetDamage(damage);

        return damageTextObj;
    }

    /// <summary>
    /// Создает простой текст урона без префаба
    /// </summary>
    private static GameObject CreateSimpleDamageText(Vector3 position)
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("DamageTextCanvas");
        canvasObj.transform.position = position;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // Настраиваем размер Canvas
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2f, 1f);
        canvasRect.localScale = Vector3.one * 0.1f; // Уменьшаем размер

        // Создаем Text
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(canvasObj.transform);

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 200; // Увеличиваем размер шрифта
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "100"; // Тестовый текст

        // Настраиваем RectTransform
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localPosition = Vector3.zero;
        textRect.localScale = Vector3.one;

        // Добавляем Outline для лучшей видимости
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, 2);

        return canvasObj;
    }
}
