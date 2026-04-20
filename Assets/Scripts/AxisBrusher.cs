using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace IARVR.Visualization
{
    /// <summary>
    /// Attached to each axis cylinder. Allows the user to drag a range
    /// selection along the axis using an XR controller button.
    /// Fires <see cref="MultiAxisPlotter.HighlightRange"/> on release.
    /// </summary>
    public class AxisBrusher : MonoBehaviour
    {
        // --- Serialized fields ----------------------------------------------

        [Tooltip("Index of the axis this brusher belongs to.")]
        public int AxisIndex;

        [Tooltip("Color applied to data points inside the brushed range.")]
        public Color BrushColor = Color.cyan;

        [Tooltip("Controller button that activates brushing (e.g. primaryButton, triggerButton).")]
        public InputFeatureUsage<bool> BrushButton = CommonUsages.primaryButton;

        // --- Private state --------------------------------------------------

        private MultiAxisPlotter _plotter;
        private LineRenderer     _brushLine;
        private XRRayInteractor  _rayInteractor;
        private XRNode           _controllerNode = XRNode.RightHand;

        private bool    _isBrushing;
        private bool    _lastButtonState;
        private Vector3 _brushStartWorld;
        private Vector3 _brushEndWorld;

        // --- Unity lifecycle ------------------------------------------------

        private void Start()
        {
            _plotter = FindObjectOfType<MultiAxisPlotter>();

            CreateBrushLine();
            ResolveRayInteractor();
        }

        private void Update()
        {
            if (_rayInteractor == null || _plotter == null) return;

            InputDevice device = InputDevices.GetDeviceAtXRNode(_controllerNode);
            if (!device.isValid) return;

            device.TryGetFeatureValue(BrushButton, out bool buttonPressed);

            if (buttonPressed && !_lastButtonState)
                TryBeginBrush();
            else if (!buttonPressed && _lastButtonState)
                TryEndBrush();

            if (_isBrushing)
                UpdateBrushFromRay(buttonPressed);

            _lastButtonState = buttonPressed;
        }

        // --- Private brush logic --------------------------------------------

        private void TryBeginBrush()
        {
            if (_isBrushing) return;

            if (_rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit)
                && hit.collider != null
                && hit.collider.transform.IsChildOf(_plotter.GetAxisParent(AxisIndex)))
            {
                BeginBrush(hit.point);
            }
        }

        private void UpdateBrushFromRay(bool buttonHeld)
        {
            if (!_isBrushing) return;

            // Update end point only while the ray is still hitting this axis;
            // otherwise keep the last valid position.
            if (_rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit)
                && hit.collider != null
                && hit.collider.transform.IsChildOf(_plotter.GetAxisParent(AxisIndex)))
            {
                _brushEndWorld = hit.point;
            }

            UpdateBrushVisual();
        }

        private void TryEndBrush()
        {
            if (!_isBrushing) return;
            EndBrush(_brushEndWorld);
        }

        private void BeginBrush(Vector3 hitPointWorld)
        {
            _isBrushing      = true;
            _brushStartWorld = hitPointWorld;
            _brushEndWorld   = hitPointWorld;
            UpdateBrushVisual();
        }

        private void EndBrush(Vector3 hitPointWorld)
        {
            if (!_isBrushing) return;

            _isBrushing      = false;
            _brushEndWorld   = hitPointWorld;
            _brushLine.enabled = false;

            float minVal = _plotter.ValueFromWorldPoint(AxisIndex, _brushStartWorld);
            float maxVal = _plotter.ValueFromWorldPoint(AxisIndex, _brushEndWorld);

            if (minVal > maxVal)
                (minVal, maxVal) = (maxVal, minVal); // swap via tuple deconstruction

            _plotter.HighlightRange(AxisIndex, minVal, maxVal, BrushColor);
        }

        private void UpdateBrushVisual()
        {
            Transform axisParent = _plotter.GetAxisParent(AxisIndex);

            if (axisParent == null)
            {
                // Fallback: draw line in world space
                SetBrushLinePositions(_brushStartWorld, _brushEndWorld);
                return;
            }

            // Project both endpoints onto the axis (clamp to valid height range)
            float maxH       = _plotter.maxVisualHeight;
            Vector3 startLocal = axisParent.InverseTransformPoint(_brushStartWorld);
            Vector3 endLocal   = axisParent.InverseTransformPoint(_brushEndWorld);

            startLocal.y = Mathf.Clamp(startLocal.y, 0f, maxH);
            endLocal.y   = Mathf.Clamp(endLocal.y,   0f, maxH);

            Vector3 worldA = axisParent.TransformPoint(new Vector3(0f, startLocal.y, 0f));
            Vector3 worldB = axisParent.TransformPoint(new Vector3(0f, endLocal.y,   0f));

            SetBrushLinePositions(worldA, worldB);
        }

        // --- Private setup helpers ------------------------------------------

        private void CreateBrushLine()
        {
            _brushLine = new GameObject("BrushLine").AddComponent<LineRenderer>();
            _brushLine.transform.SetParent(transform.root, true);
            _brushLine.material     = new Material(Shader.Find("Sprites/Default"));
            _brushLine.startWidth   = _brushLine.endWidth = 0.05f;
            _brushLine.positionCount = 2;
            _brushLine.enabled       = false;
        }

        private void ResolveRayInteractor()
        {
            _rayInteractor = FindObjectOfType<XRRayInteractor>();

            if (_rayInteractor == null)
            {
                Debug.LogWarning("[AxisBrusher] No XRRayInteractor found in scene.");
                return;
            }

            string interactorName = _rayInteractor.gameObject.name.ToLower();

            if (interactorName.Contains("left"))
                _controllerNode = XRNode.LeftHand;
            else if (interactorName.Contains("right"))
                _controllerNode = XRNode.RightHand;
        }

        private void SetBrushLinePositions(Vector3 start, Vector3 end)
        {
            _brushLine.enabled    = true;
            _brushLine.SetPosition(0, start);
            _brushLine.SetPosition(1, end);
            _brushLine.startColor = _brushLine.endColor = BrushColor;
        }
    }
}