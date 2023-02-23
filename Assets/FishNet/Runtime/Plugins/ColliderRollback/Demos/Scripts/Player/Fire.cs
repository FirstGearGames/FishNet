using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.ColliderRollback.Demo
{

    /// <summary>
    /// DEMO. CODE IS NOT OPTIMIZED.
    /// Fires at objects.
    /// </summary>
    public class Fire : NetworkBehaviour
    {
        //PROSTART
        /// <summary>
        /// Layer hitboxes are on.
        /// </summary>
        [Tooltip("Layer hitboxes are on.")]
        [SerializeField]
        private LayerMask _hitboxLayer;
        /// <summary>
        /// Audio to play when firing.
        /// </summary>
        [Tooltip("Audio to play when firing.")]
        [SerializeField]
        private AudioClip _audio;
        /// <summary>
        /// Muzzle flash to spawn.
        /// </summary>
        [Tooltip("Muzzle flash to spawn.")]
        [SerializeField]
        private GameObject _muzzleFlashPrefab;
        /// <summary>
        /// Next time player may fire.
        /// </summary>
        private float _nextFire = 0f;
        /// <summary>
        /// How often the player can fire.
        /// </summary>
        private const float FIRE_RATE = 0.2f;

        /// <summary>
        /// Aim component on this object.
        /// </summary>
        private Aim _aim;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner)
                _aim = GetComponent<Aim>();
        }

        private void Update()
        {
            if (base.IsOwner)
            {
                //CheckFire();
                CheckFireDemo();
            }
        }

        #region Real usage example.
        /// <summary>
        /// A demonstration of what firing in your game may look like.
        /// </summary>
        private void CheckFire()
        {
            if (Time.time < _nextFire)
                return;
            //Only fire on mouse0 down.
            if (!Input.GetKeyDown(KeyCode.Mouse0))
                return;

            _nextFire = Time.time + FIRE_RATE;

            Vector3 direction = _aim.PlayerCamera.transform.forward;
            Vector3 start = _aim.PlayerCamera.transform.position;
            ServerFire(base.TimeManager.GetPreciseTick(base.TimeManager.LastPacketTick), start, direction);
        }

        /// <summary>
        /// Fires using a specified fixed frame.
        /// </summary>
        /// <param name="fixedFrame"></param>
        [ServerRpc]
        private void ServerFire(PreciseTick pt, Vector3 start, Vector3 direction)
        {
            /* IMPORTANT.
             * base.IsOwner is passed into the Rollback
             * to indicate that this is being performed
             * on a host object. EG: If this object is owned by
             * the server then it must be the clientHost object. */

            //Notes
            /* Rollback using the frame sent in while
             * also including the interpolated reduction value. */
            base.RollbackManager.Rollback(pt, RollbackManager.PhysicsType.ThreeDimensional, base.IsOwner);

            /* Perform your raycast here using typical values.
             * EG: Trace from start, to direction, on whatever hit
             * layers you would normally use. Process hit results normally. */

            /* After performing your trace it's important to call RollbackManager.ReturnForward(). */
            base.RollbackManager.Return();
        }
        #endregion

        #region Demo.
        /// <summary>
        /// Only sends fire command if an object is hit locally to test accuracy.
        /// </summary>
        private void CheckFireDemo()
        {
            if (Time.time < _nextFire)
                return;
            //Only fire on mouse0 down.
            if (!Input.GetKeyDown(KeyCode.Mouse0))
                return;

            _nextFire = Time.time + FIRE_RATE;
            //Audio and muzzle flash effects.
            AudioSource.PlayClipAtPoint(_audio, _aim.PlayerCamera.transform.position);
            Instantiate(_muzzleFlashPrefab, _aim.PlayerCamera.MuzzleFlash.position, _aim.PlayerCamera.MuzzleFlash.rotation);

            Vector3 direction = _aim.PlayerCamera.transform.forward;
            Vector3 start = _aim.PlayerCamera.transform.position;

            Ray ray = new Ray(start, direction);
            RaycastHit hit;
            //If raycast hit.
            if (Physics.Raycast(ray, out hit, float.PositiveInfinity, _hitboxLayer))
            {
                //If moving enemy was hit.
                if (hit.transform.root.GetComponent<RollbackVisualizer>() != null)
                {
                    PreciseTick pt = base.TimeManager.GetPreciseTick(base.TimeManager.LastPacketTick);
                    //Send the frame, start, and direction.
                    /* The remaining arguments are used to calculate
                     * the accuracy between where client hit and where
                     * shot will register on server. There is no reason to
                     * really know these results other than for testing. */
                    ServerFireDemo(pt, start, direction,
                        hit.transform.root.GetComponent<NetworkObject>(), hit.transform.position);
                }
            }
        }

        /// <summary>
        /// Fires using a specified fixed frame.
        /// </summary>
        /// <param name="tick"></param>
        [ServerRpc]
        private void ServerFireDemo(PreciseTick pt, Vector3 start, Vector3 direction, NetworkObject hitObject, Vector3 hitIdentityPosition)
        {
            Transform hitChild = hitObject.transform.GetChild(0);
            /* Rollback using the frame sent in
             * while subtracting frames for interpolation, such
             * as a NetworkTransform moving an object into position. */
            base.RollbackManager.Rollback(pt, RollbackManager.PhysicsType.ThreeDimensional, base.IsOwner);
            /* This is where you would use the start
             * and direction to fire your raycast. This method is
             * only used to show accuracy so I won't be using those here. */

            //The hitbox is hard set to first child for demo.
            Vector3 rollbackPosition = hitChild.position;
            float difference = Vector3.Distance(hitIdentityPosition, rollbackPosition);
            //if (canRollback)
            base.RollbackManager.Return();

            //Distance object root is after rollback, in comparison to where it was when client hit it.
            //Only print if also not client.
            if (!base.IsClient)
                Debug.Log($"Accuracy is within {difference} units.");
            RollbackVisualizer med = hitObject.GetComponent<RollbackVisualizer>();
            med.ShowDifference(base.NetworkObject, hitIdentityPosition, rollbackPosition);
        }
        #endregion
        //PROEND

    }
}

