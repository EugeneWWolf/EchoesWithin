using UnityEngine;

public class PlayerMovement
{
    private readonly CharacterController controller;
    private Vector3 moveInput;
    private Vector3 velocity;

    private float speed;
    private float jumpHeight;
    private float gravity;

    private PlayerStats playerStats;
    private PlayerSettings settings;

    public PlayerMovement(CharacterController controller, PlayerSettings settings, PlayerStats playerStats)
    {
        this.controller = controller;
        this.settings = settings;
        this.playerStats = playerStats;
        ApplySettings(settings);
    }

    public void ApplySettings(PlayerSettings settings)
    {
        // PlayerSettings больше не содержит статы движения
        // Все статы теперь управляются через PlayerStats
        if (playerStats != null)
        {
            UpdateStats(playerStats);
        }
        else
        {
            // Если нет PlayerStats, используем значения по умолчанию
            speed = 5f;
            jumpHeight = 2f;
            gravity = -9.8f;
            Debug.LogWarning("⚠ PlayerMovement: PlayerStats не найден, используются значения по умолчанию");
        }
    }

    public void SetMoveInput(Vector2 input) => moveInput = new Vector3(input.x, 0f, input.y);

    public void TryJump(bool pressed)
    {
        if (pressed && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    public void Tick()
    {
        Vector3 move = controller.transform.right * moveInput.x + controller.transform.forward * moveInput.z;
        controller.Move(speed * Time.deltaTime * move);

        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // Обновление статов из PlayerStats
    public void UpdateStats(PlayerStats stats)
    {
        if (stats != null)
        {
            speed = stats.currentSpeed;
            jumpHeight = stats.currentJumpHeight;
            gravity = stats.currentGravity;
        }
    }

    // Принудительное обновление статов
    public void ForceUpdateStats()
    {
        if (playerStats != null)
        {
            UpdateStats(playerStats);
            Debug.Log($"📊 PlayerMovement: Принудительно обновлены статы - Speed: {speed}, Jump: {jumpHeight}, Gravity: {gravity}");
        }
    }

    // runtime-настройки
    public void SetSpeed(float s) => speed = s;
    public void SetGravity(float g) => gravity = g;
}
