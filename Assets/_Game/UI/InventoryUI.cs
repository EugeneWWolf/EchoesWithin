using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Image[] slots;
    [SerializeField] private Sprite emptySlotSprite;

    private InventorySystem inventory;
    private Sprite[] cachedSprites; // ��� ��������
    private bool[] slotDirty; // ����� ��� ������������ ���������
    private int lastActiveSlot = -1;

    public void BindInventory(InventorySystem inv)
    {
        inventory = inv;
        inventory.OnInventoryChanged += OnInventoryChanged;

        // ������������� ����
        cachedSprites = new Sprite[inventory.Size];
        slotDirty = new bool[inventory.Size];

        // �������� ��� ����� ��� "�������" ��� ��������������� ����������
        for (int i = 0; i < inventory.Size; i++)
        {
            slotDirty[i] = true;
        }

        UpdateUI();

        // ����: ���������� ������� �������� ���� ��� "���������"
        // ����� ��� ������ ������������ ��������� �������� ��������� �����������
        lastActiveSlot = inventory.ActiveSlot;
    }

    private void OnInventoryChanged()
    {
        // �������� �������� ���� ��� "�������" ��� ���������� �����
        if (lastActiveSlot != inventory.ActiveSlot)
        {
            if (lastActiveSlot >= 0 && lastActiveSlot < inventory.Size)
                slotDirty[lastActiveSlot] = true;
            if (inventory.ActiveSlot >= 0 && inventory.ActiveSlot < inventory.Size)
                slotDirty[inventory.ActiveSlot] = true;
            lastActiveSlot = inventory.ActiveSlot;
        }
        else if (lastActiveSlot == -1)
        {
            // �������: ���� ����� �� ���� ��������� �����,
            // �������������� ������� ����� ���� ����� ���� ���
            for (int i = 0; i < inventory.Size; i++)
            {
                slotDirty[i] = true;
            }
            lastActiveSlot = inventory.ActiveSlot;
        }

        // �������� ���������� ������ �� ������ ���������
        int changed = inventory.LastChangedIndex;
        if (changed >= 0 && changed < inventory.Size)
            slotDirty[changed] = true;

        UpdateUI();
        inventory.ClearUIUpdateFlag();
    }

    private void UpdateUI()
    {
        for (int i = 0; i < inventory.Size; i++)
        {
            if (!slotDirty[i]) continue; // ���������� ������������ �����

            GameObject item = inventory.GetItem(i);
            Sprite newSprite = emptySlotSprite;

            if (item != null)
            {
                Item itemComp = item.GetComponent<Item>();
                if (itemComp != null && itemComp.icon != null)
                    newSprite = itemComp.icon;
            }

            // ��������� ������ ���� ������ ���������
            if (cachedSprites[i] != newSprite)
            {
                cachedSprites[i] = newSprite;
                slots[i].sprite = newSprite;
            }

            // ��������� ���� ��������� �����
            slots[i].color = (i == inventory.ActiveSlot) ? Color.softRed : Color.white;

            slotDirty[i] = false; // �������� ��� �����������
        }
    }

    /// <summary>
    /// Принудительно обновляет весь UI инвентаря
    /// </summary>
    public void RefreshDisplay()
    {
        if (inventory == null) return;

        // Помечаем все слоты как измененные
        for (int i = 0; i < inventory.Size; i++)
        {
            slotDirty[i] = true;
        }

        UpdateUI();
    }
}
