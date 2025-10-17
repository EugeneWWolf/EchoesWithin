using UnityEngine;
using System.Collections;

/// <summary>
/// Простая система отображения урона с 3D текстом
/// </summary>
public class SimpleDamageText : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private Vector3 moveDirection = Vector3.up;

    private TextMesh textMesh;
    private float startTime;
    private Color originalColor;

    private void Awake()
    {
        textMesh = GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = gameObject.AddComponent<TextMesh>();
        }

        startTime = Time.time;
        originalColor = textMesh.color;
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
        if (textMesh != null)
        {
            textMesh.text = damage.ToString("F0");
            textMesh.fontSize = 20;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            // Цвет в зависимости от урона
            if (damage >= 50)
                textMesh.color = Color.red;
            else if (damage >= 25)
                textMesh.color = Color.yellow;
            else
                textMesh.color = Color.white;

            originalColor = textMesh.color;
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
            if (textMesh != null)
            {
                Color currentColor = originalColor;
                currentColor.a = 1f - progress;
                textMesh.color = currentColor;
            }

            yield return null;
        }

        // Уничтожаем объект
        Destroy(gameObject);
    }

    /// <summary>
    /// Создает текст урона в указанной позиции
    /// </summary>
    public static GameObject CreateDamageText(Vector3 position, float damage)
    {
        GameObject damageTextObj = new GameObject("DamageText");
        damageTextObj.transform.position = position;

        // Добавляем TextMesh
        TextMesh textMesh = damageTextObj.AddComponent<TextMesh>();
        textMesh.text = damage.ToString("F0");
        textMesh.fontSize = 20;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;

        // Цвет в зависимости от урона
        if (damage >= 50)
            textMesh.color = Color.red;
        else if (damage >= 25)
            textMesh.color = Color.yellow;
        else
            textMesh.color = Color.white;

        // Добавляем компонент анимации
        SimpleDamageText damageText = damageTextObj.AddComponent<SimpleDamageText>();

        return damageTextObj;
    }
}
