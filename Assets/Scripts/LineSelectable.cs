using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(LineRenderer))]
public class LineSelectable : MonoBehaviour
{
    // Config
    [SerializeField] float colliderRadius = 0.05f;
    [SerializeField] float grabMoveThreshold = 0.001f; // unused but handy if querés lógica adicional

    // Runtime
    LineRenderer lr;
    CapsuleCollider capsule;
    XRGrabInteractable grabInteractable;
    Rigidbody rb;
    Color originalColor;
    public int rowIndex; // índice de fila (seteado por MultiAxisPlotter)
    private MultiAxisPlotter _plotter;

    void Awake()
    {
        // LineRenderer obligatorio
        lr = GetComponent<LineRenderer>();
        if (lr == null)
        {
            Debug.LogError("LineSelectable requires a LineRenderer");
            enabled = false;
            return;
        }

        // Guarda color original (si no está definido, asigna uno)
        originalColor = lr.startColor;
        if (originalColor == default)
        {
            originalColor = Color.red;
            lr.startColor = lr.endColor = originalColor;
        }

        // Collider
        capsule = GetComponent<CapsuleCollider>();
        if (!capsule) capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = 2; // Z
        capsule.radius = colliderRadius;
        capsule.isTrigger = false; // NO trigger: importante para detección por ray/interactors

        // Rigidbody requerido por XRGrabInteractable (mejor tener uno)
        rb = GetComponent<Rigidbody>();
        if (!rb)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // dejamos kinematic para que no afecte física global
            rb.useGravity = false;
        }

        // XRGrabInteractable: lo creamos si no existe y suscribimos listeners aquí
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (!grabInteractable)
        {
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
        }

        // Asegurarse de no subscribir múltiples veces (por reloads)
        grabInteractable.selectEntered.RemoveListener(OnSelectEnteredEvent);
        grabInteractable.selectExited.RemoveListener(OnSelectExitedEvent);

        grabInteractable.selectEntered.AddListener(OnSelectEnteredEvent);
        grabInteractable.selectExited.AddListener(OnSelectExitedEvent);
    }

    public void SetPlotter(MultiAxisPlotter plotter)
    {
        _plotter = plotter;
    }

    void OnDestroy()
    {
        // limpieza de listeners
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEnteredEvent);
            grabInteractable.selectExited.RemoveListener(OnSelectExitedEvent);
        }
    }

    void LateUpdate()
    {
        // Actualiza collider para seguir la línea si tiene 2 posiciones válidas
        UpdateColliderToMatchLine();
    }

    void UpdateColliderToMatchLine()
    {
        if (lr == null || lr.positionCount < 2 || capsule == null) return;

        Vector3 p1 = lr.GetPosition(0);
        Vector3 p2 = lr.GetPosition(1);

        float length = Vector3.Distance(p1, p2);
        // Ajusta altura y radio
        capsule.height = Mathf.Max(0.01f, length);
        capsule.radius = Mathf.Max(0.001f, lr.startWidth / 2f);

        // Centrar y rotar transform para que el collider quede alineado
        transform.position = (p1 + p2) / 2f;
        if ((p2 - p1).sqrMagnitude > 0f)
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, p2 - p1);
    }

    // --- Eventos que se suscriben al XRGrabInteractable ---
    private void OnSelectEnteredEvent(SelectEnterEventArgs args)
    {
        SetLineColor(Color.green);

        _plotter?.HighlightRow(rowIndex, Color.green);
    }

    private void OnSelectExitedEvent(SelectExitEventArgs args)
    {
        SetLineColor(originalColor);

        _plotter?.HighlightRow(rowIndex, originalColor);
    }
/*
    private void OnSelectEnteredEvent(SelectEnterEventArgs args)
    {
        SetLineColor(Color.green);
    }

    private void OnSelectExitedEvent(SelectExitEventArgs args)
    {
        SetLineColor(originalColor);
    }*/

    private void SetLineColor(Color c)
    {
        if (lr == null) return;
        lr.startColor = c;
        lr.endColor = c;
    }
}
