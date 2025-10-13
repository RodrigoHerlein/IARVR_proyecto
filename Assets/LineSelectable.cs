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

        // 👇 En lugar de crear un hijo, usamos un collider en el mismo GameObject
        CapsuleCollider col = gameObject.GetComponent<CapsuleCollider>();
        if (!col) col = gameObject.AddComponent<CapsuleCollider>();

        col.radius = 0.05f;
        col.direction = 2; // Z
        col.center = Vector3.zero; // se alinea con transform
        col.height = Vector3.Distance(p1, p2);

        // Alineamos el transform al centro de la línea
        transform.position = (p1 + p2) / 2f;
        transform.rotation = Quaternion.FromToRotation(Vector3.forward, p2 - p1);
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
