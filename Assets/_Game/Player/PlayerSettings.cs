using UnityEngine;

[CreateAssetMenu(fileName = "PlayerSettings", menuName = "Config/PlayerSettings")]
public class PlayerSettings : ScriptableObject
{
    [Header("Camera")]
    public float mouseSensitivity = 0.5f;
    public float verticalLookLimit = 80f;

    [Header("Interaction")]
    public float interactDistance = 3f;
}