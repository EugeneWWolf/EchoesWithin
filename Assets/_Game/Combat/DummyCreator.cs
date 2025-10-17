using UnityEngine;

/// <summary>
/// Утилита для создания манекена для тестирования урона
/// </summary>
public class DummyCreator : MonoBehaviour
{
    [Header("Dummy Settings")]
    [SerializeField] private GameObject dummyPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(5, 0, 0);
    [SerializeField] private float dummyHealth = 100f;
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private float respawnTime = 3f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugUI = true;

    private Dummy currentDummy;

    [ContextMenu("Create Dummy")]
    public void CreateDummy()
    {
        // Удаляем существующий манекен
        if (currentDummy != null)
        {
            DestroyImmediate(currentDummy.gameObject);
        }

        // Создаем новый манекен
        GameObject dummyObj;

        if (dummyPrefab != null)
        {
            dummyObj = Instantiate(dummyPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // Создаем простой манекен из примитивов
            dummyObj = CreateSimpleDummy();
        }

        // Настраиваем манекен
        currentDummy = dummyObj.GetComponent<Dummy>();
        if (currentDummy == null)
        {
            currentDummy = dummyObj.AddComponent<Dummy>();
        }

        // Настраиваем параметры
        currentDummy.SetMaxHealth(dummyHealth);

        Debug.Log($"🎯 Манекен создан в позиции {spawnPosition}");
    }

    /// <summary>
    /// Создание простого манекена из примитивов
    /// </summary>
    private GameObject CreateSimpleDummy()
    {
        GameObject dummy = new GameObject("Dummy");
        dummy.transform.position = spawnPosition;

        // Создаем тело (цилиндр)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(dummy.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(1, 2, 1);

        // Создаем голову (сфера)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(dummy.transform);
        head.transform.localPosition = new Vector3(0, 1.5f, 0);
        head.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

        // Создаем руки (кубы)
        GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftArm.name = "LeftArm";
        leftArm.transform.SetParent(dummy.transform);
        leftArm.transform.localPosition = new Vector3(-0.7f, 0.5f, 0);
        leftArm.transform.localScale = new Vector3(0.3f, 1, 0.3f);

        GameObject rightArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightArm.name = "RightArm";
        rightArm.transform.SetParent(dummy.transform);
        rightArm.transform.localPosition = new Vector3(0.7f, 0.5f, 0);
        rightArm.transform.localScale = new Vector3(0.3f, 1, 0.3f);

        // Создаем ноги (кубы)
        GameObject leftLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftLeg.name = "LeftLeg";
        leftLeg.transform.SetParent(dummy.transform);
        leftLeg.transform.localPosition = new Vector3(-0.3f, -1.5f, 0);
        leftLeg.transform.localScale = new Vector3(0.4f, 1, 0.4f);

        GameObject rightLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightLeg.name = "RightLeg";
        rightLeg.transform.SetParent(dummy.transform);
        rightLeg.transform.localPosition = new Vector3(0.3f, -1.5f, 0);
        rightLeg.transform.localScale = new Vector3(0.4f, 1, 0.4f);

        // Настраиваем материал
        Material dummyMaterial = new Material(Shader.Find("Standard"));
        dummyMaterial.color = Color.gray;

        Renderer[] renderers = dummy.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material = dummyMaterial;
        }

        // Добавляем коллайдер
        if (dummy.GetComponent<Collider>() == null)
        {
            CapsuleCollider collider = dummy.AddComponent<CapsuleCollider>();
            collider.height = 3f;
            collider.radius = 0.5f;
            collider.center = Vector3.zero;
        }

        return dummy;
    }

    /// <summary>
    /// Удаление манекена
    /// </summary>
    [ContextMenu("Destroy Dummy")]
    public void DestroyDummy()
    {
        if (currentDummy != null)
        {
            DestroyImmediate(currentDummy.gameObject);
            currentDummy = null;
            Debug.Log("🎯 Манекен удален");
        }
    }

    /// <summary>
    /// Сброс здоровья манекена
    /// </summary>
    [ContextMenu("Reset Dummy Health")]
    public void ResetDummyHealth()
    {
        if (currentDummy != null)
        {
            currentDummy.ResetHealth();
            Debug.Log("🎯 Здоровье манекена сброшено");
        }
    }

    /// <summary>
    /// Переключение неуязвимости манекена
    /// </summary>
    [ContextMenu("Toggle Dummy Invincibility")]
    public void ToggleDummyInvincibility()
    {
        if (currentDummy != null)
        {
            currentDummy.ToggleInvincibility();
        }
    }

    private void OnGUI()
    {
        if (!enableDebugUI) return;

        GUILayout.BeginArea(new Rect(10, 750, 300, 150));
        GUILayout.Label("=== DUMMY CREATOR ===");

        if (GUILayout.Button("Create Dummy"))
        {
            CreateDummy();
        }

        if (GUILayout.Button("Destroy Dummy"))
        {
            DestroyDummy();
        }

        if (GUILayout.Button("Reset Dummy Health"))
        {
            ResetDummyHealth();
        }

        if (GUILayout.Button("Toggle Invincibility"))
        {
            ToggleDummyInvincibility();
        }

        if (currentDummy != null)
        {
            GUILayout.Label($"Dummy: {currentDummy.GetDummyInfo()}");
        }
        else
        {
            GUILayout.Label("No Dummy");
        }

        GUILayout.EndArea();
    }
}
