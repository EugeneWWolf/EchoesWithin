using UnityEngine;

public class PlayerCameraController
{
    private readonly Transform cameraT;
    private readonly Transform playerTransform;

    private Vector2 lookInput;
    private float sensitivity;
    private float verticalLimit;
    private float xRotation;

    public PlayerCameraController(Transform cameraT, Transform playerTransform, PlayerSettings settings)
    {
        this.cameraT = cameraT;
        this.playerTransform = playerTransform;
        ApplySettings(settings);
    }

    public void ApplySettings(PlayerSettings settings)
    {
        if (settings == null) return;
        sensitivity = settings.mouseSensitivity;
        verticalLimit = settings.verticalLookLimit;
    }

    public void SetLookInput(Vector2 input) => lookInput = input;

    public void Tick()
    {
        // Кэшируем deltaTime для оптимизации
        float deltaTime = Time.deltaTime;
        float mouseX = lookInput.x * sensitivity * deltaTime;
        float mouseY = lookInput.y * sensitivity * deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -verticalLimit, verticalLimit);

        // Кэшируем Quaternion для избежания повторных вычислений
        cameraT.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerTransform.Rotate(Vector3.up * mouseX);
    }

    public void SetSensitivity(float s) => sensitivity = s;
}
