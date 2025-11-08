using UnityEngine;
using System.Collections;

public class TeleportZone : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform returnSpawnPoint;
    [SerializeField] private float holdTime = 3f; // Больше не используется, оставлено для совместимости
    [SerializeField] private float teleportDelay = 0.5f; // Задержка перед телепортацией при входе в триггер
    [SerializeField] private float teleportCooldown = 2f; // Кулдаун после телепортации (в секундах)
    [SerializeField] private LayerMask playerLayer = 1; // Default layer

    [Header("Visual Feedback")]
    [SerializeField] private GameObject progressIndicator;
    [SerializeField] private Material progressMaterial;
    [SerializeField] private TeleportProgressUI progressUI;

    private bool isPlayerNearby = false;
    private bool isHolding = false;
    private float holdProgress = 0f;
    private bool isTeleporting = false; // Флаг, чтобы избежать повторной телепортации
    private PlayerController playerController;
    private Renderer zoneRenderer;
    private Material originalMaterial;

    private void Start()
    {
        // Находим игрока
        playerController = FindFirstObjectByType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("❌ TeleportZone: Не найден PlayerController!");
            return;
        }

        // Убеждаемся, что есть триггер коллайдер
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            // Добавляем BoxCollider по умолчанию
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            Debug.Log($"🔧 TeleportZone: Добавлен BoxCollider (триггер) для {gameObject.name}");
        }
        else if (!collider.isTrigger)
        {
            // Делаем существующий коллайдер триггером
            collider.isTrigger = true;
            Debug.Log($"🔧 TeleportZone: Коллайдер установлен как триггер для {gameObject.name}");
        }

        // Настраиваем визуальные эффекты
        zoneRenderer = GetComponent<Renderer>();
        if (zoneRenderer != null)
        {
            originalMaterial = zoneRenderer.material;
        }

        // Устанавливаем кулдаун в общий менеджер
        TeleportCooldownManager.SetCooldown(teleportCooldown);

        Debug.Log($"✅ TeleportZone инициализирован. Задержка телепортации: {teleportDelay} секунд, кулдаун: {teleportCooldown} секунд");
    }

    private void Update()
    {
        if (isHolding)
        {
            holdProgress += Time.deltaTime;
            UpdateVisualFeedback();

            if (holdProgress >= holdTime)
            {
                TeleportToSurface();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other) && !isTeleporting)
        {
            // Проверяем кулдаун через общий менеджер
            if (!TeleportCooldownManager.CanTeleport())
            {
                float remainingCooldown = TeleportCooldownManager.GetRemainingCooldown();
                Debug.Log($"⏳ TeleportZone: Кулдаун активен. Осталось {remainingCooldown:F1} секунд");
                return;
            }

            isPlayerNearby = true;
            Debug.Log("🔄 Игрок вошел в зону возврата. Начинаем возврат на поверхность...");

            // Автоматически телепортируем при входе в триггер
            if (teleportDelay > 0f)
            {
                Debug.Log($"🔄 Задержка телепортации: {teleportDelay} секунд");
                StartCoroutine(DelayedTeleport(teleportDelay));
            }
            else
            {
                Debug.Log("🔄 Мгновенная телепортация");
                TeleportToSurface();
            }
        }
        else if (IsPlayer(other) && isTeleporting)
        {
            Debug.Log("🔄 Игрок уже в процессе телепортации, пропускаем");
        }
    }

    private IEnumerator DelayedTeleport(float delay)
    {
        isTeleporting = true;
        yield return new WaitForSeconds(delay);
        TeleportToSurface();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            isPlayerNearby = false;
            isHolding = false;
            holdProgress = 0f;
            isTeleporting = false; // Сбрасываем флаг при выходе
            UpdateVisualFeedback();
            Debug.Log("🔄 Игрок отошел от зоны возврата.");
        }
    }

    public void StartHold()
    {
        // Игнорируем зажатие клавиши - телепортация теперь автоматическая при входе в триггер
        // Оставляем метод для совместимости, но он больше не используется
        Debug.Log("ℹ TeleportZone: StartHold() вызван, но игнорируется. Телепортация происходит автоматически при входе в триггер.");
    }

    public void StopHold()
    {
        // Игнорируем остановку зажатия - телепортация теперь автоматическая при входе в триггер
        // Оставляем метод для совместимости, но он больше не используется
        if (isHolding)
        {
            isHolding = false;
            holdProgress = 0f;
            UpdateVisualFeedback();
        }
    }

    private void TeleportToSurface()
    {
        if (returnSpawnPoint == null)
        {
            Debug.LogError("❌ TeleportZone: Не назначена точка возврата на поверхность!");
            return;
        }

        if (playerController == null)
        {
            Debug.LogError("❌ TeleportZone: PlayerController не найден!");
            return;
        }

        Debug.Log($"🔄 Телепортируем игрока из {playerController.transform.position} в {returnSpawnPoint.position}");

        // Используем TeleportPhysicsFix если доступен
        TeleportPhysicsFix physicsFix = playerController.GetComponent<TeleportPhysicsFix>();
        if (physicsFix != null)
        {
            Debug.Log("🔧 Используем TeleportPhysicsFix для возврата");
            physicsFix.TeleportWithPhysicsFix(returnSpawnPoint.position);
        }
        else
        {
            // Стандартная телепортация с отключением CharacterController
            CharacterController characterController = playerController.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null ? characterController.enabled : false;

            if (characterController != null)
            {
                characterController.enabled = false;
                Debug.Log("🔧 CharacterController отключен для возврата");
            }

            // Телепортируем игрока
            playerController.transform.position = returnSpawnPoint.position;

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
                Debug.Log("🔧 CharacterController включен обратно");
            }
        }

        playerController.transform.rotation = returnSpawnPoint.rotation;

        // Проверяем результат телепортации
        Debug.Log($"✅ Игрок возвращен в позицию: {playerController.transform.position}");

        // Дополнительная проверка через небольшую задержку
        StartCoroutine(VerifyReturnTeleportation(0.1f));

        // Сбрасываем состояние
        isHolding = false;
        holdProgress = 0f;
        isTeleporting = false;
        UpdateVisualFeedback();

        // Регистрируем телепортацию в общем менеджере кулдауна
        TeleportCooldownManager.RegisterTeleport();
        Debug.Log($"⏳ TeleportZone: Кулдаун установлен на {teleportCooldown} секунд");

        // Уведомляем PlayerInteraction о завершении
        if (playerController != null)
        {
            // Получаем PlayerInteraction через рефлексию
            var interactionField = typeof(PlayerController).GetField("interaction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (interactionField != null)
            {
                var interaction = interactionField.GetValue(playerController) as PlayerInteraction;
                if (interaction != null)
                {
                    interaction.ResetHoldState();
                }
            }
        }

        Debug.Log("🔄 Возврат на поверхность завершен!");
    }

    private void UpdateVisualFeedback()
    {
        // Только логи, без визуальной индикации
        if (isHolding && holdProgress > 0f)
        {
            float progress = holdProgress / holdTime;
            if (Mathf.FloorToInt(holdProgress) != Mathf.FloorToInt(holdProgress - Time.deltaTime))
            {
                Debug.Log($"🔄 Прогресс возврата: {progress:P0} ({holdProgress:F1}/{holdTime:F1}с)");
            }
        }
    }

    private bool IsPlayer(Collider other)
    {
        return ((1 << other.gameObject.layer) & playerLayer) != 0;
    }

    // Метод для настройки точки возврата
    public void SetReturnSpawnPoint(Transform spawnPoint)
    {
        returnSpawnPoint = spawnPoint;
    }

    // Метод для настройки времени зажатия
    public void SetHoldTime(float time)
    {
        holdTime = time;
    }

    private IEnumerator VerifyReturnTeleportation(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (playerController != null)
        {
            Vector3 currentPos = playerController.transform.position;
            Debug.Log($"🔍 Проверка возврата через {delay}с: {currentPos}");

            // Если игрок не на поверхности, принудительно перемещаем
            if (currentPos.y < 0)
            {
                Debug.LogWarning("⚠ Игрок остался под землей! Принудительно перемещаем на поверхность...");

                Vector3 surfacePos = new Vector3(currentPos.x, 0f, currentPos.z);
                playerController.transform.position = surfacePos;

                Debug.Log($"🔧 Принудительно перемещен на поверхность: {playerController.transform.position}");
            }
            else
            {
                Debug.Log("✅ Игрок успешно возвращен на поверхность");
            }
        }
    }
}
