using UnityEngine;
using System.Collections;

public class TeleportDoor : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform dungeonSpawnPoint;
    [SerializeField] private float holdTime = 3f; // Больше не используется, оставлено для совместимости
    [SerializeField] private float teleportDelay = 0.5f; // Задержка перед телепортацией при входе в триггер
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
    private Renderer doorRenderer;
    private Material originalMaterial;

    private void Start()
    {
        // Находим игрока
        playerController = FindObjectOfType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("❌ TeleportDoor: Не найден PlayerController!");
            return;
        }

        // Проверяем точку спавна
        if (dungeonSpawnPoint == null)
        {
            Debug.LogError("❌ TeleportDoor: Не назначена точка спавна в данже! Назначьте DungeonSpawnPoint в инспекторе.");
        }
        else
        {
            Debug.Log($"✅ TeleportDoor: Точка спавна назначена: {dungeonSpawnPoint.name} в позиции {dungeonSpawnPoint.position}");

            // Проверяем, что точка действительно под землей
            if (dungeonSpawnPoint.position.y >= 0)
            {
                Debug.LogWarning($"⚠ TeleportDoor: Точка спавна находится на поверхности (Y={dungeonSpawnPoint.position.y})! Переместите её под землю (Y < 0)");
            }
        }

        // Убеждаемся, что есть триггер коллайдер
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            // Добавляем BoxCollider по умолчанию
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            Debug.Log($"🔧 TeleportDoor: Добавлен BoxCollider (триггер) для {gameObject.name}");
        }
        else if (!collider.isTrigger)
        {
            // Делаем существующий коллайдер триггером
            collider.isTrigger = true;
            Debug.Log($"🔧 TeleportDoor: Коллайдер установлен как триггер для {gameObject.name}");
        }

        // Настраиваем визуальные эффекты
        doorRenderer = GetComponent<Renderer>();
        if (doorRenderer != null)
        {
            originalMaterial = doorRenderer.material;
        }

        Debug.Log($"✅ TeleportDoor инициализирован. Задержка телепортации: {teleportDelay} секунд");
    }

    private void Update()
    {
        if (isHolding)
        {
            holdProgress += Time.deltaTime;
            UpdateVisualFeedback();

            // Отладочная информация каждую секунду
            if (Mathf.FloorToInt(holdProgress) != Mathf.FloorToInt(holdProgress - Time.deltaTime))
            {
                Debug.Log($"🚪 Прогресс телепортации: {holdProgress:F1}/{holdTime:F1} секунд");
            }

            if (holdProgress >= holdTime)
            {
                Debug.Log("🚪 Время зажатия достигнуто! Начинаем телепортацию...");
                TeleportToDungeon();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other) && !isTeleporting)
        {
            isPlayerNearby = true;
            Debug.Log("🚪 Игрок вошел в дверь. Начинаем телепортацию в данж...");

            // Автоматически телепортируем при входе в триггер
            if (teleportDelay > 0f)
            {
                Debug.Log($"🚪 Задержка телепортации: {teleportDelay} секунд");
                StartCoroutine(DelayedTeleport(teleportDelay));
            }
            else
            {
                Debug.Log("🚪 Мгновенная телепортация");
                TeleportToDungeon();
            }
        }
        else if (IsPlayer(other) && isTeleporting)
        {
            Debug.Log("🚪 Игрок уже в процессе телепортации, пропускаем");
        }
    }

    private IEnumerator DelayedTeleport(float delay)
    {
        isTeleporting = true;
        yield return new WaitForSeconds(delay);
        TeleportToDungeon();
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
            Debug.Log("🚪 Игрок отошел от двери.");
        }
    }

    public void StartHold()
    {
        // Игнорируем зажатие клавиши - телепортация теперь автоматическая при входе в триггер
        // Оставляем метод для совместимости, но он больше не используется
        Debug.Log("ℹ TeleportDoor: StartHold() вызван, но игнорируется. Телепортация происходит автоматически при входе в триггер.");
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

    private void TeleportToDungeon()
    {
        if (dungeonSpawnPoint == null)
        {
            Debug.LogError("❌ TeleportDoor: Не назначена точка спавна в данже!");
            return;
        }

        if (playerController == null)
        {
            Debug.LogError("❌ TeleportDoor: PlayerController не найден!");
            return;
        }

        Debug.Log($"🚪 Телепортируем игрока из {playerController.transform.position} в {dungeonSpawnPoint.position}");

        // Определяем позицию для телепортации
        Vector3 teleportPosition;
        if (dungeonSpawnPoint != null && dungeonSpawnPoint.position.y < 0)
        {
            // Используем назначенную точку, если она под землей
            teleportPosition = dungeonSpawnPoint.position;
            Debug.Log($"✅ Используем назначенную точку спавна: {teleportPosition}");
        }
        else
        {
            // Создаем точку под землей относительно двери
            teleportPosition = transform.position;
            teleportPosition.y = -10f; // Принудительно под землю
            Debug.LogWarning($"⚠ Создаем точку под землей: {teleportPosition}");
        }

        // Используем TeleportPhysicsFix если доступен
        TeleportPhysicsFix physicsFix = playerController.GetComponent<TeleportPhysicsFix>();
        if (physicsFix != null)
        {
            Debug.Log("🔧 Используем TeleportPhysicsFix для телепортации");
            physicsFix.TeleportWithPhysicsFix(teleportPosition);
        }
        else
        {
            // Стандартная телепортация с отключением CharacterController
            CharacterController characterController = playerController.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null ? characterController.enabled : false;

            if (characterController != null)
            {
                characterController.enabled = false;
                Debug.Log("🔧 CharacterController отключен для телепортации");
            }

            // Телепортируем игрока
            playerController.transform.position = teleportPosition;

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
                Debug.Log("🔧 CharacterController включен обратно");
            }
        }

        if (dungeonSpawnPoint != null)
        {
            playerController.transform.rotation = dungeonSpawnPoint.rotation;
        }

        // Проверяем результат телепортации
        Debug.Log($"✅ Игрок телепортирован в позицию: {playerController.transform.position}");

        // Дополнительная проверка через небольшую задержку
        StartCoroutine(VerifyTeleportation(0.1f));

        // Сбрасываем состояние
        isHolding = false;
        holdProgress = 0f;
        isTeleporting = false;
        UpdateVisualFeedback();

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

        Debug.Log("🚪 Телепортация в данж завершена!");
    }

    private void UpdateVisualFeedback()
    {
        // Только логи, без визуальной индикации
        if (isHolding && holdProgress > 0f)
        {
            float progress = holdProgress / holdTime;
            if (Mathf.FloorToInt(holdProgress) != Mathf.FloorToInt(holdProgress - Time.deltaTime))
            {
                Debug.Log($"🚪 Прогресс телепортации: {progress:P0} ({holdProgress:F1}/{holdTime:F1}с)");
            }
        }
    }

    private void CreateProgressIndicator()
    {
        // Создаем простой индикатор прогресса
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = "ProgressIndicator";
        indicator.transform.SetParent(transform);
        indicator.transform.localPosition = Vector3.up * 2f;
        indicator.transform.localScale = Vector3.zero;

        // Убираем коллайдер
        DestroyImmediate(indicator.GetComponent<Collider>());

        // Настраиваем материал
        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        Material indicatorMaterial = new Material(Shader.Find("Standard"));
        indicatorMaterial.color = Color.green;
        indicatorMaterial.SetFloat("_Metallic", 0f);
        indicatorMaterial.SetFloat("_Smoothness", 0.5f);
        indicatorRenderer.material = indicatorMaterial;

        progressIndicator = indicator;
    }

    private bool IsPlayer(Collider other)
    {
        return ((1 << other.gameObject.layer) & playerLayer) != 0;
    }

    // Метод для настройки точки спавна в данже
    public void SetDungeonSpawnPoint(Transform spawnPoint)
    {
        dungeonSpawnPoint = spawnPoint;
    }

    // Метод для настройки времени зажатия
    public void SetHoldTime(float time)
    {
        holdTime = time;
    }

    // Метод для принудительной телепортации (для тестирования)
    [ContextMenu("Force Teleport to Dungeon")]
    public void ForceTeleportToDungeon()
    {
        Debug.Log("🧪 FORCE TELEPORT TO DUNGEON");

        if (playerController == null)
        {
            Debug.LogError("❌ PlayerController not found!");
            return;
        }

        Vector3 currentPos = playerController.transform.position;
        Vector3 undergroundPos = new Vector3(currentPos.x, -10f, currentPos.z);

        Debug.Log($"🧪 From: {currentPos}");
        Debug.Log($"🧪 To: {undergroundPos}");

        playerController.transform.position = undergroundPos;

        Debug.Log($"🧪 Final: {playerController.transform.position}");
        Debug.Log($"🧪 Underground: {(playerController.transform.position.y < 0 ? "YES" : "NO")}");
    }

    private IEnumerator VerifyTeleportation(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (playerController != null)
        {
            Vector3 currentPos = playerController.transform.position;
            Debug.Log($"🔍 Проверка через {delay}с: {currentPos}");

            // Если игрок не под землей, принудительно перемещаем
            if (currentPos.y >= 0)
            {
                Debug.LogWarning("⚠ Игрок был отброшен на поверхность! Принудительно перемещаем под землю...");

                Vector3 undergroundPos = new Vector3(currentPos.x, -10f, currentPos.z);
                playerController.transform.position = undergroundPos;

                Debug.Log($"🔧 Принудительно перемещен в: {playerController.transform.position}");
            }
            else
            {
                Debug.Log("✅ Игрок успешно находится под землей");
            }
        }
    }
}
