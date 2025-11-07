using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI компонент для отображения экрана смерти
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deathScreenPanel;
    [SerializeField] private Text deathText;
    [SerializeField] private CanvasGroup canvasGroup; // Для управления видимостью без отключения GameObject

    [Header("Display Settings")]
    [SerializeField] private string deathMessage = "Вы умерли";
    [SerializeField] private Color textColor = Color.red;
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private int fontSize = 72; // Размер шрифта по умолчанию
    [SerializeField] private bool centerText = true; // Центрировать текст

    private bool isShowing = false;

    private void Awake()
    {
        // Автоматически ищем панель, если не назначена
        if (deathScreenPanel == null)
        {
            // Проверяем, не является ли сам GameObject панелью (Canvas, Image, RectTransform)
            Canvas canvas = GetComponent<Canvas>();
            Image image = GetComponent<Image>();
            RectTransform rectTransform = GetComponent<RectTransform>();

            if (canvas != null || image != null || rectTransform != null)
            {
                // Если скрипт висит на UI элементе, используем сам GameObject как панель
                deathScreenPanel = gameObject;
            }
            else
            {
                // Ищем панель среди дочерних объектов
                deathScreenPanel = transform.Find("DeathScreenPanel")?.gameObject;
                if (deathScreenPanel == null)
                {
                    // Ищем по имени среди всех дочерних объектов
                    foreach (Transform child in transform)
                    {
                        if (child.name.Contains("Death") || child.name.Contains("Panel"))
                        {
                            deathScreenPanel = child.gameObject;
                            break;
                        }
                    }
                }
            }
        }

        // Автоматически ищем текст, если не назначен
        if (deathText == null)
        {
            if (deathScreenPanel != null)
            {
                deathText = deathScreenPanel.GetComponentInChildren<Text>();
            }
            else
            {
                // Если панель не найдена, ищем текст среди дочерних объектов
                deathText = GetComponentInChildren<Text>();
            }
        }

        // Ищем CanvasGroup для управления видимостью
        if (canvasGroup == null)
        {
            if (deathScreenPanel != null)
            {
                canvasGroup = deathScreenPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = deathScreenPanel.AddComponent<CanvasGroup>();
                }
            }
            else
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null && (GetComponent<Canvas>() != null || GetComponent<Image>() != null))
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        // Скрываем экран смерти при старте
        HideDeathScreen();
    }

    private void Start()
    {
        // Дополнительная проверка в Start() для гарантии
        HideDeathScreen();
    }

    private void OnEnable()
    {
        // Скрываем при включении компонента
        HideDeathScreen();
    }

    /// <summary>
    /// Показать экран смерти
    /// </summary>
    public void ShowDeathScreen()
    {
        if (isShowing) return;

        isShowing = true;

        // Пытаемся найти панель, если не назначена
        if (deathScreenPanel == null)
        {
            // Проверяем, не является ли сам GameObject панелью
            Canvas canvas = GetComponent<Canvas>();
            Image image = GetComponent<Image>();
            RectTransform rectTransform = GetComponent<RectTransform>();

            if (canvas != null || image != null || rectTransform != null)
            {
                deathScreenPanel = gameObject;
            }
            else
            {
                deathScreenPanel = transform.Find("DeathScreenPanel")?.gameObject;
            }
        }

        // Убеждаемся, что CanvasGroup найден
        if (canvasGroup == null && deathScreenPanel != null)
        {
            canvasGroup = deathScreenPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = deathScreenPanel.AddComponent<CanvasGroup>();
            }
        }

        if (deathScreenPanel != null)
        {
            // Если панель - это сам GameObject с компонентом, используем CanvasGroup
            if (deathScreenPanel == gameObject && canvasGroup != null)
            {
                deathScreenPanel.SetActive(true); // Убеждаемся, что активен
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                deathScreenPanel.SetActive(true);
            }

            // Настраиваем панель на весь экран для центрирования текста
            RectTransform panelRect = deathScreenPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;
                panelRect.anchoredPosition = Vector2.zero;
            }

            Debug.Log($"💀 DeathScreenUI: Панель активирована: {deathScreenPanel.name}");
        }
        else
        {
            Debug.LogWarning("⚠ DeathScreenUI: Панель не найдена!");
        }

        // Пытаемся найти текст, если не назначен
        if (deathText == null && deathScreenPanel != null)
        {
            deathText = deathScreenPanel.GetComponentInChildren<Text>();
        }

        if (deathText != null)
        {
            deathText.text = deathMessage;
            deathText.color = textColor;
            deathText.fontSize = fontSize;

            // Центрируем текст
            if (centerText)
            {
                deathText.alignment = TextAnchor.MiddleCenter;
                deathText.horizontalOverflow = HorizontalWrapMode.Overflow;
                deathText.verticalOverflow = VerticalWrapMode.Overflow;

                // Настраиваем RectTransform для центрирования
                RectTransform textRect = deathText.GetComponent<RectTransform>();
                if (textRect != null)
                {
                    textRect.anchorMin = new Vector2(0.5f, 0.5f);
                    textRect.anchorMax = new Vector2(0.5f, 0.5f);
                    textRect.pivot = new Vector2(0.5f, 0.5f);
                    textRect.anchoredPosition = Vector2.zero;
                }
            }

            Debug.Log($"💀 DeathScreenUI: Текст установлен: {deathMessage}, размер: {fontSize}");
        }
        else
        {
            Debug.LogWarning("⚠ DeathScreenUI: Текст не найден!");
        }

        Debug.Log("💀 DeathScreenUI: Экран смерти показан");
    }

    /// <summary>
    /// Скрыть экран смерти
    /// </summary>
    public void HideDeathScreen()
    {
        isShowing = false;

        // Убеждаемся, что CanvasGroup найден
        if (canvasGroup == null && deathScreenPanel != null)
        {
            canvasGroup = deathScreenPanel.GetComponent<CanvasGroup>();
        }
        if (canvasGroup == null)
        {
            Canvas canvas = GetComponent<Canvas>();
            Image image = GetComponent<Image>();
            if (canvas != null || image != null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        if (deathScreenPanel != null)
        {
            // Если панель - это сам GameObject с компонентом, используем CanvasGroup для скрытия
            if (deathScreenPanel == gameObject && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                deathScreenPanel.SetActive(false);
            }
        }
        else
        {
            // Если панель не назначена, пытаемся найти её снова
            Canvas canvas = GetComponent<Canvas>();
            Image image = GetComponent<Image>();
            RectTransform rectTransform = GetComponent<RectTransform>();

            if (canvas != null || image != null || rectTransform != null)
            {
                // Если скрипт висит на UI элементе, используем CanvasGroup
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
                else
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }
                deathScreenPanel = gameObject;
            }
            else
            {
                deathScreenPanel = transform.Find("DeathScreenPanel")?.gameObject;
                if (deathScreenPanel != null)
                {
                    deathScreenPanel.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Показать экран смерти на определенное время
    /// </summary>
    public IEnumerator ShowDeathScreenForDuration(float duration)
    {
        ShowDeathScreen();
        yield return new WaitForSeconds(duration);
        HideDeathScreen();
    }

    /// <summary>
    /// Получить длительность отображения
    /// </summary>
    public float GetDisplayDuration() => displayDuration;

    /// <summary>
    /// Проверка, показывается ли экран смерти
    /// </summary>
    public bool IsShowing() => isShowing;
}

