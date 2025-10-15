using UnityEngine;

public class PlayerMovement
{
    private readonly CharacterController controller;
    private Vector3 moveInput;
    private Vector3 velocity;

    private float speed;
    private float jumpHeight;
    private float gravity;

    public PlayerMovement(CharacterController controller, PlayerSettings settings)
    {
        this.controller = controller;
        ApplySettings(settings);
    }

    public void ApplySettings(PlayerSettings settings)
    {
        if (settings == null) return;
        speed = settings.speed;
        jumpHeight = settings.jumpHeight;
        gravity = settings.gravity;
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

    // опционально: runtime-сеттеры
    public void SetSpeed(float s) => speed = s;
    public void SetGravity(float g) => gravity = g;
}
