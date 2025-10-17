using UnityEngine;
using System;

public class PlayerWallet : MonoBehaviour
{
    [SerializeField]
    private int balance;

    public int Balance => balance;

    // Событие для уведомления об изменении баланса
    public event Action<int> OnBalanceChanged;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        balance += amount;
        OnBalanceChanged?.Invoke(balance);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (balance < amount) return false;
        balance -= amount;
        OnBalanceChanged?.Invoke(balance);
        return true;
    }

    /// <summary>
    /// Устанавливает баланс напрямую (для тестирования)
    /// </summary>
    public void SetBalance(int newBalance)
    {
        if (newBalance < 0) newBalance = 0;
        balance = newBalance;
        OnBalanceChanged?.Invoke(balance);
    }

    /// <summary>
    /// Добавляет деньги для тестирования
    /// </summary>
    [ContextMenu("Add 100 Money")]
    public void AddMoney()
    {
        Add(100);
    }

    /// <summary>
    /// Сбрасывает деньги на 0
    /// </summary>
    [ContextMenu("Reset Money")]
    public void ResetMoney()
    {
        SetBalance(0);
    }
}


