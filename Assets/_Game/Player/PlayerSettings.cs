using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "Config/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    [Header("Movement")]
    public float speed = 5f;
    public float jumpHeight = 2f;
    public float gravity = -9.8f;

    [Header("Camera")]
    public float mouseSensitivity = 0.5f;
    public float verticalLookLimit = 80f;

    [Header("Interaction")]
    public float interactDistance = 3f;
}