using UnityEngine;

public class InventorySystem
{
    private readonly GameObject[] slots;
    private int activeSlot = 0;

    // ��� ��� �����������
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
        if (activeSlot == index) return; // �������� �������� ����������

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
        if (obj == null) return null; // �������� �������� ����������

        slots[activeSlot] = null;
        LastChangedIndex = activeSlot;
        needsUIUpdate = true;
        OnInventoryChanged?.Invoke();
        return obj;
    }

    /// <summary>
    /// Очищает весь инвентарь
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = null;
        }
        needsUIUpdate = true;
        LastChangedIndex = -1;
        OnInventoryChanged?.Invoke();
    }

    public int Size => slots.Length;
}
