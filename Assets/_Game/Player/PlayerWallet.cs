using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    [SerializeField]
    private int balance;

    public int Balance => balance;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        balance += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (balance < amount) return false;
        balance -= amount;
        return true;
    }
}


