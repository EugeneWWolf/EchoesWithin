using UnityEngine;
using UnityEngine.UI;

public class TeleportProgressUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject progressPanel;
    [SerializeField] private Image progressBar;
    [SerializeField] private Text progressText;
    [SerializeField] private Text instructionText;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 2f;

    private bool isVisible = false;
    private float currentProgress = 0f;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Находим или создаем CanvasGroup для плавного появления/исчезновения
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Создаем UI элементы если они не назначены
        if (progressPanel == null)
        {
            CreateProgressUI();
        }

        // Скрываем UI изначально
        SetVisible(false);
    }

    private void Update()
    {
        if (isVisible)
        {
            // Плавно показываем UI
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, fadeSpeed * Time.deltaTime);
        }
        else
        {
            // Плавно скрываем UI
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, fadeSpeed * Time.deltaTime);
        }
    }

    public void ShowProgress(float progress, string instruction = "Зажмите кнопку взаимодействия")
    {
        currentProgress = Mathf.Clamp01(progress);
        isVisible = true;

        if (progressBar != null)
        {
            progressBar.fillAmount = currentProgress;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(currentProgress * 100)}%";
        }

        if (instructionText != null)
        {
            instructionText.text = instruction;
        }
    }

    public void HideProgress()
    {
        isVisible = false;
        currentProgress = 0f;

        if (progressBar != null)
        {
            progressBar.fillAmount = 0f;
        }

        if (progressText != null)
        {
            progressText.text = "0%";
        }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (!visible)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void CreateProgressUI()
    {
        // Создаем Canvas если его нет
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Создаем панель прогресса
        GameObject panelObj = new GameObject("TeleportProgressPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.3f);
        panelRect.anchorMax = new Vector2(0.5f, 0.3f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(300, 100);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);

        // Создаем текст инструкции
        GameObject instructionObj = new GameObject("InstructionText");
        instructionObj.transform.SetParent(panelObj.transform, false);

        RectTransform instructionRect = instructionObj.AddComponent<RectTransform>();
        instructionRect.anchorMin = new Vector2(0, 0.6f);
        instructionRect.anchorMax = new Vector2(1, 1);
        instructionRect.offsetMin = Vector2.zero;
        instructionRect.offsetMax = Vector2.zero;

        Text instructionTextComponent = instructionObj.AddComponent<Text>();
        instructionTextComponent.text = "Зажмите кнопку взаимодействия";
        instructionTextComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        instructionTextComponent.fontSize = 16;
        instructionTextComponent.color = Color.white;
        instructionTextComponent.alignment = TextAnchor.MiddleCenter;

        // Создаем полосу прогресса
        GameObject progressBarObj = new GameObject("ProgressBar");
        progressBarObj.transform.SetParent(panelObj.transform, false);

        RectTransform progressBarRect = progressBarObj.AddComponent<RectTransform>();
        progressBarRect.anchorMin = new Vector2(0.1f, 0.2f);
        progressBarRect.anchorMax = new Vector2(0.9f, 0.5f);
        progressBarRect.offsetMin = Vector2.zero;
        progressBarRect.offsetMax = Vector2.zero;

        Image progressBarImage = progressBarObj.AddComponent<Image>();
        progressBarImage.color = Color.green;
        progressBarImage.type = Image.Type.Filled;
        progressBarImage.fillMethod = Image.FillMethod.Horizontal;

        // Создаем текст прогресса
        GameObject progressTextObj = new GameObject("ProgressText");
        progressTextObj.transform.SetParent(panelObj.transform, false);

        RectTransform progressTextRect = progressTextObj.AddComponent<RectTransform>();
        progressTextRect.anchorMin = new Vector2(0, 0);
        progressTextRect.anchorMax = new Vector2(1, 0.3f);
        progressTextRect.offsetMin = Vector2.zero;
        progressTextRect.offsetMax = Vector2.zero;

        Text progressTextComponent = progressTextObj.AddComponent<Text>();
        progressTextComponent.text = "0%";
        progressTextComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        progressTextComponent.fontSize = 14;
        progressTextComponent.color = Color.white;
        progressTextComponent.alignment = TextAnchor.MiddleCenter;

        // Присваиваем ссылки
        progressPanel = panelObj;
        progressBar = progressBarImage;
        progressText = progressTextComponent;
        instructionText = instructionTextComponent;
    }
}
