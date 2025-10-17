using UnityEngine;
using System.Collections;

public class TeleportZone : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Transform returnSpawnPoint;
    [SerializeField] private float holdTime = 3f;
    [SerializeField] private LayerMask playerLayer = 1; // Default layer

    [Header("Visual Feedback")]
    [SerializeField] private GameObject progressIndicator;
    [SerializeField] private Material progressMaterial;
    [SerializeField] private TeleportProgressUI progressUI;

    private bool isPlayerNearby = false;
    private bool isHolding = false;
    private float holdProgress = 0f;
    private PlayerController playerController;
    private Renderer zoneRenderer;
    private Material originalMaterial;

    private void Start()
    {
        // Находим игрока
        playerController = FindObjectOfType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("❌ TeleportZone: Не найден PlayerController!");
            return;
        }

        // Настраиваем визуальные эффекты
        zoneRenderer = GetComponent<Renderer>();
        if (zoneRenderer != null)
        {
            originalMaterial = zoneRenderer.material;
        }

        // Визуальная индикация отключена - только логи
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
        if (IsPlayer(other))
        {
            isPlayerNearby = true;
            Debug.Log("🔄 Игрок рядом с зоной возврата. Зажмите кнопку взаимодействия для возврата на поверхность.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            isPlayerNearby = false;
            isHolding = false;
            holdProgress = 0f;
            UpdateVisualFeedback();
            Debug.Log("🔄 Игрок отошел от зоны возврата.");
        }
    }

    public void StartHold()
    {
        if (!isHolding)
        {
            isHolding = true;
            holdProgress = 0f;
            Debug.Log("🔄 Начало зажатия кнопки для возврата...");
        }
    }

    public void StopHold()
    {
        if (isHolding)
        {
            isHolding = false;
            holdProgress = 0f;
            UpdateVisualFeedback();
            Debug.Log("🔄 Зажатие кнопки прервано.");
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
        indicatorMaterial.color = Color.blue;
        indicatorMaterial.SetFloat("_Metallic", 0f);
        indicatorMaterial.SetFloat("_Smoothness", 0.5f);
        indicatorRenderer.material = indicatorMaterial;

        progressIndicator = indicator;
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
