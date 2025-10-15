using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    private InputAction pauseAction;
    private bool isPaused = false;

    void Awake()
    {
        var playerInput = GetComponent<PlayerInput>();
        pauseAction = playerInput.actions["Pause"];
        pauseAction.performed += OnPauseToggle;

        // Убеждаемся, что меню выключено при старте
        pauseMenu.SetActive(false);
    }

    private void OnPauseToggle(InputAction.CallbackContext context)
    {
        isPaused = !isPaused;
        pauseMenu.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
        if (isPaused)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        } else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    public void OnResume()
    {
        isPaused = false;
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnExit()
    {
        Application.Quit();
    }
}