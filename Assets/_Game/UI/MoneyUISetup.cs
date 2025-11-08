using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Утилита для автоматического создания Money UI
/// </summary>
public class MoneyUISetup : MonoBehaviour
{
    [Header("Setup Settings")]
    [SerializeField] private Vector2 moneyUIPosition = new Vector2(50, -50);
    [SerializeField] private Vector2 moneyUISize = new Vector2(200, 50);

    [ContextMenu("Check MoneyUI Status")]
    public void CheckMoneyUIStatus()
    {
        Debug.Log("🔍 Проверяем состояние MoneyUI в сцене...");

        MoneyUI[] allMoneyUIs = FindObjectsOfType<MoneyUI>();
        Debug.Log($"📊 Найдено MoneyUI объектов: {allMoneyUIs.Length}");

        for (int i = 0; i < allMoneyUIs.Length; i++)
        {
            MoneyUI moneyUI = allMoneyUIs[i];
            Debug.Log($"💰 MoneyUI #{i + 1}:");
            Debug.Log($"   - Объект: {moneyUI.gameObject.name}");
            Debug.Log($"   - Активен: {moneyUI.gameObject.activeInHierarchy}");
            Debug.Log($"   - Canvas: {moneyUI.transform.parent?.name ?? "Нет родителя"}");

            // Проверяем, назначен ли в PlayerController
            PlayerController playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                var moneyUIField = typeof(PlayerController).GetField("moneyUI",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                MoneyUI assignedMoneyUI = moneyUIField?.GetValue(playerController) as MoneyUI;

                if (assignedMoneyUI == moneyUI)
                {
                    Debug.Log($"   - ✅ Назначен в PlayerController");
                }
                else
                {
                    Debug.Log($"   - ❌ НЕ назначен в PlayerController");
                }
            }
        }

        if (allMoneyUIs.Length > 1)
        {
            Debug.LogWarning("⚠ Обнаружены дублирующиеся MoneyUI! Используйте 'Clean Up Duplicate MoneyUI' для очистки");
        }
    }

    [ContextMenu("Clean Up Duplicate MoneyUI")]
    public void CleanUpDuplicateMoneyUI()
    {
        Debug.Log("🧹 Начинаем очистку дублирующихся MoneyUI...");

        MoneyUI[] allMoneyUIs = FindObjectsOfType<MoneyUI>();
        Debug.Log($"🔍 Найдено MoneyUI объектов: {allMoneyUIs.Length}");

        if (allMoneyUIs.Length <= 1)
        {
            Debug.Log("✅ Дублирующихся MoneyUI не найдено");
            return;
        }

        // Оставляем первый, удаляем остальные
        for (int i = 1; i < allMoneyUIs.Length; i++)
        {
            Debug.Log($"🗑️ Удаляем дублирующийся MoneyUI: {allMoneyUIs[i].gameObject.name}");
            DestroyImmediate(allMoneyUIs[i].gameObject);
        }

        Debug.Log($"✅ Очистка завершена. Остался 1 MoneyUI: {allMoneyUIs[0].gameObject.name}");
    }

    [ContextMenu("Setup Money UI")]
    public void SetupMoneyUI()
    {
        Debug.Log("🚀 Начинаем настройку Money UI...");

        // Проверяем, есть ли уже MoneyUI в сцене
        MoneyUI existingMoneyUI = FindObjectOfType<MoneyUI>();
        if (existingMoneyUI != null)
        {
            Debug.LogWarning("⚠ MoneyUI уже существует в сцене!");
            Debug.Log($"⚠ Найден MoneyUI на объекте: {existingMoneyUI.gameObject.name}");
            Debug.Log("⚠ Удалите существующий MoneyUI перед созданием нового");
            return;
        }

        // Шаг 1: Создаем или находим Canvas
        Canvas canvas = FindOrCreateCanvas();
        if (canvas == null)
        {
            Debug.LogError("❌ Не удалось создать Canvas!");
            return;
        }

        // Шаг 2: Создаем MoneyUI
        GameObject moneyUI = CreateMoneyUI(canvas);
        if (moneyUI == null)
        {
            Debug.LogError("❌ Не удалось создать MoneyUI!");
            return;
        }

        Debug.Log("✅ Money UI создан успешно!");
        Debug.Log("📝 Инструкции:");
        Debug.Log("1. Перетащите созданный MoneyUI в поле 'Money UI' в PlayerController");
        Debug.Log("2. Используйте контекстное меню PlayerWallet для тестирования");
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

    private GameObject CreateMoneyUI(Canvas canvas)
    {
        Debug.Log("🔨 Создаем MoneyUI...");

        // Создаем основной контейнер
        GameObject moneyContainer = new GameObject("MoneyUI");
        moneyContainer.transform.SetParent(canvas.transform, false);

        // Настраиваем RectTransform
        RectTransform containerRect = moneyContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(0, 1);
        containerRect.anchoredPosition = moneyUIPosition;
        containerRect.sizeDelta = moneyUISize;

        // Добавляем фон
        Image backgroundImage = moneyContainer.AddComponent<Image>();
        backgroundImage.color = new Color(0, 0, 0, 0.7f);

        // Создаем текст
        GameObject textObject = new GameObject("MoneyText");
        textObject.transform.SetParent(moneyContainer.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text moneyText = textObject.AddComponent<Text>();
        moneyText.text = "💰 0";
        moneyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        moneyText.fontSize = 24;
        moneyText.color = Color.white;
        moneyText.alignment = TextAnchor.MiddleCenter;

        // Добавляем Outline для лучшей читаемости
        Outline textOutline = textObject.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(1, 1);

        // Добавляем MoneyUI компонент
        MoneyUI moneyUIComponent = moneyContainer.AddComponent<MoneyUI>();

        // Настраиваем ссылку на текст через рефлексию
        var moneyTextField = typeof(MoneyUI).GetField("moneyText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        moneyTextField?.SetValue(moneyUIComponent, moneyText);

        Debug.Log("✅ MoneyUI создан успешно");
        return moneyContainer;
    }
}
