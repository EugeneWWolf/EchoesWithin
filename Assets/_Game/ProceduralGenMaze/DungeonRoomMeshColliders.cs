using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// У ассетов комнаты часто нет коллайдеров — добавляет статичные MeshCollider к каждому MeshFilter с мешем.
/// </summary>
public static class DungeonRoomMeshColliders
{
    /// <summary>
    /// Для каждого дочернего объекта с MeshFilter и sharedMesh, у которого ещё нет Collider, добавляет MeshCollider (не convex).
    /// </summary>
    public static void EnsureOnHierarchy(GameObject root) =>
        EnsureOnHierarchy(root, null);

    /// <param name="skipColliderUnderAncestorNameHints">
    /// Если задано: не добавлять MeshCollider к мешам, у которых вверх по иерархии от корня комнаты есть объект,
    /// чьё имя (без суффикса « (Clone)») совпадает с подсказкой без учёта регистра (например «Cellar», «Ceiling»).
    /// Нужно, чтобы луч сверху вниз не останавливался на верхней грани потолка при поиске пола.
    /// </param>
    public static void EnsureOnHierarchy(GameObject root, IReadOnlyList<string> skipColliderUnderAncestorNameHints)
    {
        if (root == null)
            return;

        Transform roomRoot = root.transform;
        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh == null)
                continue;

            if (skipColliderUnderAncestorNameHints != null &&
                skipColliderUnderAncestorNameHints.Count > 0 &&
                IsUnderSkippedAncestor(mf.transform, roomRoot, skipColliderUnderAncestorNameHints))
                continue;

            GameObject go = mf.gameObject;
            if (go.GetComponent<Collider>() != null)
                continue;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }
    }

    private static bool IsUnderSkippedAncestor(Transform meshTransform, Transform roomRoot, IReadOnlyList<string> hints)
    {
        for (Transform t = meshTransform; t != null && t != roomRoot; t = t.parent)
        {
            string baseName = StripCloneSuffix(t.name);
            foreach (string hint in hints)
            {
                if (string.IsNullOrWhiteSpace(hint))
                    continue;
                if (string.Equals(baseName, hint.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static string StripCloneSuffix(string instanceName)
    {
        const string suffix = " (Clone)";
        if (instanceName.EndsWith(suffix))
            return instanceName.Substring(0, instanceName.Length - suffix.Length);
        return instanceName;
    }
}
