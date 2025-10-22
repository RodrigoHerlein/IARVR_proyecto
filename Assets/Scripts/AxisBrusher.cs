using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class AxisBrusher : MonoBehaviour
{
    public int axisIndex;
    public Color brushColor = Color.cyan;

    [Tooltip("Which button starts the brush (e.g. gripButton, triggerButton, primaryButton)")]
    public InputFeatureUsage<bool> brushButton = CommonUsages.primaryButton;

    private bool isBrushing = false;
    private Vector3 brushStartWorld;
    private Vector3 brushEndWorld;

    private MultiAxisPlotter plotter;
    private LineRenderer brushLine;

    private XRRayInteractor rayInteractor;
    private bool lastButtonState = false;
    private XRNode controllerNode = XRNode.RightHand; // fallback

    void Start()
    {
        plotter = FindObjectOfType<MultiAxisPlotter>();

        // Create visual brush line
        brushLine = new GameObject("BrushLine").AddComponent<LineRenderer>();
        brushLine.transform.SetParent(transform.root, true); // no lo atamos al cilindro (usaremos axisParent para posiciones)
        brushLine.material = new Material(Shader.Find("Sprites/Default"));
        brushLine.startWidth = brushLine.endWidth = 0.05f;
        brushLine.positionCount = 2;
        brushLine.enabled = false;

        // Find XRRayInteractor (your hand laser)
        rayInteractor = FindObjectOfType<XRRayInteractor>();
        if (rayInteractor == null)
        {
            Debug.LogWarning("[AxisBrusher] No XRRayInteractor found in scene.");
            return;
        }

        // Try to infer which controller this interactor belongs to
        var interactorGO = rayInteractor.gameObject.name.ToLower();
        if (interactorGO.Contains("left")) controllerNode = XRNode.LeftHand;
        else if (interactorGO.Contains("right")) controllerNode = XRNode.RightHand;
    }

    void Update()
    {
        if (rayInteractor == null || plotter == null)
            return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid) return;

        bool buttonPressed = false;
        device.TryGetFeatureValue(brushButton, out buttonPressed);

        if (buttonPressed && !lastButtonState)
            TryBeginBrush();
        else if (!buttonPressed && lastButtonState)
            TryEndBrush();

        // 🔁 mientras el botón está presionado, seguimos actualizando la visual
        if (isBrushing)
            UpdateBrushFromRay(buttonPressed);

        lastButtonState = buttonPressed;
    }

    private void TryBeginBrush()
    {
        if (isBrushing) return;

        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // continuar sólo si el hit pertenece a este eje
            if (hit.collider != null && hit.collider.transform.IsChildOf(plotter.GetAxisParent(axisIndex)))
            {
                BeginBrush(hit.point);
            }
        }
    }

    private void UpdateBrushFromRay(bool buttonHeld)
    {
        if (!isBrushing) return;

        // si sigue golpeando el eje, actualizamos con el punto real
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit) &&
            hit.collider != null &&
            hit.collider.transform.IsChildOf(plotter.GetAxisParent(axisIndex)))
        {
            brushEndWorld = hit.point;
        }
        else if (buttonHeld)
        {
            // si no golpea, mantenemos el último punto válido (sin desaparecer)
        }

        UpdateBrushVisual();
    }

    private void TryEndBrush()
    {
        if (!isBrushing) return;

        // usamos el último punto válido, no dependemos del raycast final
        EndBrush(brushEndWorld);
    }


    public void BeginBrush(Vector3 hitPointWorld)
    {
        isBrushing = true;
        brushStartWorld = hitPointWorld;
        brushEndWorld = brushStartWorld;
        UpdateBrushVisual();
    }

    public void UpdateBrush(Vector3 hitPointWorld)
    {
        if (!isBrushing) return;
        brushEndWorld = hitPointWorld;
        UpdateBrushVisual();
    }

    public void EndBrush(Vector3 hitPointWorld)
    {
        if (!isBrushing) return;
        isBrushing = false;
        brushLine.enabled = false;

        brushEndWorld = hitPointWorld;

        // Convert both world points to axis values using el plotter (corregido)
        float minVal = plotter.ValueFromWorldPoint(axisIndex, brushStartWorld);
        float maxVal = plotter.ValueFromWorldPoint(axisIndex, brushEndWorld);

        if (minVal > maxVal)
        {
            float tmp = minVal; minVal = maxVal; maxVal = tmp;
        }

        plotter.HighlightRange(axisIndex, minVal, maxVal, brushColor);
    }

    private void UpdateBrushVisual()
    {
        // Para dibujar la línea de brushing, proyectamos ambos puntos al sistema de coordenadas del axis parent
        Transform axisParent = plotter.GetAxisParent(axisIndex);
        if (axisParent == null)
        {
            // fallback: dibujar en world directamente
            brushLine.enabled = true;
            brushLine.SetPosition(0, brushStartWorld);
            brushLine.SetPosition(1, brushEndWorld);
            brushLine.startColor = brushLine.endColor = brushColor;
            return;
        }

        // Ajustamos para que la visual del brush quede sobre (o muy cerca) del eje:
        Vector3 startLocal = axisParent.InverseTransformPoint(brushStartWorld);
        Vector3 endLocal = axisParent.InverseTransformPoint(brushEndWorld);

        // Clamp Y para que no salga fuera del eje visual
        float maxH = plotter.maxVisualHeight;
        startLocal.y = Mathf.Clamp(startLocal.y, 0f, maxH);
        endLocal.y = Mathf.Clamp(endLocal.y, 0f, maxH);

        Vector3 worldA = axisParent.TransformPoint(new Vector3(0f, startLocal.y, 0f));
        Vector3 worldB = axisParent.TransformPoint(new Vector3(0f, endLocal.y, 0f));

        brushLine.enabled = true;
        brushLine.SetPosition(0, worldA);
        brushLine.SetPosition(1, worldB);
        brushLine.startColor = brushLine.endColor = brushColor;
    }
}
