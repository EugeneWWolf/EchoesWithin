using UnityEngine;

public class ShopZone : MonoBehaviour
{
    [Header("Shop Settings")]
    [Tooltip("Множитель цены покупки (например, 1.0 = обычная цена, 0.8 = скидка 20%)")]
    public float priceMultiplier = 1f;

    [Header("Shop Items")]
    [Tooltip("Предметы, доступные для покупки в этой лавке")]
    public GameObject[] shopItems;

    private bool playerInside;

    public bool IsPlayerInside => playerInside;

    private void Start()
    {
        // Размещаем предметы в лавке при старте
        PlaceShopItems();
    }

    public void PlaceShopItems()
    {
        Debug.Log($"🛒 ShopZone: Начинаем размещение предметов. Количество: {(shopItems?.Length ?? 0)}");

        if (shopItems == null || shopItems.Length == 0)
        {
            Debug.LogWarning("⚠ ShopZone: Массив shopItems пуст или не назначен!");
            return;
        }

        // Проверяем, не размещены ли уже предметы
        bool alreadyPlaced = false;
        for (int i = 0; i < shopItems.Length; i++)
        {
            if (shopItems[i] != null && shopItems[i].activeInHierarchy)
            {
                alreadyPlaced = true;
                break;
            }
        }

        if (alreadyPlaced)
        {
            Debug.Log("ℹ ShopZone: Предметы уже размещены, пропускаем повторное размещение");
            return;
        }

        float spacing = 2f;
        Vector3 startPosition = transform.position + Vector3.right * (-shopItems.Length * spacing * 0.5f);

        Debug.Log($"🛒 ShopZone: Позиция размещения предметов: {startPosition}");

        for (int i = 0; i < shopItems.Length; i++)
        {
            if (shopItems[i] != null)
            {
                Vector3 itemPosition = startPosition + Vector3.right * (i * spacing);
                shopItems[i].transform.position = itemPosition;
                shopItems[i].SetActive(true);

                Debug.Log($"✅ ShopZone: Размещен предмет {i}: {shopItems[i].name} в позиции {itemPosition}");

                // Добавляем коллайдер если его нет
                if (!shopItems[i].GetComponent<Collider>())
                {
                    var collider = shopItems[i].AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    Debug.Log($"🔧 ShopZone: Добавлен коллайдер для {shopItems[i].name}");
                }

                // Устанавливаем слой для взаимодействия
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer != -1)
                {
                    shopItems[i].layer = interactableLayer;
                    Debug.Log($"🔧 ShopZone: Установлен слой Interactable для {shopItems[i].name}");
                }
                else
                {
                    Debug.LogWarning($"⚠ ShopZone: Слой 'Interactable' не найден! Создайте его в Project Settings > Tags and Layers");
                }

                // Проверяем компоненты предмета
                CheckItemComponents(shopItems[i]);
            }
            else
            {
                Debug.LogWarning($"⚠ ShopZone: Элемент {i} в массиве shopItems равен null!");
            }
        }

        Debug.Log("🛒 ShopZone: Размещение предметов завершено");
    }

    private void CheckItemComponents(GameObject item)
    {
        bool hasItemComponent = item.GetComponent<Item>() != null;
        bool hasBuffItem = item.GetComponent<BuffItem>() != null;
        bool hasWeapon = item.GetComponent<Weapon>() != null;
        bool hasSellableItem = item.GetComponent<SellableItem>() != null;

        Debug.Log($"🔍 ShopZone: Компоненты предмета {item.name}:");
        Debug.Log($"   - Item: {hasItemComponent}");
        Debug.Log($"   - BuffItem: {hasBuffItem}");
        Debug.Log($"   - Weapon: {hasWeapon}");
        Debug.Log($"   - SellableItem: {hasSellableItem}");

        if (!hasItemComponent && !hasBuffItem && !hasWeapon && !hasSellableItem)
        {
            Debug.LogWarning($"⚠ ShopZone: Предмет {item.name} не имеет ни одного компонента для покупки!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            Debug.Log("🛒 Игрок вошел в лавку");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            Debug.Log("🛒 Игрок вышел из лавки");
        }
    }

    public int GetPurchasePrice(int basePrice)
    {
        return Mathf.RoundToInt(basePrice * priceMultiplier);
    }

    // Метод для ручного обновления предметов (можно вызвать из инспектора)
    [ContextMenu("Обновить предметы в лавке")]
    public void RefreshShopItems()
    {
        Debug.Log("🔄 ShopZone: Ручное обновление предметов в лавке");
        PlaceShopItems();
    }

    // Метод для проверки состояния лавки
    [ContextMenu("Проверить состояние лавки")]
    public void CheckShopStatus()
    {
        Debug.Log($"🔍 ShopZone: Проверка состояния лавки '{gameObject.name}'");
        Debug.Log($"   - Позиция: {transform.position}");
        Debug.Log($"   - Количество предметов: {(shopItems?.Length ?? 0)}");
        Debug.Log($"   - Множитель цены: {priceMultiplier}");
        Debug.Log($"   - Игрок внутри: {playerInside}");

        if (shopItems != null)
        {
            for (int i = 0; i < shopItems.Length; i++)
            {
                if (shopItems[i] != null)
                {
                    Debug.Log($"   - Предмет {i}: {shopItems[i].name} (активен: {shopItems[i].activeInHierarchy})");
                }
                else
                {
                    Debug.LogWarning($"   - Предмет {i}: NULL!");
                }
            }
        }
    }
}
