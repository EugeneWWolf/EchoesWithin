using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Image[] slots;
    [SerializeField] private Sprite emptySlotSprite;

    private InventorySystem inventory;
    private Sprite[] cachedSprites; // Кэш спрайтов
    private bool[] slotDirty; // Флаги для отслеживания изменений
    private int lastActiveSlot = -1;

    public void BindInventory(InventorySystem inv)
    {
        inventory = inv;
        inventory.OnInventoryChanged += OnInventoryChanged;

        // Инициализация кэша
        cachedSprites = new Sprite[inventory.Size];
        slotDirty = new bool[inventory.Size];

        // Помечаем все слоты как "грязные" для первоначального обновления
        for (int i = 0; i < inventory.Size; i++)
        {
            slotDirty[i] = true;
        }

        UpdateUI();

        // Фикс: запоминаем текущий активный слот как "последний"
        // чтобы при первом переключении корректно очистить подсветку предыдущего
        lastActiveSlot = inventory.ActiveSlot;
    }

    private void OnInventoryChanged()
    {
        // Помечаем активный слот как "грязный" для обновления цвета
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
            // Фоллбэк: если ранее не было активного слота,
            // гарантированно обновим цвета всех ячеек один раз
            for (int i = 0; i < inventory.Size; i++)
            {
                slotDirty[i] = true;
            }
            lastActiveSlot = inventory.ActiveSlot;
        }

        // Помечаем изменённую ячейку из модели инвентаря
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
            if (!slotDirty[i]) continue; // Пропускаем неизмененные слоты

            GameObject item = inventory.GetItem(i);
            Sprite newSprite = emptySlotSprite;

            if (item != null)
            {
                Item itemComp = item.GetComponent<Item>();
                if (itemComp != null && itemComp.icon != null)
                    newSprite = itemComp.icon;
            }

            // Обновляем только если спрайт изменился
            if (cachedSprites[i] != newSprite)
            {
                cachedSprites[i] = newSprite;
                slots[i].sprite = newSprite;
            }

            // Обновляем цвет активного слота
            slots[i].color = (i == inventory.ActiveSlot) ? Color.softRed : Color.white;

            slotDirty[i] = false; // Помечаем как обновленный
        }
    }
}
