using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Компонент для отображения цены над предметом
/// </summary>
public class ItemPriceDisplay : MonoBehaviour
{
    [Header("Display Settings")]
    [Tooltip("Смещение цены относительно центра предмета (по Y)")]
    [SerializeField] private float offsetY = 1.5f;

    [Tooltip("Размер шрифта")]
    [SerializeField] private int fontSize = 30;

    [Tooltip("Цвет текста")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Цвет обводки")]
    [SerializeField] private Color outlineColor = Color.black;

    [Tooltip("Показывать только когда игрок в магазине")]
    [SerializeField] private bool onlyInShop = false;

    private GameObject priceCanvasObj;
    private Text priceText;
    private Item item;
    private ShopZone shopZone;
    private bool isInitialized = false;

    private void Awake()
    {
        item = GetComponent<Item>();
        if (item == null)
        {
            Debug.LogWarning($"⚠ ItemPriceDisplay: Предмет {gameObject.name} не имеет компонента Item!");
            return;
        }
    }

    private void Start()
    {
        Debug.Log($"🔄 ItemPriceDisplay.Start() для {gameObject.name}");
        if (!isInitialized)
        {
            Initialize();
        }
    }

    private void OnEnable()
    {
        Debug.Log($"🔄 ItemPriceDisplay.OnEnable() для {gameObject.name}, активен={gameObject.activeInHierarchy}");
        if (!isInitialized && gameObject.activeInHierarchy)
        {
            // Небольшая задержка для гарантии, что Item компонент уже инициализирован
            StartCoroutine(DelayedInitialize());
        }
        else if (isInitialized && priceCanvasObj != null)
        {
            // Если уже инициализирован, просто показываем Canvas
            priceCanvasObj.SetActive(true);
        }
    }

    private System.Collections.IEnumerator DelayedInitialize()
    {
        // Ждем один кадр, чтобы убедиться, что все компоненты инициализированы
        yield return null;
        if (!isInitialized && gameObject.activeInHierarchy)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        if (isInitialized) return;
        if (item == null) return;

        Debug.Log($"✅ ItemPriceDisplay.Initialize() для {gameObject.name}");
        shopZone = FindNearestShopZone();
        CreatePriceDisplay();
        UpdatePriceDisplay();
        isInitialized = true;
    }

    private ShopZone FindNearestShopZone()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 50f);
        foreach (var col in colliders)
        {
            if (col.TryGetComponent<ShopZone>(out var zone))
            {
                return zone;
            }
        }
        return null;
    }

    private void CreatePriceDisplay()
    {
        if (priceCanvasObj != null) return;

        Debug.Log($"🖼 ItemPriceDisplay.CreatePriceDisplay() для {gameObject.name}");

        // Создаем Canvas в мировом пространстве
        priceCanvasObj = new GameObject("PriceDisplayCanvas_" + gameObject.name);

        // НЕ ДЕЛАЕМ ДОЧЕРНИМ! Чтобы избежать наследования localScale от предмета.
        // Вместо этого позиция и ротация будут обновляться в LateUpdate.
        // priceCanvasObj.transform.SetParent(transform); // УБРАНО

        // Устанавливаем начальную мировую позицию
        Vector3 worldPosition = transform.position + Vector3.up * offsetY;
        priceCanvasObj.transform.position = worldPosition;
        priceCanvasObj.transform.localRotation = Quaternion.identity;
        priceCanvasObj.SetActive(true);

        Canvas canvas = priceCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        // НЕ используем CanvasScaler для WorldSpace - он может мешать
        // CanvasScaler scaler = priceCanvasObj.AddComponent<CanvasScaler>();

        GraphicRaycaster raycaster = priceCanvasObj.AddComponent<GraphicRaycaster>();

        // Настраиваем размер Canvas для WorldSpace
        RectTransform canvasRect = priceCanvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200f, 60f); // Размер Canvas
                                                       // Масштаб определяет размер в мировых единицах Unity (теперь фиксирован, без наследования)
        canvasRect.localScale = Vector3.one * 0.02f;
        // НЕ устанавливаем localPosition на RectTransform - позиция контролируется через transform.localPosition

        // Фон удален - оставляем только белый текст с ценой

        // Создаем Text
        GameObject textObj = new GameObject("PriceText");
        textObj.transform.SetParent(priceCanvasObj.transform, false);
        textObj.SetActive(true); // Явно активируем

        priceText = textObj.AddComponent<Text>();

        // Пытаемся получить шрифт
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (font != null)
        {
            priceText.font = font;
        }

        // Устанавливаем размер шрифта - ПРИНУДИТЕЛЬНО используем одинаковый размер для ВСЕХ цен
        // Игнорируем fontSize из инспектора, используем фиксированное значение
        const int FIXED_FONT_SIZE = 30; // Единый размер для всех цен
        priceText.fontSize = FIXED_FONT_SIZE;
        priceText.color = textColor;
        priceText.alignment = TextAnchor.MiddleCenter;
        priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
        priceText.verticalOverflow = VerticalWrapMode.Overflow;
        priceText.fontStyle = FontStyle.Bold;
        priceText.resizeTextForBestFit = false; // Отключаем автоподбор
        priceText.resizeTextMinSize = FIXED_FONT_SIZE; // Фиксированные значения
        priceText.resizeTextMaxSize = FIXED_FONT_SIZE; // Фиксированные значения
        priceText.text = "Loading..."; // Временный текст для проверки видимости

        // Настраиваем RectTransform для Text
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localPosition = Vector3.zero;
        textRect.localScale = Vector3.one;
    }

    // Удаляем все Outline компоненты, если они есть (чтобы избежать дублирования)

    private void UpdatePriceDisplay()
    {
        if (priceText == null)
        {
            Debug.LogWarning($"⚠ ItemPriceDisplay: priceText == null для {gameObject.name}");
            return;
        }

        if (item == null)
        {
            Debug.LogWarning($"⚠ ItemPriceDisplay: item == null для {gameObject.name}");
            priceText.text = "???";
            return;
        }

        int displayPrice = item.price;

        // Если есть ShopZone, применяем множитель цены
        if (shopZone != null)
        {
            displayPrice = shopZone.GetPurchasePrice(item.price);
        }

        string priceString = $"💰 {displayPrice}";
        priceText.text = priceString;

        // Обновляем размер шрифта - ПРИНУДИТЕЛЬНО используем одинаковый размер для ВСЕХ цен
        // Игнорируем fontSize из инспектора, используем фиксированное значение
        const int FIXED_FONT_SIZE = 30; // Единый размер для всех цен
        priceText.fontSize = FIXED_FONT_SIZE;
        priceText.resizeTextMinSize = FIXED_FONT_SIZE;
        priceText.resizeTextMaxSize = FIXED_FONT_SIZE;

        // Убеждаемся, что текст активен и видим
        if (!priceText.gameObject.activeSelf)
        {
            priceText.gameObject.SetActive(true);
        }

        Debug.Log($"💰 ItemPriceDisplay: Цена установлена '{priceText.text}' для {gameObject.name}, item.price={item.price}, textObj.active={priceText.gameObject.activeSelf}");
    }

    /// <summary>
    /// Публичный метод для установки ShopZone (если не найдена автоматически)
    /// </summary>
    public void SetShopZone(ShopZone zone)
    {
        Debug.Log($"🎯 ItemPriceDisplay.SetShopZone() для {gameObject.name}, zone: {zone != null}");
        shopZone = zone;
        if (isInitialized && priceText != null)
        {
            UpdatePriceDisplay();
        }
    }

    /// <summary>
    /// Принудительная инициализация для компонентов, добавленных к неактивным объектам
    /// </summary>
    public void ForceInitialize(ShopZone zone = null)
    {
        Debug.Log($"🔨 ItemPriceDisplay.ForceInitialize() для {gameObject.name}");
        if (zone != null)
        {
            shopZone = zone;
        }
        Initialize();
    }

    /// <summary>
    /// Публичный метод для обновления цены вручную
    /// </summary>
    public void RefreshPrice()
    {
        UpdatePriceDisplay();
    }

    private void LateUpdate()
    {
        if (!isInitialized || priceCanvasObj == null || !gameObject.activeInHierarchy) return;

        // Фиксируем позицию Canvas в мировых координатах относительно предмета
        // Это предотвращает накопление смещения и подъем цен
        Vector3 targetWorldPosition = transform.position + Vector3.up * offsetY;

        // Используем сравнение с небольшим допуском для float
        if (Vector3.Distance(priceCanvasObj.transform.position, targetWorldPosition) > 0.001f)
        {
            priceCanvasObj.transform.position = targetWorldPosition;
        }

        // Гарантируем одинаковый размер шрифта для всех цен
        if (priceText != null)
        {
            const int FIXED_FONT_SIZE = 30;
            if (priceText.fontSize != FIXED_FONT_SIZE)
            {
                priceText.fontSize = FIXED_FONT_SIZE;
                priceText.resizeTextMinSize = FIXED_FONT_SIZE;
                priceText.resizeTextMaxSize = FIXED_FONT_SIZE;
            }
        }

        // Поворачиваем текст лицом к камере
        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - priceCanvasObj.transform.position;
            directionToCamera.y = 0; // Сохраняем вертикальную ориентацию
            if (directionToCamera.sqrMagnitude > 0.01f)
            {
                priceCanvasObj.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }

        // Показываем/скрываем в зависимости от настроек
        if (onlyInShop)
        {
            bool shouldShow = IsPlayerInShop();
            if (priceCanvasObj.activeSelf != shouldShow)
            {
                priceCanvasObj.SetActive(shouldShow);
            }
        }
    }

    private bool IsPlayerInShop()
    {
        if (shopZone == null) return false;
        return shopZone.IsPlayerInside;
    }

    private void OnDestroy()
    {
        if (priceCanvasObj != null)
        {
            Destroy(priceCanvasObj);
        }
    }
}

