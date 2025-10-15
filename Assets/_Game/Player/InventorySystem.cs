using UnityEngine;

public class InventorySystem
{
    private readonly GameObject[] slots;
    private int activeSlot = 0;

    // Кэш для оптимизации
    private bool needsUIUpdate = false;
    public bool NeedsUIUpdate => needsUIUpdate;
    public int LastChangedIndex { get; private set; } = -1;

    public delegate void InventoryChanged();
    public event InventoryChanged OnInventoryChanged;

    public InventorySystem(int size)
    {
        slots = new GameObject[size];
    }

    public GameObject GetItem(int index) => slots[index];
    public int ActiveSlot => activeSlot;
    public void ClearUIUpdateFlag() => needsUIUpdate = false;

    public void SetActiveSlot(int index)
    {
        if (activeSlot == index) return; // Избегаем ненужных обновлений

        activeSlot = index;
        LastChangedIndex = index;
        needsUIUpdate = true;
        OnInventoryChanged?.Invoke();
    }

    public bool TryAdd(GameObject item)
    {
        if (slots[activeSlot] != null)
            return false;

        slots[activeSlot] = item;
        LastChangedIndex = activeSlot;
        needsUIUpdate = true;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public GameObject RemoveActive()
    {
        GameObject obj = slots[activeSlot];
        if (obj == null) return null; // Избегаем ненужных обновлений

        slots[activeSlot] = null;
        LastChangedIndex = activeSlot;
        needsUIUpdate = true;
        OnInventoryChanged?.Invoke();
        return obj;
    }

    public int Size => slots.Length;
}
