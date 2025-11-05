using UnityEngine;
using UnityEngine.UI;

public class AxisHeightController : MonoBehaviour
{
    [SerializeField] private MultiAxisPlotter plotter;
    [SerializeField] private Slider heightSlider;
    [SerializeField] private float minHeight = 1f;
    [SerializeField] private float maxHeight = 10f;

    void Start()
    {
        if (plotter == null)
            plotter = FindObjectOfType<MultiAxisPlotter>();

        if (heightSlider != null)
        {
            // Inicializamos el slider con el valor actual
            heightSlider.minValue = minHeight;
            heightSlider.maxValue = maxHeight;
            heightSlider.value = plotter.maxVisualHeight;

            // Escuchar cambios en el slider
            heightSlider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void OnSliderChanged(float newHeight)
    {
        if (plotter == null) return;

        plotter.maxVisualHeight = newHeight;

        // Actualizamos visualmente los ejes
        plotter.UpdateAxesHeight();
    }
}
