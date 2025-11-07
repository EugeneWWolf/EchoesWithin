using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

/// <summary>
/// Компонент для ракеты - финальной цели игры
/// Игрок должен собрать достаточно денег и взаимодействовать с ракетой, чтобы покинуть планету
/// </summary>
public class RocketExit : MonoBehaviour
{
    [Header("Exit Settings")]
    [SerializeField] private int requiredMoney = 500;
    [SerializeField] private string insufficientFundsMessage = "You need ${0} to leave this planet! You have ${1}.";

    [Header("References")]
    [SerializeField] private PlayerWallet wallet;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject errorMessagePrefab; // Опциональный префаб для сообщения об ошибке
    [SerializeField] private float errorMessageDuration = 3f;

    private GameObject currentErrorMessage; // Текущее сообщение об ошибке на экране

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
    }

    /// <summary>
    /// Вызывается при взаимодействии с ракетой
    /// </summary>
    public bool TryExit()
    {
        if (wallet == null)
        {
            Debug.LogError("❌ RocketExit: PlayerWallet не найден!");
            ShowErrorMessage("Error: Wallet not found!");
            return false;
        }

        int currentBalance = wallet.Balance;

        if (currentBalance >= requiredMoney)
        {
            // Игрок собрал достаточно денег - завершаем игру
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

