using UnityEngine;
using UnityEngine.UI;

namespace IARVR.Visualization
{
    /// <summary>
    /// Binds a UI Slider to <see cref="MultiAxisPlotter.maxVisualHeight"/>.
    /// Triggers a full axis + connection refresh whenever the slider value changes.
    /// </summary>
    public class AxisHeightController : MonoBehaviour
    {
        // --- Serialized fields ----------------------------------------------

        [SerializeField] private MultiAxisPlotter _plotter;
        [SerializeField] private Slider           _heightSlider;
        [SerializeField] private float            _minHeight = 1f;
        [SerializeField] private float            _maxHeight = 10f;

        // --- Unity lifecycle ------------------------------------------------

        private void Start()
        {
            if (_plotter == null)
                _plotter = FindObjectOfType<MultiAxisPlotter>();

            if (_heightSlider == null)
            {
                Debug.LogWarning("[AxisHeightController] No Slider assigned.");
                return;
            }

            InitializeSlider();
        }

        // --- Private helpers ------------------------------------------------

        private void InitializeSlider()
        {
            _heightSlider.minValue = _minHeight;
            _heightSlider.maxValue = _maxHeight;
            _heightSlider.value    = _plotter.maxVisualHeight;

            _heightSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        private void OnSliderChanged(float newHeight)
        {
            if (_plotter == null) return;

            _plotter.maxVisualHeight = newHeight;
            _plotter.UpdateAxesHeight();
            _plotter.MarkConnectionsDirty();
        }
    }
}