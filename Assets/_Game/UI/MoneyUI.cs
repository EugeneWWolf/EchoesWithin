using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI компонент для отображения денег игрока
/// </summary>
public class MoneyUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text moneyText;

    [Header("Display Settings")]
    [SerializeField] private string moneyFormat = "💰 {0}";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowMoneyColor = Color.red;
    [SerializeField] private int lowMoneyThreshold = 10;

    [Header("Fallback Update")]
    [SerializeField] private bool enableFallbackUpdate = true;
    [SerializeField] private float fallbackUpdateInterval = 0.5f;

    private PlayerWallet wallet;
    private int lastBalance = -1;
    private float lastFallbackCheck = 0f;

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
            UpdateDisplay(wallet.Balance);
            Debug.Log($"💰 MoneyUI: Привязан к кошельку с балансом {wallet.Balance}");
        }
        else
        {
            Debug.LogWarning("⚠ MoneyUI: PlayerWallet не найден!");
        }
    }

    private void OnBalanceChanged(int newBalance)
    {
        if (lastBalance != newBalance)
        {
            UpdateDisplay(newBalance);
            lastBalance = newBalance;
        }
    }

    private void Update()
    {
        // Fallback update mechanism - проверяем изменения баланса периодически
        if (enableFallbackUpdate && wallet != null && Time.time - lastFallbackCheck > fallbackUpdateInterval)
        {
            lastFallbackCheck = Time.time;

            int currentBalance = wallet.Balance;
            if (currentBalance != lastBalance)
            {
                Debug.Log($"💰 MoneyUI: Fallback update обнаружено изменение баланса: {lastBalance} → {currentBalance}");
                UpdateDisplay(currentBalance);
                lastBalance = currentBalance;
            }
        }
    }

    private void UpdateDisplay(int balance)
    {
        if (moneyText != null)
        {
            string newText = string.Format(moneyFormat, balance);
            moneyText.text = newText;

            // Меняем цвет при низком балансе
            if (balance <= lowMoneyThreshold)
            {
                moneyText.color = lowMoneyColor;
            }
            else
            {
                moneyText.color = normalColor;
            }
        }
        else
        {
            Debug.LogWarning("⚠ MoneyUI: moneyText не назначен!");
        }
    }

    private void OnDestroy()
    {
        if (wallet != null)
        {
            wallet.OnBalanceChanged -= OnBalanceChanged;
        }
    }

    /// <summary>
    /// Обновляет отображение вручную
    /// </summary>
    public void RefreshDisplay()
    {
        if (wallet != null)
        {
            UpdateDisplay(wallet.Balance);
        }
    }
}