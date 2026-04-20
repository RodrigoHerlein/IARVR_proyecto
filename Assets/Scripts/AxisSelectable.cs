using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace IARVR.Visualization
{
    /// <summary>
    /// Attached to each axis parent. Allows the user to grab and reposition
    /// an axis in VR. Notifies <see cref="MultiAxisPlotter"/> to refresh
    /// connections while the axis is held.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AxisSelectable : MonoBehaviour
    {
        // --- Serialized fields ----------------------------------------------

        [Tooltip("Fallback collider half-height when no Collider is found on the axis.")]
        [SerializeField] private float defaultColliderHalfHeight = 2.5f;

        [Tooltip("Fallback collider radius when no Collider is found on the axis.")]
        [SerializeField] private float defaultColliderRadius = 0.2f;

        // --- Private state --------------------------------------------------

        private MeshRenderer      _meshRenderer;
        private Color             _originalColor;
        private XRGrabInteractable _grab;
        private MultiAxisPlotter  _plotter;

        // --- Unity lifecycle ------------------------------------------------

        private void Awake()
        {
            CacheComponents();
            EnsureRigidbody();
            EnsureCollider();
            ConfigureGrab();

            _plotter = FindObjectOfType<MultiAxisPlotter>();
        }

        private void OnDestroy()
        {
            if (_grab == null) return;

            _grab.selectEntered.RemoveListener(OnGrab);
            _grab.selectExited.RemoveListener(OnRelease);
        }

        // --- Grab callbacks -------------------------------------------------

        private void OnGrab(SelectEnterEventArgs args)
        {
            if (_meshRenderer != null)
                _meshRenderer.material.color = Color.green;

            StartCoroutine(MarkDirtyWhileGrabbed());
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (_meshRenderer != null)
                _meshRenderer.material.color = _originalColor;

            _plotter?.MarkConnectionsDirty();
        }

        // --- Private helpers ------------------------------------------------

        private void CacheComponents()
        {
            _meshRenderer = GetComponentInChildren<MeshRenderer>();

            if (_meshRenderer != null)
                _originalColor = _meshRenderer.material.color;
            else
                Debug.LogWarning($"[AxisSelectable] No MeshRenderer found on '{name}'.");
        }

        private void EnsureRigidbody()
        {
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        private void EnsureCollider()
        {
            if (GetComponent<Collider>() != null) return;

            var capsule        = gameObject.AddComponent<CapsuleCollider>();
            capsule.center     = new Vector3(0f, defaultColliderHalfHeight, 0f);
            capsule.height     = defaultColliderHalfHeight * 2f;
            capsule.radius     = defaultColliderRadius;
        }

        private void ConfigureGrab()
        {
            _grab = GetComponent<XRGrabInteractable>()
                    ?? gameObject.AddComponent<XRGrabInteractable>();

            _grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            _grab.trackRotation = false;
            _grab.throwOnDetach  = false;

            _grab.selectEntered.AddListener(OnGrab);
            _grab.selectExited.AddListener(OnRelease);
        }

        private IEnumerator MarkDirtyWhileGrabbed()
        {
            while (_grab.isSelected)
            {
                _plotter?.MarkConnectionsDirty();
                yield return null;
            }
        }
    }
}