using UnityEngine;
using System.Collections;
using System.Reflection;

public class TeleportZone : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform returnSpawnPoint;
    [Header("Куда вести после выхода")]
    [SerializeField] private DungeonReturnExitMode exitDestination = DungeonReturnExitMode.FixedReturnSpawn;
    [Tooltip("Для RandomFloorInProceduralDungeon. Если пусто — ищется в сцене.")]
    [SerializeField] private ProceduralDungeonGenerator proceduralDungeon;
    [Tooltip("Для RandomHorizontalAroundReturnSpawn — радиус в XZ от returnSpawnPoint")]
    [SerializeField] private float randomSurfaceHorizontalRadius = 8f;
    [SerializeField] private float randomSurfaceRaycastStartHeight = 40f;
    [SerializeField] private LayerMask randomSurfaceGroundMask = 1;

    [SerializeField] private float holdTime = 3f; // Больше не используется, оставлено для совместимости
    [SerializeField] private float teleportDelay = 0.5f; // Задержка перед телепортацией при входе в триггер
    [SerializeField] private float teleportCooldown = 2f; // Кулдаун после телепортации (в секундах)
    [SerializeField] private LayerMask playerLayer = 1; // Default layer

    [Header("Visual Feedback")]
    [SerializeField] private GameObject progressIndicator;
    [SerializeField] private Material progressMaterial;
    [SerializeField] private TeleportProgressUI progressUI;

    [Header("Маркер на полу")]
    [Tooltip("Тонкий диск под зоной (луч вниз от позиции зоны), чтобы выход было видно на земле")]
    [SerializeField] private bool showGroundMarker = true;
    [SerializeField] private float groundMarkerRadius = 1.75f;
    [SerializeField] private float groundMarkerRaycastUp = 4f;
    [SerializeField] private float groundMarkerRaycastDown = 25f;
    [SerializeField] private LayerMask groundMarkerRaycastMask = ~0;
    [SerializeField] private Color groundMarkerColor = new Color(0.2f, 0.85f, 1f, 0.9f);

    [Header("World Sign")]
    [Tooltip("Показывать подпись над зоной телепортации")]
    [SerializeField] private bool showSign = true;
    [Tooltip("Текст подписи")]
    [SerializeField] private string signText = "Выход из данжа";
    [Tooltip("Высота подписи")]
    [SerializeField] private float signHeight = 2f;

    private bool _lastTeleportKeptPlayerUnderground;

    private bool isPlayerNearby = false;
    private Coroutine _delayedTeleportCoroutine;
    private WorldSign worldSign;
    private bool isHolding = false;
    private float holdProgress = 0f;
    private bool isTeleporting = false; // Флаг, чтобы избежать повторной телепортации
    private PlayerController playerController;
    private Renderer zoneRenderer;
    private Material originalMaterial;

    private void Start()
    {
        // Игрок может появиться позже генератора данжа — не прерываем Start, иначе не создаются маркер и подпись.
        TryCachePlayerController();
        if (playerController == null)
            Debug.LogWarning("TeleportZone: PlayerController не найден на Start — визуалы зоны всё равно создаются; игрок будет найден при телепорте.");

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

        // Создаем подпись, если включена
        if (showSign)
        {
            worldSign = gameObject.AddComponent<WorldSign>();
            // Используем рефлексию для установки параметров
            SetSignProperties(worldSign, signText, signHeight);
        }

        if (showGroundMarker)
            CreateGroundExitMarker();

        Debug.Log($"✅ TeleportZone инициализирован. Задержка телепортации: {teleportDelay} секунд, кулдаун: {teleportCooldown} секунд");
    }

    private void CreateGroundExitMarker()
    {
        int mask = groundMarkerRaycastMask.value == 0 ? Physics.DefaultRaycastLayers : groundMarkerRaycastMask.value;
        Vector3 origin = transform.position + Vector3.up * groundMarkerRaycastUp;
        Vector3 discWorld = transform.position;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundMarkerRaycastUp + groundMarkerRaycastDown, mask,
                QueryTriggerInteraction.Ignore))
            discWorld = hit.point + Vector3.up * 0.03f;

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "ExitGroundMarker";
        Destroy(disc.GetComponent<Collider>());

        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = transform.InverseTransformPoint(discWorld);
        disc.transform.localRotation = Quaternion.identity;

        float r = Mathf.Max(0.25f, groundMarkerRadius);
        Vector3 ls = transform.lossyScale;
        disc.transform.localScale = new Vector3(
            (r * 2f) / Mathf.Max(0.0001f, ls.x),
            0.05f / Mathf.Max(0.0001f, ls.y),
            (r * 2f) / Mathf.Max(0.0001f, ls.z));

        Renderer rend = disc.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null)
                sh = Shader.Find("Standard");
            Material mat = new Material(sh) { color = groundMarkerColor, name = "ExitMarker (runtime)" };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", groundMarkerColor);
            rend.sharedMaterial = mat;
        }
    }

    private void SetSignProperties(WorldSign sign, string text, float height)
    {
        sign.signText = text;
        sign.heightOffset = height;
        sign.SetText(text);
    }

    /// <summary>
    /// Вызвать сразу после AddComponent, до первого кадра Start — задаёт маркер и подпись (процедурный выход без префаба).
    /// </summary>
    public void ApplyProceduralExitVisuals(
        bool marker,
        float markerRadius,
        Color markerColor,
        bool sign,
        string text,
        float heightOffset)
    {
        showGroundMarker = marker;
        groundMarkerRadius = Mathf.Max(0.15f, markerRadius);
        groundMarkerColor = markerColor;
        showSign = sign;
        if (!string.IsNullOrEmpty(text))
            signText = text;
        signHeight = Mathf.Max(0.25f, heightOffset);
    }

    private void TryCachePlayerController()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
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
            TryCachePlayerController();
            if (playerController == null)
                playerController = other.GetComponentInParent<PlayerController>();

            // Не используем мировой Y=0: данж может быть целиком выше нуля — иначе выход из данжа никогда не срабатывает.

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
                if (_delayedTeleportCoroutine != null)
                    StopCoroutine(_delayedTeleportCoroutine);
                _delayedTeleportCoroutine = StartCoroutine(DelayedTeleport(teleportDelay));
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
        _delayedTeleportCoroutine = null;
        TeleportToSurface();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            isPlayerNearby = false;
            isHolding = false;
            holdProgress = 0f;
            // Пока идёт задержка телепорта, не сбрасываем isTeleporting — иначе CC/триггер ведут себя нестабильно
            if (_delayedTeleportCoroutine == null)
                isTeleporting = false;
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
        TryCachePlayerController();
        if (playerController == null)
        {
            Debug.LogError("❌ TeleportZone: PlayerController не найден!");
            isTeleporting = false;
            return;
        }

        if (!TryResolveExitPosition(out Vector3 targetPosition, out Quaternion targetRotation))
        {
            Debug.LogError("❌ TeleportZone: не удалось определить точку выхода. Проверь returnSpawnPoint и режим exitDestination.");
            isTeleporting = false;
            return;
        }

        Debug.Log($"🔄 Телепортируем игрока из {playerController.transform.position} в {targetPosition}");

        TeleportPhysicsFix physicsFix = playerController.GetComponent<TeleportPhysicsFix>();
        if (physicsFix != null)
        {
            Debug.Log("🔧 Используем TeleportPhysicsFix для возврата");
            physicsFix.TeleportWithPhysicsFix(targetPosition, targetRotation);
        }
        else
        {
            CharacterController characterController = playerController.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;

            if (characterController != null)
                characterController.enabled = false;

            playerController.transform.position = targetPosition;

            if (characterController != null)
                characterController.enabled = wasEnabled;

            playerController.transform.rotation = targetRotation;
        }

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

    private bool TryResolveExitPosition(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        _lastTeleportKeptPlayerUnderground = false;

        switch (exitDestination)
        {
            case DungeonReturnExitMode.FixedReturnSpawn:
                if (returnSpawnPoint == null)
                    return false;
                position = returnSpawnPoint.position;
                rotation = returnSpawnPoint.rotation;
                return true;

            case DungeonReturnExitMode.RandomHorizontalAroundReturnSpawn:
                if (returnSpawnPoint == null)
                    return false;
                if (!TryRandomPointOnGroundAround(returnSpawnPoint.position, randomSurfaceHorizontalRadius,
                        randomSurfaceRaycastStartHeight, randomSurfaceGroundMask, out position))
                {
                    position = returnSpawnPoint.position;
                }
                rotation = returnSpawnPoint.rotation;
                return true;

            case DungeonReturnExitMode.RandomFloorInProceduralDungeon:
            {
                ProceduralDungeonGenerator gen = proceduralDungeon != null
                    ? proceduralDungeon
                    : FindFirstObjectByType<ProceduralDungeonGenerator>();
                if (gen != null && gen.TryGetRandomFloorPosition(out position, out rotation))
                {
                    _lastTeleportKeptPlayerUnderground = true;
                    return true;
                }

                Debug.LogWarning("TeleportZone: RandomFloorInProceduralDungeon — не удалось, используем фиксированную точку.");
                if (returnSpawnPoint == null)
                    return false;
                position = returnSpawnPoint.position;
                rotation = returnSpawnPoint.rotation;
                return true;
            }

            default:
                if (returnSpawnPoint == null)
                    return false;
                position = returnSpawnPoint.position;
                rotation = returnSpawnPoint.rotation;
                return true;
        }
    }

    private static bool TryRandomPointOnGroundAround(
        Vector3 center,
        float horizontalRadius,
        float raycastStartHeight,
        LayerMask groundMask,
        out Vector3 hitPosition)
    {
        hitPosition = center;
        Vector2 disk = Random.insideUnitCircle * Mathf.Max(0.1f, horizontalRadius);
        Vector3 origin = center + new Vector3(disk.x, raycastStartHeight, disk.y);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastStartHeight + 200f, groundMask, QueryTriggerInteraction.Ignore))
        {
            hitPosition = hit.point + Vector3.up * 0.06f;
            return true;
        }

        return false;
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

            if (_lastTeleportKeptPlayerUnderground)
            {
                Debug.Log("✅ Выход внутри данжа — проверку «поверхности» не делаем");
                yield break;
            }

            if (returnSpawnPoint != null && currentPos.y < returnSpawnPoint.position.y - 0.75f)
            {
                Debug.LogWarning("⚠ Игрок остался заметно ниже точки выхода. Принудительно ставим в returnSpawnPoint.");

                playerController.transform.position = returnSpawnPoint.position;
                playerController.transform.rotation = returnSpawnPoint.rotation;

                Debug.Log($"🔧 Принудительно перемещен к точке выхода: {playerController.transform.position}");
            }
            else
            {
                Debug.Log("✅ Игрок успешно возвращен на поверхность");
            }
        }
    }
}
