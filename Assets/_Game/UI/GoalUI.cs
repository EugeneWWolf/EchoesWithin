using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI компонент для отображения цели игры в верхнем правом углу
/// </summary>
public class GoalUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text goalText;

    [Header("Display Settings")]
    [SerializeField] private int requiredMoney = 500;
    [SerializeField] private string goalFormat = "Goal: Collect ${0} and leave this planet";

    private PlayerWallet wallet;
    private int lastBalance = -1;

    public void SetRequiredMoney(int amount)
    {
        requiredMoney = amount;
        // Обновляем отображение только если текст уже установлен
        if (goalText != null)
        {
            UpdateDisplay();
        }
    }

    public void BindWallet(PlayerWallet playerWallet)
    {
        // Отписываемся от предыдущего кошелька
        if (wallet != null)
        {
            wallet.OnBalanceChanged -= OnBalanceChanged;
        }

        wallet = playerWallet;

        if (wallet != null)
        {
            wallet.OnBalanceChanged += OnBalanceChanged;
            UpdateDisplay();
            Debug.Log($"🎯 GoalUI: Привязан к кошельку. Требуется: ${requiredMoney}");
        }
        else
        {
            Debug.LogWarning("⚠ GoalUI: PlayerWallet не найден!");
        }
    }

    private void OnBalanceChanged(int newBalance)
    {
        if (lastBalance != newBalance)
        {
            UpdateDisplay();
            lastBalance = newBalance;
        }
    }

    private void Update()
    {
        // Fallback update mechanism - только во время игры
        if (!Application.isPlaying) return;

        if (wallet != null && wallet.Balance != lastBalance)
        {
            UpdateDisplay();
            lastBalance = wallet.Balance;
        }
    }

    private void UpdateDisplay()
    {
        // Проверяем, что мы в главном потоке и текст установлен
        if (goalText == null) return;
        if (!Application.isPlaying && !Application.isEditor) return;

        try
        {
            string displayText = string.Format(goalFormat, requiredMoney);
            goalText.text = displayText;

            // Меняем цвет в зависимости от прогресса
            if (wallet != null)
            {
                if (wallet.Balance >= requiredMoney)
                {
                    goalText.color = Color.green; // Цель достигнута
                }
                else
                {
                    goalText.color = Color.white; // Цель не достигнута
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠ GoalUI: Ошибка при обновлении отображения: {e.Message}");
        }
    }

    /// <summary>
    /// Обновляет отображение вручную
    /// </summary>
    public void RefreshDisplay()
    {
        UpdateDisplay();
    }
}

