using FishNet.Object;
using UnityEngine;

namespace FishNet.Example.ColliderRollbacks
{
    /// <summary>
    /// DEMO. CODE IS NOT OPTIMIZED.
    /// Aims the camera.
    /// </summary>
    public class Aim : NetworkBehaviour
    {
        public PlayerCamera PlayerCamera { get; private set; }
        private readonly Vector3 _offset = new(0f, 1.65f, 0f);

        public override void OnStartClient()
        {
            if (IsOwner)
                PlayerCamera = Camera.main.transform.GetComponent<PlayerCamera>();
        }

        private void Update()
        {
            if (!IsOwner || PlayerCamera == null)
                return;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            MoveAim();
            MoveCamera();
        }

        /// <summary>
        /// Aims camera.
        /// </summary>
        private void MoveAim()
        {
            float speed = 2f;
            // Yaw.
            transform.Rotate(new(0f, Input.GetAxis("Mouse X") * speed, 0f));
            // Pitch.
            float pitch = PlayerCamera.transform.eulerAngles.x - Input.GetAxis("Mouse Y") * speed;
            /* If not signed on X then make it
             * signed for easy clamping. */
            if (pitch > 180f)
                pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            PlayerCamera.transform.eulerAngles = new(pitch, transform.eulerAngles.y, transform.eulerAngles.z);
        }

        /// <summary>
        /// Moves camera.
        /// </summary>
        private void MoveCamera()
        {
            PlayerCamera.transform.position = transform.position + _offset;
            PlayerCamera.transform.rotation = Quaternion.Euler(PlayerCamera.transform.eulerAngles.x, transform.eulerAngles.y, transform.eulerAngles.z);
        }
    }
}