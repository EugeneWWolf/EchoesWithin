using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

public class SellZone : MonoBehaviour
{
    [Tooltip("Множитель цены продажи (например, 1.0 = обычная цена)")]
    public float priceMultiplier = 1f;

    [Header("UI Message")]
    [Tooltip("Сообщение, которое будет показано при входе в зону продажи")]
    [SerializeField] private string sellMessage = "Нажмите клавишу С для продажи предмета";

    private bool playerInside;
    private GameObject currentMessageUI;

    public bool IsPlayerInside => playerInside;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            ShowSellMessage();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            HideSellMessage();
        }
    }

    private void ShowSellMessage()
    {
        // Удаляем предыдущее сообщение, если оно есть
        if (currentMessageUI != null)
        {
            Destroy(currentMessageUI);
            currentMessageUI = null;
        }

        // Находим Canvas
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("⚠ SellZone: Canvas не найден! Сообщение не будет отображено.");
            return;
        }

        // Создаем UI элемент для сообщения
        GameObject messageObj = new GameObject("SellZoneMessage");
        messageObj.transform.SetParent(canvas.transform, false);
        currentMessageUI = messageObj;

        // Добавляем RectTransform
        RectTransform rectTransform = messageObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero; // Позиция в центре экрана
        rectTransform.sizeDelta = new Vector2(600, 100);

        // Пытаемся использовать TextMeshPro, если доступен
        Component textComponent = TryAddTextMeshPro(messageObj, sellMessage);
        if (textComponent == null)
        {
            // Если TextMeshPro недоступен, используем обычный Text
            Text text = messageObj.AddComponent<Text>();
            text.text = sellMessage;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        Debug.Log($"✅ SellZone: Показано сообщение: {sellMessage}");
    }

    private void HideSellMessage()
    {
        if (currentMessageUI != null)
        {
            Destroy(currentMessageUI);
            currentMessageUI = null;
            Debug.Log("✅ SellZone: Сообщение скрыто");
        }
    }

    private Canvas FindCanvas()
    {
        // Сначала ищем Canvas с именем "Inventory"
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas c in allCanvases)
        {
            if (c.name == "Inventory" || c.name.Contains("Inventory"))
            {
                return c;
            }
        }

        // Если не нашли, ищем Canvas с максимальным sortingOrder
        if (allCanvases.Length > 0)
        {
            Canvas maxCanvas = allCanvases[0];
            int maxSortingOrder = maxCanvas.sortingOrder;
            foreach (Canvas c in allCanvases)
            {
                if (c.sortingOrder > maxSortingOrder)
                {
                    maxSortingOrder = c.sortingOrder;
                    maxCanvas = c;
                }
            }
            return maxCanvas;
        }

        // Если все еще не нашли, используем первый попавшийся
        return FindObjectOfType<Canvas>();
    }

    private Component TryAddTextMeshPro(GameObject obj, string text)
    {
        // Пытаемся найти TextMeshPro через рефлексию
        try
        {
            System.Type tmpType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType != null)
            {
                Component tmpComponent = obj.AddComponent(tmpType);
                PropertyInfo textProperty = tmpType.GetProperty("text");
                if (textProperty != null)
                {
                    textProperty.SetValue(tmpComponent, text);

                    // Настраиваем размер шрифта и выравнивание
                    PropertyInfo fontSizeProperty = tmpType.GetProperty("fontSize");
                    if (fontSizeProperty != null)
                    {
                        fontSizeProperty.SetValue(tmpComponent, 24f);
                    }

                    PropertyInfo alignmentProperty = tmpType.GetProperty("alignment");
                    if (alignmentProperty != null)
                    {
                        System.Type alignmentEnumType = System.Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
                        if (alignmentEnumType != null)
                        {
                            object centerValue = System.Enum.Parse(alignmentEnumType, "Center");
                            alignmentProperty.SetValue(tmpComponent, centerValue);
                        }
                    }

                    return tmpComponent;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠ SellZone: Не удалось использовать TextMeshPro: {e.Message}");
        }

        return null;
    }

    private void OnDestroy()
    {
        // Очищаем сообщение при уничтожении объекта
        HideSellMessage();
    }
}
