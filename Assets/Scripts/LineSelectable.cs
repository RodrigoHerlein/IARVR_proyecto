using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace IARVR.Visualization
{
    /// <summary>
    /// Attached to each polyline segment. Provides VR grab interaction,
    /// highlights the full row on select, and keeps a CapsuleCollider
    /// aligned with the LineRenderer every frame.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class LineSelectable : MonoBehaviour
    {
        // --- Serialized fields ----------------------------------------------

        [SerializeField] private float _colliderRadius = 0.05f;

        // --- Public state ---------------------------------------------------

        /// <summary>Index into <see cref="MultiAxisPlotter"/> row data.</summary>
        public int RowIndex { get; set; }

        // --- Private state --------------------------------------------------

        private LineRenderer       _lineRenderer;
        private CapsuleCollider    _capsule;
        private XRGrabInteractable _grabInteractable;
        private Color              _originalColor;
        private MultiAxisPlotter   _plotter;

        // --- Unity lifecycle ------------------------------------------------

        private void Awake()
        {
            if (!TryGetComponent(out _lineRenderer))
            {
                Debug.LogError("[LineSelectable] LineRenderer component is required.");
                enabled = false;
                return;
            }

            CacheOriginalColor();
            EnsureCapsuleCollider();
            EnsureRigidbody();
            ConfigureGrabInteractable();
        }

        private void OnDestroy()
        {
            if (_grabInteractable == null) return;

            _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            _grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        private void LateUpdate()
        {
            AlignColliderWithLine();
        }

        // --- Public API -----------------------------------------------------

        /// <summary>
        /// Injects the plotter reference. Called by <see cref="MultiAxisPlotter"/>
        /// at construction time to avoid per-event FindObjectOfType calls.
        /// </summary>
        public void SetPlotter(MultiAxisPlotter plotter) => _plotter = plotter;

        // --- Grab callbacks -------------------------------------------------

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            SetLineColor(Color.green);
            _plotter?.HighlightRow(RowIndex, Color.green);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            SetLineColor(_originalColor);
            _plotter?.HighlightRow(RowIndex, _originalColor);
        }

        // --- Private helpers ------------------------------------------------

        private void CacheOriginalColor()
        {
            _originalColor = _lineRenderer.startColor;

            if (_originalColor == default)
            {
                _originalColor = Color.red;
                _lineRenderer.startColor = _lineRenderer.endColor = _originalColor;
            }
        }

        private void EnsureCapsuleCollider()
        {
            _capsule = GetComponent<CapsuleCollider>();
            if (_capsule == null)
                _capsule = gameObject.AddComponent<CapsuleCollider>();

            _capsule.direction = 2; // Z-axis
            _capsule.radius    = _colliderRadius;
            _capsule.isTrigger = false;
        }

        private void EnsureRigidbody()
        {
            if (TryGetComponent<Rigidbody>(out _)) return;

            var rb         = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        private void ConfigureGrabInteractable()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>()
                                ?? gameObject.AddComponent<XRGrabInteractable>();

            // Guard against duplicate subscriptions on domain reload
            _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            _grabInteractable.selectExited.RemoveListener(OnSelectExited);

            _grabInteractable.selectEntered.AddListener(OnSelectEntered);
            _grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        private void AlignColliderWithLine()
        {
            if (_lineRenderer == null || _lineRenderer.positionCount < 2 || _capsule == null)
                return;

            Vector3 p1 = _lineRenderer.GetPosition(0);
            Vector3 p2 = _lineRenderer.GetPosition(1);

            _capsule.height = Mathf.Max(0.01f, Vector3.Distance(p1, p2));
            _capsule.radius = Mathf.Max(0.001f, _lineRenderer.startWidth / 2f);

            transform.position = (p1 + p2) * 0.5f;

            if ((p2 - p1).sqrMagnitude > 0f)
                transform.rotation = Quaternion.FromToRotation(Vector3.forward, p2 - p1);
        }

        private void SetLineColor(Color color)
        {
            if (_lineRenderer == null) return;
            _lineRenderer.startColor = _lineRenderer.endColor = color;
        }
    }
}