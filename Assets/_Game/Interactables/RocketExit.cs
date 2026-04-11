using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Reflection;

/// <summary>
/// Компонент для ракеты - финальной цели игры
/// Игрок должен собрать достаточно денег и взаимодействовать с ракетой, чтобы покинуть планету
/// </summary>
public class RocketExit : MonoBehaviour
{
    [Header("Exit Settings")]
    [SerializeField] private int requiredMoney = 500;
    [SerializeField] private string insufficientFundsMessage = "Вам нужно ${0}, чтобы покинуть планету! У вас есть ${1}.";

    [Header("References")]
    [SerializeField] private PlayerWallet wallet;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject errorMessagePrefab; // Опциональный префаб для сообщения об ошибке
    [SerializeField] private float errorMessageDuration = 3f;

    [Header("Victory Screen")]
    [SerializeField] private Sprite victoryImage; // Картинка для экрана победы (на весь экран)
    [SerializeField] private string pressEnterText = "Нажмите Enter для выхода из игры";
    [SerializeField] private int pressEnterFontSize = 36;
    [SerializeField] private Color pressEnterTextColor = Color.white;

    [Header("World Sign")]
    [Tooltip("Показывать подпись над ракетой")]
    [SerializeField] private bool showSign = true;
    [Tooltip("Текст подписи")]
    [SerializeField] private string signText = "Выход из данжа";
    [Tooltip("Высота подписи")]
    [SerializeField] private float signHeight = 3f;

    [Header("Продолжить игру при выполненной квоте")]
    [Tooltip("Если включено и денег хватает, сначала показывается выбор: улететь или поднять квоту и играть дальше")]
    [SerializeField] private bool offerContinueChoiceWhenQuotaMet = true;
    [Tooltip("Во сколько раз умножить текущую квоту при «Продолжить игру» (минимум ×2)")]
    [SerializeField, Min(2f)] private float quotaMultiplierOnContinue = 4f;
    [Tooltip("Новая квота не ниже «баланс × (1 + эта доля)», чтобы нельзя было сразу снова улететь с теми же деньгами")]
    [SerializeField, Range(0.05f, 3f)] private float continueQuotaAboveBalanceFraction = 0.25f;
    [SerializeField] private string choiceTitleText = "Квота выполнена";
    [SerializeField] private string choiceBodyFormat = "У вас ${0}. Сейчас для вылета нужно ${1}.\nПокинуть планету или продолжить? Новая квота будет ${2}.";
    [SerializeField] private string leaveButtonText = "Покинуть планету";
    [SerializeField] private string continueButtonText = "Продолжить игру";

    private GameObject currentErrorMessage; // Текущее сообщение об ошибке на экране
    private WorldSign worldSign;
    private GameObject victoryScreen; // Экран победы
    private bool isShowingVictoryScreen = false;
    private GameObject choiceScreenRoot;
    private bool isShowingExitChoice = false;
    private static Sprite s_cachedUiSprite;

    private void Start()
    {
        // Автоматически находим PlayerWallet если не назначен
        if (wallet == null)
        {
            PlayerController playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                wallet = playerController.GetComponent<PlayerWallet>();
                if (wallet == null)
                {
                    Debug.LogWarning("⚠ RocketExit: PlayerWallet не найден на игроке!");
                }
            }
        }

