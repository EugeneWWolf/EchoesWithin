using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// ��������� ��� ����������� ������ �����
/// </summary>
public class DamageText : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float fadeSpeed = 1f;
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private Vector3 moveDirection = Vector3.up;

    private Text damageText;
    private CanvasGroup canvasGroup;
    private float startTime;

    private void Awake()
    {
        damageText = GetComponent<Text>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        startTime = Time.time;
    }

    private void Start()
    {
        StartCoroutine(AnimateText());
    }

    private void LateUpdate()
    {
        // Поворачиваем текст к камере (billboard эффект)
        if (Camera.main != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - transform.position;
            if (directionToCamera != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }

    /// <summary>
    /// ������������� ����� �����
    /// </summary>
    public void SetDamage(float damage)
    {
        if (damageText != null)
        {
            damageText.text = damage.ToString("F0");

            // ���� � ����������� �� �����
            if (damage >= 50)
                damageText.color = Color.red;
            else if (damage >= 25)
                damageText.color = Color.yellow;
            else
                damageText.color = Color.white;
        }
        else
        {
            // ���� damageText �� ������, ���� ���
            damageText = GetComponentInChildren<Text>();
            if (damageText != null)
            {
                damageText.text = damage.ToString("F0");

                // ���� � ����������� �� �����
                if (damage >= 50)
                    damageText.color = Color.red;
                else if (damage >= 25)
                    damageText.color = Color.yellow;
                else
                    damageText.color = Color.white;
            }
        }
    }

    /// <summary>
    /// �������� ������
    /// </summary>
    private IEnumerator AnimateText()
    {
        Vector3 startPosition = transform.position;

        while (Time.time - startTime < lifetime)
        {
            float elapsed = Time.time - startTime;
            float progress = elapsed / lifetime;

            // �������� �����
            transform.position = startPosition + moveDirection * moveSpeed * elapsed;

            // ���������
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - progress;
            }

            yield return null;
        }

        // ���������� ������
        Destroy(gameObject);
    }

    /// <summary>
    /// ������� ����� ����� � ��������� �������
    /// </summary>
    public static GameObject CreateDamageText(Vector3 position, float damage, GameObject prefab = null)
    {
        GameObject damageTextObj;

        if (prefab != null)
        {
            damageTextObj = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            // ������� ������� ����� �����
            damageTextObj = CreateSimpleDamageText(position);
        }

        // ����������� ����
        DamageText damageTextComponent = damageTextObj.GetComponent<DamageText>();
        if (damageTextComponent == null)
        {
            damageTextComponent = damageTextObj.AddComponent<DamageText>();
        }

        damageTextComponent.SetDamage(damage);

        return damageTextObj;
    }

    /// <summary>
    /// ������� ������� ����� ����� ��� �������
    /// </summary>
    private static GameObject CreateSimpleDamageText(Vector3 position)
    {
        // ������� Canvas
        GameObject canvasObj = new GameObject("DamageTextCanvas");
        canvasObj.transform.position = position;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // ����������� ������ Canvas
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2f, 1f);
        canvasRect.localScale = Vector3.one * 0.1f; // ��������� ������

        // ������� Text
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(canvasObj.transform);

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 200; // ����������� ������ ������
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "100"; // �������� �����

        // ����������� RectTransform
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localPosition = Vector3.zero;
        textRect.localScale = Vector3.one;

        // ��������� Outline ��� ������ ���������
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, 2);

        return canvasObj;
    }
}
