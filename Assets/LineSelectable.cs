using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(LineRenderer))]
public class LineSelectable : XRBaseInteractable
{

     /*
        Es un objeto interactuable en VR (usa XR Interaction Toolkit).

        Requiere un LineRenderer (la línea que dibuja entre ejes).

        Crea un collider en forma de cápsula alineado a la línea → permite que el puntero láser de VR la detecte.

        Al seleccionar la línea, cambia su color a verde.

        Al soltarla, vuelve a su color original.
    */

    private LineRenderer lr;
    private Color originalColor;

   
    protected override void Awake()
    {
        base.Awake();
        lr = GetComponent<LineRenderer>();
        originalColor = lr.startColor;

        // Crea un collider que sigue la línea para poder seleccionarla con el láser en VR
        UpdateCollider();
    }

    private void UpdateCollider()
    {
        if (lr.positionCount < 2) return;

        Vector3 p1 = lr.GetPosition(0);
        Vector3 p2 = lr.GetPosition(1);

        GameObject colliderObj = new GameObject("LineCollider");
        colliderObj.transform.SetParent(transform, false);

        CapsuleCollider col = colliderObj.AddComponent<CapsuleCollider>();
        col.radius = 0.1f; // grosor del rayo de selección
        col.direction = 2; // Z
        col.center = (p1 + p2) / 2f;
        col.height = Vector3.Distance(p1, p2);
        colliderObj.transform.position = (p1 + p2) / 2f;
        colliderObj.transform.rotation = Quaternion.FromToRotation(Vector3.forward, p2 - p1);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        lr.startColor = Color.green; // cambia a verde al seleccionar
        lr.endColor = Color.green;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        lr.startColor = originalColor; // vuelve al color original al soltar
        lr.endColor = originalColor;
    }
}
