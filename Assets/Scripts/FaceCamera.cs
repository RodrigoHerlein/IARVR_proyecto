using UnityEngine;

namespace IARVR.Visualization
{
    /// <summary>
    /// Rotates this GameObject to always face the main camera.
    /// Used by axis labels to remain readable from any angle.
    /// </summary>
    public class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main == null) return;

            transform.rotation = Quaternion.LookRotation(
                transform.position - Camera.main.transform.position);
        }
    }
}