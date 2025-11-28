using UnityEngine;
using System.Reflection;

/// <summary>
/// Компонент для отображения 3D подписи в игровом мире
/// Текст всегда поворачивается к камере (billboard эффект)
/// </summary>
public class WorldSign : MonoBehaviour
{
    [Header("Sign Settings")]
    [Tooltip("Текст подписи")]
    public string signText = "Подпись";

    [Tooltip("Высота подписи над объектом")]
    public float heightOffset = 2f;

    [Tooltip("Размер шрифта")]
    [SerializeField] private int fontSize = 3;

    [Tooltip("Цвет текста")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Использовать TextMeshPro (если доступен)")]
    [SerializeField] private bool useTextMeshPro = true;

    private GameObject signObject;
    private Component textComponent;
    private Camera mainCamera;
    private bool isInitialized = false;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        CreateSign();
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        isInitialized = true;
    }

    private void LateUpdate()
    {
        // Поворачиваем текст к камере (billboard эффект)
        if (signObject != null && mainCamera != null)
        {
            Vector3 directionToCamera = mainCamera.transform.position - signObject.transform.position;
            if (directionToCamera != Vector3.zero)
            {
                signObject.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }

    private void CreateSign()
    {
        // Удаляем старую подпись, если она существует
        if (signObject != null)
        {
            DestroyImmediate(signObject);
        }

        // Создаем объект для подписи
        signObject = new GameObject("WorldSign");
        signObject.transform.SetParent(transform);
        signObject.transform.localPosition = Vector3.up * heightOffset;
        signObject.transform.localRotation = Quaternion.identity;
        signObject.transform.localScale = Vector3.one;

        // Пытаемся использовать TextMeshPro, если доступен
        if (useTextMeshPro)
        {
            textComponent = TryAddTextMeshPro(signObject, signText);
        }

        // Если TextMeshPro недоступен, используем обычный TextMesh
        if (textComponent == null)
        {
            TextMesh textMesh = signObject.AddComponent<TextMesh>();
            textMesh.text = signText;
            textMesh.fontSize = fontSize;
            textMesh.color = textColor;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.01f;
            textMesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Добавляем обводку для лучшей читаемости
            textMesh.fontStyle = FontStyle.Bold;

            textComponent = textMesh;
        }

        Debug.Log($"✅ WorldSign: Создана подпись '{signText}' для {gameObject.name}");
    }

    private Component TryAddTextMeshPro(GameObject obj, string text)
    {
        try
        {
            System.Type tmpType = System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
            if (tmpType != null)
            {
                Component tmpComponent = obj.AddComponent(tmpType);
                PropertyInfo textProperty = tmpType.GetProperty("text");
                PropertyInfo fontSizeProperty = tmpType.GetProperty("fontSize");
                PropertyInfo colorProperty = tmpType.GetProperty("color");
                PropertyInfo alignmentProperty = tmpType.GetProperty("alignment");

                if (textProperty != null)
                {
                    textProperty.SetValue(tmpComponent, text);
                }

                if (fontSizeProperty != null)
                {
                    fontSizeProperty.SetValue(tmpComponent, (float)fontSize);
                }

                if (colorProperty != null)
                {
                    colorProperty.SetValue(tmpComponent, textColor);
                }

                if (alignmentProperty != null)
                {
                    System.Type alignmentEnumType = System.Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
                    if (alignmentEnumType != null)
                    {
                        object centerValue = System.Enum.Parse(alignmentEnumType, "Center");
                        alignmentProperty.SetValue(tmpComponent, centerValue);
                    }
                }

                Debug.Log("✅ WorldSign: Использован TextMeshPro");
                return tmpComponent;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠ WorldSign: Не удалось использовать TextMeshPro: {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// Устанавливает текст подписи
    /// </summary>
    public void SetText(string text)
    {
        signText = text;

        // Убеждаемся, что подпись создана
        if (!isInitialized)
        {
            Initialize();
        }

        if (textComponent != null)
        {
            // Обновляем текст через рефлексию (для TextMeshPro)
            try
            {
                PropertyInfo textProperty = textComponent.GetType().GetProperty("text");
                if (textProperty != null)
                {
                    textProperty.SetValue(textComponent, text);
                    return;
                }
            }
            catch { }

            // Для обычного TextMesh
            if (textComponent is TextMesh textMesh)
            {
                textMesh.text = text;
            }
        }
    }

    /// <summary>
    /// Устанавливает цвет текста
    /// </summary>
    public void SetColor(Color color)
    {
        textColor = color;

        if (textComponent != null)
        {
            // Обновляем цвет через рефлексию (для TextMeshPro)
            try
            {
                PropertyInfo colorProperty = textComponent.GetType().GetProperty("color");
                if (colorProperty != null)
                {
                    colorProperty.SetValue(textComponent, color);
                    return;
                }
            }
            catch { }

            // Для обычного TextMesh
            if (textComponent is TextMesh textMesh)
            {
                textMesh.color = color;
            }
        }
    }

    private void OnDestroy()
    {
        if (signObject != null)
        {
            Destroy(signObject);
        }
    }
}

