using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AxisSelectable : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Color originalColor;
    private XRGrabInteractable grab;
    private Rigidbody rb;
    private MultiAxisPlotter plotter;

    void Awake()
    {
        // Busca el cilindro o cualquier MeshRenderer en los hijos
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer)
            originalColor = meshRenderer.material.color;
        else
            Debug.LogWarning($"[AxisSelectable] No encontró MeshRenderer en {name}");

        // Asegura que haya Rigidbody
        rb = GetComponent<Rigidbody>();
        if (!rb)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Asegura collider (usa el del cilindro si existe, o crea uno simple)
        Collider col = GetComponent<Collider>();
        if (!col)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 2.5f, 0);
            capsule.height = 5f;
            capsule.radius = 0.2f;
        }

        // Configura el grab interactable
        grab = GetComponent<XRGrabInteractable>();
        if (!grab)
            grab = gameObject.AddComponent<XRGrabInteractable>();

        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.trackRotation = false;
        grab.throwOnDetach = false;

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);

        plotter = FindObjectOfType<MultiAxisPlotter>(); // solo una vez en Awake
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (grab)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        if (meshRenderer)
            meshRenderer.material.color = Color.green;

        // Marcar dirty cada frame mientras está agarrado
        StartCoroutine(MarkDirtyWhileGrabbed());
    }

    private IEnumerator MarkDirtyWhileGrabbed()
    {
        while (grab.isSelected)
        {
            plotter?.MarkConnectionsDirty();
            yield return null; // esperar un frame
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (meshRenderer)
            meshRenderer.material.color = originalColor;

        // Avisarle al plotter que recalcule
        if (plotter != null)
            plotter.MarkConnectionsDirty();
    }
}
