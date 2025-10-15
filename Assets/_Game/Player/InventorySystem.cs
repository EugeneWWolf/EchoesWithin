using UnityEngine;

public class InventorySystem
{
    private readonly GameObject[] slots;
    private int activeSlot = 0;

    public delegate void InventoryChanged();
    public event InventoryChanged OnInventoryChanged;

    public InventorySystem(int size)
    {
        slots = new GameObject[size];
    }

    public GameObject GetItem(int index) => slots[index];
    public int ActiveSlot => activeSlot;

    public void SetActiveSlot(int index)
    {
        activeSlot = index;
        OnInventoryChanged?.Invoke();
    }

    public bool TryAdd(GameObject item)
    {
        if (slots[activeSlot] != null)
            return false;

        slots[activeSlot] = item;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public GameObject RemoveActive()
    {
        GameObject obj = slots[activeSlot];
        slots[activeSlot] = null;
        OnInventoryChanged?.Invoke();
        return obj;
    }

    public int Size => slots.Length;
}
