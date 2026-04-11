using System.Collections;
using UnityEngine;

/// <summary>
/// При входе в данж CharacterController упирается в «оболочку»/крышу — временно исключаем слой данжа из коллизий CC.
/// </summary>
public static class DungeonTeleportCollisionBypass
{
    /// <param name="secondsWithCcDisabled">Сколько секунд CC выключен после смены позиции (обычно один кадр или 0.05–0.1).</param>
    /// <param name="secondsIgnoreDungeonAfterCcEnabled">После включения CC ещё столько секунд держим excludeLayers (проход сквозь геометрию данжа).</param>
    public static IEnumerator CoTeleport(
        Transform playerTransform,
        CharacterController characterController,
        Rigidbody rigidbody,
        Vector3 worldPosition,
        Quaternion worldRotation,
        float secondsWithCcDisabled,
        float secondsIgnoreDungeonAfterCcEnabled,
        bool zeroRigidbodyVelocityAndGravityWhileDisabled)
    {
        if (playerTransform == null)
            yield break;

        int dungeonLayer = ProceduralDungeonGenerator.DungeonCollisionLayerIndex;
        LayerMask previousExclude = default;
        bool useLayerMask = characterController != null && dungeonLayer >= 0 && dungeonLayer <= 31;

        if (useLayerMask)
        {
            previousExclude = characterController.excludeLayers;
            characterController.excludeLayers = previousExclude | (1 << dungeonLayer);
        }

        if (characterController != null)
            characterController.enabled = false;

        if (rigidbody != null && zeroRigidbodyVelocityAndGravityWhileDisabled)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.useGravity = false;
        }

        playerTransform.position = worldPosition;

        if (secondsWithCcDisabled > 0f)
            yield return new WaitForSeconds(secondsWithCcDisabled);
        else
            yield return null;

        if (characterController != null)
            characterController.enabled = true;

        if (rigidbody != null)
            rigidbody.useGravity = true;

        if (secondsIgnoreDungeonAfterCcEnabled > 0f)
            yield return new WaitForSeconds(secondsIgnoreDungeonAfterCcEnabled);

        if (useLayerMask && characterController != null)
            characterController.excludeLayers = previousExclude;

        playerTransform.rotation = worldRotation;
    }
}
