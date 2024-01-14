using FishNet.Component.Prediction;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine;

namespace FishNet.Example.Prediction.Rigidbodies
{

    public class PredictedBullet : NetworkBehaviour
    {
        //SyncVar to set spawn force. This is set by predicted spawner and sent to the server.
        [HideInInspector, SyncVar(OnChange = nameof(_startingForce_OnChange))]
        private Vector3 _startingForce;
        //Tick to set rb to kinematic.
        private uint _stopTick = TimeManager.UNSET_TICK;

        /* In this example this method is called by the client
         * after it Instanties the object locally. This occurs before
         * the client calls network spawn on it. */
        public void SetStartingForce(Vector3 value)
        {
            /* Set the SyncVar so it is sent to the server when this
             * object is spawned. This will only send to the server if
             * values are set before network spawning. */
            _startingForce = value;
        }

        //Simple delay destroy so object does not exist forever.
        public override void OnStartServer()
        {
            StartCoroutine(__DelayDestroy(3f));

            //Set velocity to starting force.
            SetVelocity(_startingForce);
            //Server can still override syncvars set by the predicted spawner.
            Debug.Log("Setting new force.");
            _startingForce = Vector3.one;
        }

        public override void OnStartNetwork()
        {
            uint timeToTicks = base.TimeManager.TimeToTicks(0.65f);
            /* If server or predicted spawner then add the kinematic
             * tick onto local. Predicted spawner and server should behave
             * as though no time has elapsed since this spawned. */
            if (base.IsServer || base.Owner.IsLocalClient)
            {
                _stopTick = base.TimeManager.LocalTick + timeToTicks;
            }
            /* If not server or a client that did not predicted spawn this
             * then calculate time passed and set kinematic tick to the same
             * amount in the future while subtracting already passed ticks. */
            else
            {
                uint passed = (uint)Mathf.Max(1, base.TimeManager.Tick - base.TimeManager.LastPacketTick);
                long stopTick = (base.TimeManager.Tick + timeToTicks - passed - 1);
                if (stopTick > 0)
                    _stopTick = (uint)stopTick;
                //Time already passed, set to stop next tick.
                else
                    _stopTick = 1;
            }

            base.TimeManager.OnTick += TimeManager_OnTick;
        }

        public override void OnStopNetwork()
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
        }
        private void TimeManager_OnTick()
        {
            if (_stopTick > 0 && base.TimeManager.LocalTick >= _stopTick)
            {
                Rigidbody rb = GetComponent<Rigidbody>();
                rb.isKinematic = true;
            }
        }

        /// <summary>
        /// When starting force changes set that velocity to the rigidbody.
        /// This is an example as how a predicted spawn can modify sync types for server and other clients.
        /// </summary>
        private void _startingForce_OnChange(Vector3 prev, Vector3 next, bool asServer)
        {
            SetVelocity(next);
        }

        /// <summary>
        /// Sets velocity of the rigidbody.
        /// </summary>
        public void SetVelocity(Vector3 value)
        {
            Debug.Log($"Setting velocity on {gameObject.name} to {value}");
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.velocity = value;
        }

        /// <summary>
        /// Destroy object after time.
        /// </summary>
        private IEnumerator __DelayDestroy(float time)
        {
            yield return new WaitForSeconds(time);
            base.Despawn();
        }

    }


}