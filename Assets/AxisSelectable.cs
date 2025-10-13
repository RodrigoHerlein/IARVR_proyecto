using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Renderer))]
public class AxisSelectable : XRBaseInteractable
{
    /*
        Permite seleccionar ejes (cilindros) con el puntero láser.

        - Cambia de color al apuntar o seleccionar.
        - No usa física ni se mueve.
    */

    private Renderer rend;
    private Color originalColor;
    private Color hoverColor = Color.yellow;
    private Color selectedColor = Color.green;

    protected override void Awake()
    {
        base.Awake();
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalColor = rend.material.color;

        // Evitar que se mueva o tire física
        //movementType = MovementType.Kinematic;
    }

    protected override void OnHoverEntered(HoverEnterEventArgs args)
    {
        base.OnHoverEntered(args);
        SetColor(hoverColor);
    }

    protected override void OnHoverExited(HoverExitEventArgs args)
    {
        base.OnHoverExited(args);
        SetColor(originalColor);
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        SetColor(selectedColor);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        SetColor(originalColor);
    }

    private void SetColor(Color c)
    {
        if (rend != null)
            rend.material.color = c;
    }
}
