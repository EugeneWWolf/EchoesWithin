using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Image[] slots;
    [SerializeField] private Sprite emptySlotSprite;

    private InventorySystem inventory;

    public void BindInventory(InventorySystem inv)
    {
        inventory = inv;
        inventory.OnInventoryChanged += UpdateUI;
        UpdateUI();
    }

    private void UpdateUI()
    {
        for (int i = 0; i < inventory.Size; i++)
        {
            Sprite sprite = emptySlotSprite;
            GameObject item = inventory.GetItem(i);

            if (item != null)
            {
                Item itemComp = item.GetComponent<Item>();
                if (itemComp != null && itemComp.icon != null)
                    sprite = itemComp.icon;
            }

            slots[i].sprite = sprite;
            slots[i].color = (i == inventory.ActiveSlot) ? Color.softRed : Color.white;
        }
    }
}
