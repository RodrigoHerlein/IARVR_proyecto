using UnityEngine;

public class SelectableObject : MonoBehaviour
{

    /*
    Qué hace:

        Permite a cualquier objeto con Renderer cambiar de color temporalmente.

        Sirve para resaltar ejes o segmentos cuando se apunta o selecciona.

        Método Highlight() → pone amarillo.

        Método ResetColor() → vuelve al color original.

    */
    private Renderer rend;
    private Color originalColor;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalColor = rend.material.color;
    }

    public void Highlight()
    {
        if (rend != null)
            rend.material.color = Color.yellow;
    }

    public void ResetColor()
    {
        if (rend != null)
            rend.material.color = originalColor;
    }
}
