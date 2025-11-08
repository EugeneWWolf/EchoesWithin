using UnityEngine;

public class SellZone : MonoBehaviour
{
    [Tooltip("ћножитель цены продажи (например, 1.0 = базова€ цена)")]
    public float priceMultiplier = 1f;

    private bool playerInside;

    public bool IsPlayerInside => playerInside;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }
}


