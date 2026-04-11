using UnityEngine;

/// <summary>
/// У ассетов комнаты часто нет коллайдеров — добавляет статичные MeshCollider к каждому MeshFilter с мешем.
/// </summary>
public static class DungeonRoomMeshColliders
{
    /// <summary>
    /// Для каждого дочернего объекта с MeshFilter и sharedMesh, у которого ещё нет Collider, добавляет MeshCollider (не convex).
    /// </summary>
    public static void EnsureOnHierarchy(GameObject root)
    {
        if (root == null)
            return;

        var filters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh == null)
                continue;

            GameObject go = mf.gameObject;
            if (go.GetComponent<Collider>() != null)
                continue;

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }
    }
}
