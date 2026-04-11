using UnityEngine;
using System.Collections;

public class TeleportPhysicsFix : MonoBehaviour
{
    [Header("Physics Fix Settings")]
    [SerializeField] private float fixDuration = 0.5f;
    [SerializeField] private bool disableGravity = true;
    [SerializeField] private bool disableCollision = true;

    [Header("Телепорт в данж")]
    [SerializeField] private float teleportCcDisabledSeconds = 0.1f;
    [Tooltip("После включения CharacterController столько секунд игнорируем коллизию со слоем данжа (проход сквозь крышу/оболочку)")]
    [SerializeField] private float ignoreDungeonLayerAfterCcReenabledSeconds = 0.45f;

    private CharacterController characterController;
    private Rigidbody rigidbody;
    private bool originalGravity;
    private bool originalCollision;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        rigidbody = GetComponent<Rigidbody>();
    }

    [ContextMenu("Fix Teleportation Physics")]
    public void FixTeleportationPhysics()
    {
        StartCoroutine(DisablePhysicsTemporarily());
    }

    private IEnumerator DisablePhysicsTemporarily()
    {
        Debug.Log("🔧 Отключаем физику для телепортации...");

        // Сохраняем оригинальные настройки
        if (characterController != null)
        {
            originalCollision = characterController.enabled;
            characterController.enabled = false;
            Debug.Log("🔧 CharacterController отключен");
        }

        if (rigidbody != null)
        {
            originalGravity = rigidbody.useGravity;
            if (disableGravity)
            {
                rigidbody.useGravity = false;
                Debug.Log("🔧 Gravity отключена");
            }
        }

        // Ждем
        yield return new WaitForSeconds(fixDuration);

        // Восстанавливаем настройки
        if (characterController != null)
        {
            characterController.enabled = originalCollision;
            Debug.Log("🔧 CharacterController восстановлен");
        }

        if (rigidbody != null && disableGravity)
        {
            rigidbody.useGravity = originalGravity;
            Debug.Log("🔧 Gravity восстановлена");
        }

        Debug.Log("🔧 Физика восстановлена");
    }

    public void TeleportWithPhysicsFix(Vector3 position)
    {
        TeleportWithPhysicsFix(position, transform.rotation);
    }

    public void TeleportWithPhysicsFix(Vector3 position, Quaternion rotation)
    {
        StartCoroutine(TeleportWithFix(position, rotation));
    }

    private IEnumerator TeleportWithFix(Vector3 position, Quaternion rotation)
    {
        Debug.Log($"🔧 Телепортация с исправлением физики в: {position}");

        yield return DungeonTeleportCollisionBypass.CoTeleport(
            transform,
            characterController,
            rigidbody,
            position,
            rotation,
            teleportCcDisabledSeconds,
            ignoreDungeonLayerAfterCcReenabledSeconds,
            true);

        Debug.Log($"✅ Телепортация завершена: {transform.position}");
    }

    /// <summary>Корутина для TeleportDoor: ждать завершения телепорта на этом объекте.</summary>
    public IEnumerator DungeonTeleportSequence(Vector3 position, Quaternion rotation)
    {
        yield return TeleportWithFix(position, rotation);
    }
}
