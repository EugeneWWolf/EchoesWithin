using System.Collections;
using UnityEngine;

/// <summary>
/// При входе в данж CharacterController упирается в «оболочку»/крышу — временно исключаем слой данжа из коллизий CC.
/// Важно: пока CC включён и слой данжа в excludeLayers, пол не даёт isGrounded — игрок падает сквозь меш; поэтому
/// маска снимается сразу после включения CC, а не «ещё N секунд после».
/// </summary>
public static class DungeonTeleportCollisionBypass
{
    /// <param name="secondsWithCcDisabled">Сколько секунд CC выключен после смены позиции (обычно один кадр или 0.05–0.1).</param>
    /// <param name="optionalSettleDelayAfterCollisionsRestored">
    /// Необязательная пауза после того как коллизия со слоем данжа снова включена (гравитация уже вкл.).
    /// Раньше параметр означал «игнорировать данж при включённом CC» — это давало провалы сквозь пол; оставлено только как безопасная задержка.
    /// </param>
    public static IEnumerator CoTeleport(
        Transform playerTransform,
        CharacterController characterController,
        Rigidbody rigidbody,
        Vector3 worldPosition,
        Quaternion worldRotation,
        float secondsWithCcDisabled,
        float optionalSettleDelayAfterCollisionsRestored,
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

        playerTransform.SetPositionAndRotation(worldPosition, worldRotation);
        Physics.SyncTransforms();

        if (secondsWithCcDisabled > 0f)
            yield return new WaitForSeconds(secondsWithCcDisabled);
        else
            yield return null;

        if (characterController != null)
            characterController.enabled = true;

        if (useLayerMask && characterController != null)
            characterController.excludeLayers = previousExclude;

        if (rigidbody != null)
            rigidbody.useGravity = true;

        if (optionalSettleDelayAfterCollisionsRestored > 0f)
            yield return new WaitForSeconds(optionalSettleDelayAfterCollisionsRestored);
    }
}
