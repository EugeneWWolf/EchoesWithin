using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Утилита для автоматического создания Goal UI в верхнем правом углу
/// </summary>
public class GoalUISetup : MonoBehaviour
{
    [Header("Setup Settings")]
    [SerializeField] private Vector2 goalUIPosition = new Vector2(-50, -50); // Отрицательные значения для верхнего правого угла
    [SerializeField] private Vector2 goalUISize = new Vector2(300, 60);
    [SerializeField] private int requiredMoney = 500;

    [ContextMenu("Setup Goal UI")]
    public void SetupGoalUI()
    {
        Debug.Log("🚀 Начинаем настройку Goal UI...");

        // Проверяем, есть ли уже GoalUI в сцене
        GoalUI existingGoalUI = FindObjectOfType<GoalUI>();
        if (existingGoalUI != null)
        {
            Debug.LogWarning("⚠ GoalUI уже существует в сцене!");
            Debug.Log($"⚠ Найден GoalUI на объекте: {existingGoalUI.gameObject.name}");
            Debug.Log("⚠ Удалите существующий GoalUI перед созданием нового");
            return;
        }

        // Шаг 1: Создаем или находим Canvas
        Canvas canvas = FindOrCreateCanvas();
        if (canvas == null)
        {
            Debug.LogError("❌ Не удалось создать Canvas!");
            return;
        }

        // Шаг 2: Создаем GoalUI
        GameObject goalUI = CreateGoalUI(canvas);
        if (goalUI == null)
        {
            Debug.LogError("❌ Не удалось создать GoalUI!");
            return;
        }

        Debug.Log("✅ Goal UI создан успешно!");
        Debug.Log("📝 Инструкции:");
        Debug.Log("1. Перетащите созданный GoalUI в поле 'Goal UI' в PlayerController (если добавите поддержку)");
        Debug.Log("2. Убедитесь, что GoalUI привязан к PlayerWallet");
    }

    private Canvas FindOrCreateCanvas()
    {
        // Ищем существующий Canvas
        Canvas existingCanvas = FindObjectOfType<Canvas>();
        if (existingCanvas != null)
        {
            Debug.Log("✅ Найден существующий Canvas");
            return existingCanvas;
        }

        // Создаем новый Canvas
        Debug.Log("🔨 Создаем новый Canvas...");
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        // Добавляем необходимые компоненты
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        Debug.Log("✅ Canvas создан успешно");
        return canvas;
    }

    private GameObject CreateGoalUI(Canvas canvas)
    {
        Debug.Log("🔨 Создаем GoalUI...");

        // Создаем основной контейнер
        GameObject goalContainer = new GameObject("GoalUI");
        goalContainer.transform.SetParent(canvas.transform, false);

        // Настраиваем RectTransform для верхнего правого угла
        RectTransform containerRect = goalContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1, 1); // Верхний правый угол
        containerRect.anchorMax = new Vector2(1, 1); // Верхний правый угол
        containerRect.anchoredPosition = goalUIPosition; // Отрицательные значения для позиционирования от угла
        containerRect.sizeDelta = goalUISize;

        // Добавляем фон
        Image backgroundImage = goalContainer.AddComponent<Image>();
        backgroundImage.color = new Color(0, 0, 0, 0.7f);

        // Создаем текст
        GameObject textObject = new GameObject("GoalText");
        textObject.transform.SetParent(goalContainer.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text goalText = textObject.AddComponent<Text>();
        goalText.text = $"Goal: Collect ${requiredMoney} and leave this planet";
        goalText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goalText.fontSize = 18;
        goalText.color = Color.white;
        goalText.alignment = TextAnchor.MiddleCenter;

        // Добавляем Outline для лучшей читаемости
        Outline textOutline = textObject.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(1, 1);

        // Добавляем GoalUI компонент
        GoalUI goalUIComponent = goalContainer.AddComponent<GoalUI>();

        // Настраиваем ссылку на текст через рефлексию ПЕРЕД вызовом SetRequiredMoney
        // Используем безопасный способ через UnityEditor или публичное поле
        try
        {
            var goalTextField = typeof(GoalUI).GetField("goalText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (goalTextField != null)
            {
                goalTextField.SetValue(goalUIComponent, goalText);
            }
            else
            {
                Debug.LogWarning("⚠ Не удалось установить goalText через рефлексию. Установите его вручную в инспекторе.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠ Ошибка при установке goalText: {e.Message}. Установите его вручную в инспекторе.");
        }

        // Теперь устанавливаем требуемую сумму (после того как текст установлен)
        goalUIComponent.SetRequiredMoney(requiredMoney);

        // Автоматически привязываем к PlayerWallet если он есть
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            PlayerWallet wallet = playerController.GetComponent<PlayerWallet>();
            if (wallet != null)
            {
                goalUIComponent.BindWallet(wallet);
                Debug.Log("✅ GoalUI автоматически привязан к PlayerWallet");
            }
            else
            {
                Debug.LogWarning("⚠ PlayerWallet не найден на игроке. Привяжите GoalUI вручную.");
            }
        }

        Debug.Log("✅ GoalUI создан успешно");
        return goalContainer;
    }
}

