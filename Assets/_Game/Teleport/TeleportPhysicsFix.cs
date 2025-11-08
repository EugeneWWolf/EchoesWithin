using UnityEngine;
using System.Collections;

public class TeleportPhysicsFix : MonoBehaviour
{
    [Header("Physics Fix Settings")]
    [SerializeField] private float fixDuration = 0.5f;
    [SerializeField] private bool disableGravity = true;
    [SerializeField] private bool disableCollision = true;

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

    // Метод для принудительной телепортации с исправлением физики
    public void TeleportWithPhysicsFix(Vector3 position)
    {
        StartCoroutine(TeleportWithFix(position));
    }

    private IEnumerator TeleportWithFix(Vector3 position)
    {
        Debug.Log($"🔧 Телепортация с исправлением физики в: {position}");

        // Отключаем физику
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (rigidbody != null)
        {
            rigidbody.useGravity = false;
            rigidbody.linearVelocity = Vector3.zero;
        }

        // Телепортируем
        transform.position = position;

        // Ждем немного
        yield return new WaitForSeconds(0.1f);

        // Включаем физику обратно
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (rigidbody != null)
        {
            rigidbody.useGravity = true;
        }

        Debug.Log($"✅ Телепортация завершена: {transform.position}");
    }
}