        // Убеждаемся, что объект на правильном слое для взаимодействия
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1 && gameObject.layer != interactableLayer)
        {
            gameObject.layer = interactableLayer;
            Debug.Log($"🔧 RocketExit: Установлен слой Interactable для {gameObject.name}");
        }

        // Убеждаемся, что есть коллайдер
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            // Создаем BoxCollider по умолчанию
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            Debug.Log($"🔧 RocketExit: Добавлен BoxCollider для {gameObject.name}");
        }
        else if (!collider.isTrigger)
        {
            // Делаем коллайдер триггером для взаимодействия
            collider.isTrigger = true;
            Debug.Log($"🔧 RocketExit: Коллайдер установлен как триггер для {gameObject.name}");
        }

        // Создаем подпись, если включена
        if (showSign)
        {
            worldSign = gameObject.AddComponent<WorldSign>();
            // Используем рефлексию для установки параметров
            SetSignProperties(worldSign, signText, signHeight);
        }
    }

    private void SetSignProperties(WorldSign sign, string text, float height)
    {
        sign.signText = text;
        sign.heightOffset = height;
        sign.SetText(text);
    }

    /// <summary>
    /// Вызывается при взаимодействии с ракетой
    /// </summary>
    public bool TryExit()
    {
        if (isShowingExitChoice || isShowingVictoryScreen)
            return false;

        if (wallet == null)
        {
            Debug.LogError("❌ RocketExit: PlayerWallet не найден!");
            ShowErrorMessage("Error: Wallet not found!");
            return false;
        }

        int currentBalance = wallet.Balance;

        if (currentBalance >= requiredMoney)
        {
            if (offerContinueChoiceWhenQuotaMet)
            {
                ShowExitChoicePanel(currentBalance);
                return true;
            }

            Debug.Log($"🚀 Игрок покидает планету! Баланс: ${currentBalance} (требуется: ${requiredMoney})");
            ExitGame();
            return true;
        }
        else
        {
            // Недостаточно денег - показываем ошибку
            string errorMessage = string.Format(insufficientFundsMessage, requiredMoney, currentBalance);
            Debug.LogWarning($"⚠ {errorMessage}");
            ShowErrorMessage(errorMessage);
            return false;
        }
    }

    private void ExitGame()
    {
        Debug.Log("🎉 Поздравляем! Вы успешно покинули планету!");
        Debug.Log("🎮 Игра завершена.");

        // Показываем экран победы вместо мгновенного выхода
        ShowVictoryScreen();
    }

    private static Sprite GetSolidUiSprite()
    {
        if (s_cachedUiSprite == null)
        {
            Texture2D t = Texture2D.whiteTexture;
            s_cachedUiSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
        return s_cachedUiSprite;
    }

    private void ShowExitChoicePanel(int currentBalance)
    {
        if (isShowingExitChoice) return;
        isShowingExitChoice = true;

        Canvas canvas = FindInventoryCanvas();
        if (canvas == null)
        {
            Debug.LogError("❌ RocketExit: Canvas не найден для окна выбора!");
            isShowingExitChoice = false;
            ExitGame();
            return;
        }

        int nextQuota = ComputeNextQuotaAfterContinue(currentBalance);

        GameObject root = new GameObject("RocketExitChoice");
        root.transform.SetParent(canvas.transform, false);
        choiceScreenRoot = root;

        RectTransform panelRect = root.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.92f);
        bg.sprite = GetSolidUiSprite();

        CreateChoiceLegacyText(root.transform, choiceTitleText, 32, Color.white, new Vector2(0.5f, 0.62f), new Vector2(720, 72));

        string body = string.Format(choiceBodyFormat, currentBalance, requiredMoney, nextQuota);
        CreateChoiceLegacyText(root.transform, body, 22, new Color(0.95f, 0.95f, 0.95f, 1f), new Vector2(0.5f, 0.48f), new Vector2(760, 140));

        GameObject row = new GameObject("ButtonsRow");
        row.transform.SetParent(root.transform, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.28f);
        rowRect.anchorMax = new Vector2(0.5f, 0.28f);
        rowRect.sizeDelta = new Vector2(640, 64);
        rowRect.anchoredPosition = Vector2.zero;

        GameObject leaveBtn = CreateChoiceButton(row.transform, leaveButtonText, new Color(0.15f, 0.45f, 0.2f, 1f), new Vector2(-165, 0), new Vector2(300, 56), OnChosenLeavePlanet);
        CreateChoiceButton(row.transform, continueButtonText, new Color(0.2f, 0.25f, 0.5f, 1f), new Vector2(165, 0), new Vector2(300, 56), () => OnChosenContinuePlaying(currentBalance));

        panelRect.SetAsLastSibling();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (EventSystem.current != null && leaveBtn != null)
            EventSystem.current.SetSelectedGameObject(leaveBtn);

        Debug.Log("✅ RocketExit: Показан выбор — улететь или продолжить с повышенной квотой.");
    }

    private static void CreateChoiceLegacyText(Transform parent, string text, int fontSize, Color color, Vector2 anchorCenter, Vector2 sizeDelta)
    {
        GameObject go = new GameObject("ChoiceText");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorCenter;
        rt.anchorMax = anchorCenter;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;

        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        Outline o = go.AddComponent<Outline>();
        o.effectColor = Color.black;
        o.effectDistance = new Vector2(2, 2);
    }

    private static GameObject CreateChoiceButton(Transform parent, string label, Color bgColor, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject("Button_" + label);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image img = go.AddComponent<Image>();
        img.sprite = GetSolidUiSprite();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = bgColor * 1.15f;
        colors.pressedColor = bgColor * 0.85f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        Text t = textGo.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 20;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, 1);

        return go;
    }

    private void OnChosenLeavePlanet()
    {
        CloseExitChoicePanel();
        ExitGame();
    }

    private void OnChosenContinuePlaying(int currentBalanceAtChoice)
    {
        int newQuota = ComputeNextQuotaAfterContinue(currentBalanceAtChoice);

        foreach (RocketExit rocket in FindObjectsOfType<RocketExit>())
            rocket.SetRequiredMoney(newQuota);

        foreach (GoalUI goal in FindObjectsOfType<GoalUI>())
        {
            goal.SetRequiredMoney(newQuota);
            goal.RefreshDisplay();
        }

        CloseExitChoicePanel();
        RestoreGameplayCursor();

        Debug.Log($"🚀 Квота для вылета повышена до ${newQuota}. Игра продолжается.");
    }

    /// <summary>
    /// Новая квота после «Продолжить»: max(текущая_квота × множитель, баланс × (1 + доля_над_балансом)).
    /// </summary>
    private int ComputeNextQuotaAfterContinue(int playerBalance)
    {
        float mult = Mathf.Max(2f, quotaMultiplierOnContinue);
        int fromMultiplier = Mathf.CeilToInt(requiredMoney * mult);
        fromMultiplier = Mathf.Max(fromMultiplier, requiredMoney + 1);

        float frac = Mathf.Max(0.05f, continueQuotaAboveBalanceFraction);
        int fromBalance = Mathf.CeilToInt(Mathf.Max(0, playerBalance) * (1f + frac));
        fromBalance = Mathf.Max(fromBalance, playerBalance + 1);

        return Mathf.Max(fromMultiplier, fromBalance);
    }

    private void CloseExitChoicePanel()
    {
        if (choiceScreenRoot != null)
        {
            Destroy(choiceScreenRoot);
            choiceScreenRoot = null;
        }
        isShowingExitChoice = false;
    }

    private static void RestoreGameplayCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ShowVictoryScreen()
    {
        if (isShowingVictoryScreen) return;
        isShowingVictoryScreen = true;

        // Находим Canvas "Inventory"
        Canvas canvas = FindInventoryCanvas();
        if (canvas == null)
        {
            Debug.LogError("❌ RocketExit: Canvas не найден для экрана победы!");
            // Выходим сразу, если Canvas не найден
            QuitGame();
            return;
        }

        // Создаем панель на весь экран
        GameObject victoryPanel = new GameObject("VictoryScreen");
        victoryPanel.transform.SetParent(canvas.transform, false);
        victoryScreen = victoryPanel;

        // Настраиваем RectTransform на весь экран
        RectTransform panelRect = victoryPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        // Добавляем фон (черный или полупрозрачный)
        Image backgroundImage = victoryPanel.AddComponent<Image>();
        backgroundImage.color = new Color(0, 0, 0, 1f); // Черный фон

        // Если есть картинка, добавляем её
        if (victoryImage != null)
        {
            GameObject imageObj = new GameObject("VictoryImage");
            imageObj.transform.SetParent(victoryPanel.transform, false);

            RectTransform imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            Image image = imageObj.AddComponent<Image>();
            image.sprite = victoryImage;
            image.preserveAspect = true; // Сохраняем пропорции картинки
        }

        // Добавляем текст с инструкцией
        GameObject textObj = new GameObject("PressEnterText");
        textObj.transform.SetParent(victoryPanel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.1f);
        textRect.anchorMax = new Vector2(0.5f, 0.1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(400, 60);

        // Пытаемся использовать TextMeshPro, если доступен
        Component textComponent = CreateTextComponent(textObj, pressEnterText, pressEnterFontSize, pressEnterTextColor);

        if (textComponent == null)
        {
            // Используем Legacy Text
            Text legacyText = textObj.AddComponent<Text>();
            legacyText.text = pressEnterText;
            legacyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacyText.fontSize = pressEnterFontSize;
            legacyText.color = pressEnterTextColor;
            legacyText.alignment = TextAnchor.MiddleCenter;

            // Добавляем Outline с черной обводкой для лучшей читаемости
            Outline textOutline = textObj.AddComponent<Outline>();
            textOutline.effectColor = Color.black;
            textOutline.effectDistance = new Vector2(2, 2);
        }

        // Убеждаемся, что панель на переднем плане
        panelRect.SetAsLastSibling();

        // Разблокируем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("✅ RocketExit: Экран победы показан. Нажмите Enter для выхода.");
    }

    private Component CreateTextComponent(GameObject obj, string text, int fontSize, Color color)
    {
        // Пытаемся создать TextMeshPro компонент через рефлексию
        try
        {
            System.Type tmpType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType != null)
            {
                Component tmpComp = obj.AddComponent(tmpType);
                PropertyInfo textProperty = tmpType.GetProperty("text");
                PropertyInfo fontSizeProperty = tmpType.GetProperty("fontSize");
                PropertyInfo colorProperty = tmpType.GetProperty("color");
                PropertyInfo alignmentProperty = tmpType.GetProperty("alignment");

                if (textProperty != null) textProperty.SetValue(tmpComp, text);
                if (fontSizeProperty != null) fontSizeProperty.SetValue(tmpComp, (float)fontSize);
                if (colorProperty != null) colorProperty.SetValue(tmpComp, color);
                if (alignmentProperty != null)
                {
                    // TMPro.TextAlignmentOptions.Center
                    System.Type alignmentEnumType = System.Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
                    if (alignmentEnumType != null)
                    {
                        object centerValue = System.Enum.Parse(alignmentEnumType, "Center");
                        alignmentProperty.SetValue(tmpComp, centerValue);
                    }
                }

                // Добавляем Outline для TextMeshPro через рефлексию
                try
                {
                    System.Type outlineType = System.Type.GetType("TMPro.Outline, Unity.TextMeshPro");
                    if (outlineType != null)
                    {
                        Component outlineComp = obj.AddComponent(outlineType);
                        PropertyInfo outlineColorProperty = outlineType.GetProperty("effectColor");
                        PropertyInfo outlineDistanceProperty = outlineType.GetProperty("effectDistance");

                        if (outlineColorProperty != null) outlineColorProperty.SetValue(outlineComp, Color.black);
                        if (outlineDistanceProperty != null) outlineDistanceProperty.SetValue(outlineComp, new Vector2(2, 2));

                        Debug.Log("✅ RocketExit: Добавлена черная обводка для TextMeshPro");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"⚠ RocketExit: Не удалось добавить Outline для TextMeshPro: {e.Message}");
                }

                Debug.Log("✅ RocketExit: Использован TextMeshPro для текста экрана победы");
                return tmpComp;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠ RocketExit: Не удалось создать TextMeshPro: {e.Message}");
        }

        return null;
    }

    private Canvas FindInventoryCanvas()
    {
        Canvas canvas = null;

        // Сначала ищем Canvas с именем "Inventory"
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas c in allCanvases)
        {
            if (c.name == "Inventory" || c.name.Contains("Inventory"))
            {
                canvas = c;
                break;
            }
        }

        // Если не нашли "Inventory", ищем Canvas с максимальным sortingOrder
        if (canvas == null)
        {
            int maxSortingOrder = int.MinValue;
            foreach (Canvas c in allCanvases)
            {
                if (c.sortingOrder > maxSortingOrder)
                {
                    maxSortingOrder = c.sortingOrder;
                    canvas = c;
                }
            }
        }

        // Если все еще не нашли, используем первый попавшийся
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        return canvas;
    }

    private void Update()
    {
        // Проверяем нажатие Enter для выхода из игры, если показывается экран победы
        // Используем новую систему ввода Unity
        if (isShowingVictoryScreen && Keyboard.current != null)
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                QuitGame();
            }
        }
    }

    private void QuitGame()
    {
        // Завершаем игру
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    private void ShowErrorMessage(string message)
    {
        Debug.LogWarning($"⚠ {message}");

        // Удаляем предыдущее сообщение, если оно есть
        if (currentErrorMessage != null)
        {
            Destroy(currentErrorMessage);
            currentErrorMessage = null;
        }

        // Проверяем, назначен ли префаб
        if (errorMessagePrefab == null)
        {
            Debug.LogWarning("⚠ RocketExit: Error Message Prefab не назначен в инспекторе! Назначьте префаб с TextMeshPro или Text компонентом.");
            return;
        }

        Debug.Log($"🔍 RocketExit: Пытаемся показать сообщение: {message}");
        Debug.Log($"🔍 RocketExit: Префаб назначен: {errorMessagePrefab.name}");

        // Находим Canvas с именем "Inventory" (основной Canvas для UI игрока)
        Canvas canvas = null;

        // Сначала ищем Canvas с именем "Inventory"
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas c in allCanvases)
        {
            if (c.name == "Inventory" || c.name.Contains("Inventory"))
            {
                canvas = c;
                break;
            }
        }

        // Если не нашли "Inventory", ищем Canvas с максимальным sortingOrder (обычно главный UI Canvas)
        if (canvas == null)
        {
            int maxSortingOrder = int.MinValue;
            foreach (Canvas c in allCanvases)
            {
                if (c.sortingOrder > maxSortingOrder)
                {
                    maxSortingOrder = c.sortingOrder;
                    canvas = c;
                }
            }
        }

        // Если все еще не нашли, используем первый попавшийся
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogError("❌ RocketExit: Canvas не найден! Сообщение не будет отображено на экране.");
            Debug.LogError("❌ Убедитесь, что в сцене есть Canvas (обычно создается автоматически с UI элементами).");
            return;
        }

        Debug.Log($"✅ RocketExit: Canvas найден: {canvas.name}");

        // Создаем экземпляр префаба
        GameObject errorObj = Instantiate(errorMessagePrefab, canvas.transform);
        currentErrorMessage = errorObj;
        errorObj.name = "ErrorMessage_" + Time.time; // Уникальное имя для отладки

        Debug.Log($"✅ RocketExit: Создан объект сообщения: {errorObj.name}");

        // Убеждаемся, что объект активен и виден
        errorObj.SetActive(true);

        // Настраиваем RectTransform для правильного отображения (если это UI элемент)
        RectTransform rectTransform = errorObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Debug.Log($"✅ RocketExit: RectTransform найден, настраиваем позицию...");

            // Устанавливаем правильные якоря для центрирования
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;

            // Убеждаемся, что масштаб правильный
            rectTransform.localScale = Vector3.one;

            // Убеждаемся, что объект на переднем плане
            rectTransform.SetAsLastSibling();

            Debug.Log($"✅ RocketExit: RectTransform настроен. Позиция: {rectTransform.anchoredPosition}, Масштаб: {rectTransform.localScale}");
        }
        else
        {
            Debug.LogWarning("⚠ RocketExit: RectTransform не найден на объекте сообщения. Это может быть проблемой для UI элементов.");
        }

        // Пытаемся найти и обновить текст - сначала TextMeshPro, потом Legacy Text
        bool textUpdated = false;

        // Выводим информацию о всех компонентах для отладки
        Component[] allComps = errorObj.GetComponentsInChildren<Component>(true);
        Debug.Log($"🔍 RocketExit: Найдено компонентов на объекте: {allComps.Length}");
        foreach (Component comp in allComps)
        {
            if (comp != null)
            {
                Debug.Log($"  - {comp.GetType().FullName} на {comp.gameObject.name}");
            }
        }

        // Пытаемся найти TextMeshPro компонент через рефлексию (более надежный способ)
        Component tmpComponent = FindTextMeshProComponent(errorObj);
        if (tmpComponent != null)
        {
            Debug.Log($"✅ RocketExit: TextMeshPro компонент найден: {tmpComponent.GetType().FullName}");

            // Используем рефлексию для установки текста
            PropertyInfo textProperty = tmpComponent.GetType().GetProperty("text");
            if (textProperty != null)
            {
                textProperty.SetValue(tmpComponent, message);
                textUpdated = true;
                Debug.Log($"✅ RocketExit: Текст обновлен через TextMeshPro: '{message}'");

                // Проверяем, что текст действительно установлен
                object currentText = textProperty.GetValue(tmpComponent);
                Debug.Log($"🔍 RocketExit: Проверка - текущий текст в компоненте: '{currentText}'");
            }
            else
            {
                Debug.LogError("❌ RocketExit: Свойство 'text' не найдено в TextMeshPro компоненте!");
            }
        }
        else
        {
            Debug.LogWarning("⚠ RocketExit: TextMeshPro компонент не найден, пробуем Legacy Text...");
        }

        // Если TextMeshPro не найден, пробуем Legacy Text
        if (!textUpdated)
        {
            Text textComponent = errorObj.GetComponentInChildren<Text>();
            if (textComponent == null)
            {
                textComponent = errorObj.GetComponent<Text>();
            }

            if (textComponent != null)
            {
                textComponent.text = message;
                textUpdated = true;
                Debug.Log($"✅ RocketExit: Текст обновлен через Legacy Text: '{message}'");
            }
            else
            {
                Debug.LogWarning("⚠ RocketExit: Legacy Text компонент также не найден!");
            }
        }

        if (!textUpdated)
        {
            Debug.LogError("❌ RocketExit: В префабе сообщения об ошибке не найден компонент Text или TextMeshProUGUI!");
            Debug.LogError("❌ Убедитесь, что префаб содержит TextMeshProUGUI (рекомендуется) или Text компонент.");
        }
        else
        {
            Debug.Log($"✅ RocketExit: Сообщение успешно создано и должно быть видно на экране!");
        }

        // Уничтожаем сообщение через заданное время
        Destroy(errorObj, errorMessageDuration);

        // Очищаем ссылку после уничтожения
        StartCoroutine(ClearErrorMessageReference(errorMessageDuration));
    }

    private System.Collections.IEnumerator ClearErrorMessageReference(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentErrorMessage != null)
        {
            currentErrorMessage = null;
        }
    }

    /// <summary>
    /// Находит TextMeshPro компонент через рефлексию (работает даже без условной компиляции)
    /// </summary>
    private Component FindTextMeshProComponent(GameObject obj)
    {
        // Пробуем найти компонент по имени типа через рефлексию
        Component[] allComponents = obj.GetComponentsInChildren<Component>(true);

        foreach (Component comp in allComponents)
        {
            if (comp == null) continue;

            string typeName = comp.GetType().FullName;

            // Проверяем различные возможные имена TextMeshPro компонентов
            if (typeName == "TMPro.TextMeshProUGUI" ||
                typeName == "TMPro.TMP_Text" ||
                typeName.Contains("TextMeshProUGUI") ||
                typeName.Contains("TMP_Text"))
            {
                Debug.Log($"🔍 RocketExit: Найден TextMeshPro компонент: {typeName}");
                return comp;
            }
        }

        // Также пробуем через GetComponent с именем типа
        try
        {
            System.Type tmpType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType != null)
            {
                Component tmpComp = obj.GetComponentInChildren(tmpType) as Component;
                if (tmpComp == null)
                {
                    tmpComp = obj.GetComponent(tmpType) as Component;
                }
                if (tmpComp != null)
                {
                    Debug.Log($"🔍 RocketExit: Найден TextMeshPro компонент через Type.GetType");
                    return tmpComp;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠ RocketExit: Не удалось найти TextMeshPro через Type.GetType: {e.Message}");
        }

        return null;
    }

    /// <summary>
    /// Устанавливает требуемую сумму денег
    /// </summary>
    public void SetRequiredMoney(int amount)
    {
        requiredMoney = amount;
    }

    /// <summary>
    /// Устанавливает ссылку на кошелек
    /// </summary>
    public void SetWallet(PlayerWallet playerWallet)
    {
        wallet = playerWallet;
    }
}

