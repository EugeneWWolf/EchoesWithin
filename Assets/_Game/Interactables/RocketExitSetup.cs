using UnityEngine;

/// <summary>
/// Утилита для настройки существующего GameObject как ракеты для выхода из игры
/// Просто прикрепите этот скрипт к кубу (или любому другому объекту) на сцене
/// </summary>
public class RocketExitSetup : MonoBehaviour
{
    [Header("Rocket Settings")]
    [SerializeField] private int requiredMoney = 500;

    [ContextMenu("Setup This Object as Rocket")]
    public void SetupRocketExit()
    {
        Debug.Log($"🚀 Настраиваем {gameObject.name} как ракету для выхода...");

        // Проверяем, есть ли уже компонент RocketExit
        RocketExit rocketExit = GetComponent<RocketExit>();
        if (rocketExit == null)
        {
            // Добавляем компонент RocketExit
            rocketExit = gameObject.AddComponent<RocketExit>();
            Debug.Log("✅ Добавлен компонент RocketExit");
        }

        // Настраиваем требуемую сумму
        rocketExit.SetRequiredMoney(requiredMoney);

        // Устанавливаем слой Interactable
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer != -1)
        {
            gameObject.layer = interactableLayer;
            Debug.Log($"✅ Установлен слой Interactable для {gameObject.name}");
        }

        // Проверяем наличие коллайдера
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            // Добавляем BoxCollider по умолчанию
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            Debug.Log($"✅ Добавлен BoxCollider (триггер) для {gameObject.name}");
        }
        else if (!collider.isTrigger)
        {
            // Делаем существующий коллайдер триггером
            collider.isTrigger = true;
            Debug.Log($"✅ Коллайдер установлен как триггер для {gameObject.name}");
        }

        // Привязываем кошелек
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            PlayerWallet wallet = playerController.GetComponent<PlayerWallet>();
            if (wallet != null)
            {
                rocketExit.SetWallet(wallet);
                Debug.Log("✅ RocketExit автоматически привязан к PlayerWallet");
            }
            else
            {
                Debug.LogWarning("⚠ PlayerWallet не найден на игроке. RocketExit найдет его автоматически при старте.");
            }
        }

        Debug.Log($"✅ {gameObject.name} успешно настроен как ракета!");
        Debug.Log($"💰 Требуемая сумма: ${requiredMoney}");
        Debug.Log("💡 Теперь вы можете удалить этот компонент RocketExitSetup - он больше не нужен");
    }

    private void Start()
    {
        // Автоматически настраиваем при старте, если компонент RocketExit еще не добавлен
        if (GetComponent<RocketExit>() == null)
        {
            SetupRocketExit();
        }
    }
}

