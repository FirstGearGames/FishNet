using FishNet.Documenting;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Prediction
{

    /// <summary>
    /// Base class for predicting rigidbodies for non-owners.
    /// </summary>
    [AddComponentMenu("")]
    [APIExclude]
    public abstract class PredictedRigidbodyBase : NetworkBehaviour
    {
        #region Protected.
        /// <summary>
        /// How often to synchronize values from server to clients when no changes have been detected.
        /// </summary>
        protected const float SEND_INTERVAL = 1f;        
        /// <summary>
        /// How much of the previous velocity to retain when predicting. Default value is 0f. Increasing this value may result in overshooting with rigidbodies that do not behave naturally, such as controllers or vehicles.
        /// </summary>
        [SerializeField, HideInInspector]
        protected float PredictionRatio;
        /// <summary>
        /// Sets PredictionRatio to value.
        /// </summary>
        /// <param name="value"></param>
        internal void SetPredictionRatio(float value) => PredictionRatio = value;
        #endregion

        #region Private
        /// <summary>
        /// True if subscribed to events.
        /// </summary>
        private bool _subscribed;
        /// <summary>
        /// Next tick to send data.
        /// </summary>
        private uint _nextSendTick;
        /// <summary>
        /// World position before transform was predicted or reset.
        /// </summary>
        private Vector3 _previousPosition;
        /// <summary>
        /// World rotation before transform was predicted or reset.
        /// </summary>
        private Quaternion _previoustRotation;
        /// <summary>
        /// Local position of transform when instantiated.
        /// </summary>
        private Vector3 _instantiatedPosition;
        /// <summary>
        /// How quickly to move towards TargetPosition.
        /// </summary>
        private float _positionMoveRate;
        /// <summary>
        /// Local rotation of transform when instantiated.
        /// </summary>
        private Quaternion _instantiatedRotation;
        /// <summary>
        /// How quickly to move towards TargetRotation.
        /// </summary>
        private float _rotationMoveRate;
        #endregion



        protected virtual void Awake()
        {
            _instantiatedPosition = transform.localPosition;
            _instantiatedRotation = transform.localRotation;
        }


        protected virtual void Update()
        {
            MoveToTarget();
        }


        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ChangeSubscriptions(true);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            ChangeSubscriptions(false);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager == null)
                return;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        /// <summary>
        /// Called after a tick occurs; physics would have simulated if using PhysicsMode.TimeManager.
        /// </summary>
        protected virtual void TimeManager_OnPostTick()
        {
            if (base.IsServer)
            {

                if (base.TimeManager.LocalTick >= _nextSendTick || base.TransformMayChange())
                {
                    uint ticksRequired = base.TimeManager.TimeToTicks(SEND_INTERVAL, TickRounding.RoundUp);
                    _nextSendTick += ticksRequired;
                    SendRigidbodyState();
                }
            }
        }

        /// <summary>
        /// Subscribes to events needed to function.
        /// </summary>
        /// <param name="subscribe"></param>
        private void ChangeSubscriptions(bool subscribe)
        {
            if (base.TimeManager == null)
                return;
            if (subscribe == _subscribed)
                return;

            if (subscribe)
            {
                base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReplicateReplay += TimeManager_OnPostReplicateReplay;
                base.TimeManager.OnPostReconcile += TimeManager_OnPostReconcile;
            }
            else
            {
                base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReplicateReplay -= TimeManager_OnPostReplicateReplay;
                base.TimeManager.OnPostReconcile -= TimeManager_OnPostReconcile;
            }

            _subscribed = subscribe;
        }

        internal abstract void SetRigidbody(Rigidbody rb);
        internal abstract void SetRigidbody(Rigidbody2D rb2d);

        /// <summary>
        /// Tries to predict velocity for a Vector3.
        /// </summary>
        protected bool PredictVector3Velocity(ref float? velocityBaseline, ref Vector3 lastVelocity, Vector3 velocity, out Vector3 result)
        {
            float velocityDifference;
            float directionDifference;

            /* Velocity. */
            directionDifference = (velocityBaseline != null) ?
                Vector3.SqrMagnitude(lastVelocity.normalized - velocity.normalized) :
                0f;
            //If direction has changed too much then reset the baseline.
            if (directionDifference > 0.01f)
            {
                velocityBaseline = null;
            }
            //Direction hasn't changed enough to reset baseline.
            else
            {
                //Difference in velocity since last simulation.
                velocityDifference = Vector3.Magnitude(lastVelocity - velocity);
                //If there is no baseline.
                if (velocityBaseline == null)
                {
                    if (velocityDifference > 0)
                        velocityBaseline = velocityDifference;
                }
                //If there is a baseline.
                else
                {
                    //If the difference exceeds the baseline by 10% then reset baseline so another will be calculated.
                    if (velocityDifference > (velocityBaseline.Value * 1.1f) || velocityDifference < (velocityBaseline.Value * 0.9f))
                    {
                        velocityBaseline = null;
                    }
                    //Velocity difference is close enough to the baseline to where it doesn't need to be reset, so use prediction.
                    else
                    {
                        result = Vector3.Lerp(velocity, lastVelocity, PredictionRatio);
                        return true;
                    }
                }
            }

            //Fall through.
            result = Vector3.zero;
            return false;
        }


        /// <summary>
        /// Tries to predict velocity for a float.
        /// </summary>
        protected bool PredictFloatVelocity(ref float? velocityBaseline, ref float lastVelocity, float velocity, out float result)
        {
            float velocityDifference;
            float directionDifference;

            /* Velocity. */
            directionDifference = (velocityBaseline != null) ? (velocity - lastVelocity) : 0f;

            //If direction has changed too much then reset the baseline.
            if (directionDifference > 0.01f)
            {
                velocityBaseline = null;
            }
            //Direction hasn't changed enough to reset baseline.
            else
            {
                //Difference in velocity since last simulation.
                velocityDifference = Mathf.Abs(lastVelocity - velocity);
                //If there is no baseline.
                if (velocityBaseline == null)
                {
                    if (velocityDifference > 0)
                        velocityBaseline = velocityDifference;
                }
                //If there is a baseline.
                else
                {
                    //If the difference exceeds the baseline by 10% then reset baseline so another will be calculated.
                    if (velocityDifference > (velocityBaseline.Value * 1.1f) || velocityDifference < (velocityBaseline.Value * 0.9f))
                    {
                        velocityBaseline = null;
                    }
                    //Velocity difference is close enough to the baseline to where it doesn't need to be reset, so use prediction.
                    else
                    {
                        result = Mathf.Lerp(velocity, lastVelocity, PredictionRatio);
                        return true;
                    }
                }
            }

            //Fall through.
            result = 0f;
            return false;
        }


        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        protected virtual void TimeManager_OnPreReconcile(NetworkBehaviour obj) { }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        protected virtual void TimeManager_OnPostReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d) { }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        protected virtual void TimeManager_OnPostReconcile(NetworkBehaviour obj) { }


        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        private void MoveToTarget()
        {
            if (!CanPredict())
                return;
            if (TransformLocalMatches(_instantiatedPosition, _instantiatedRotation))
                return;

            float delta = Time.deltaTime;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, _instantiatedPosition, _positionMoveRate * delta);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, _instantiatedRotation, _rotationMoveRate * delta);
        }


        /// <summary>
        /// Sets Start or Target Position and Rotation for the transform.
        /// </summary>
        protected void SetPreviousStates()
        {
            _previousPosition = transform.position;
            _previoustRotation = transform.rotation;
        }

        /// <summary>
        /// Resets the transform to StartPosition and StartRotation.
        /// </summary>
        protected void ResetToTransformPrevious()
        {
            transform.position = _previousPosition;
            transform.rotation = _previoustRotation;
        }

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        protected void SetTransformMoveRates()
        {
            float tickDelta = (float)base.TimeManager.TickDelta;
            float distance;

            distance = Vector3.Distance(_previousPosition, transform.position);
            _positionMoveRate = (distance / tickDelta);
            distance = Quaternion.Angle(_previoustRotation, transform.rotation);
            if (distance > 0f)
                _rotationMoveRate = (distance / tickDelta);
        }


        /// <summary>
        /// Sends the current state of the rigidbody to clients.
        /// </summary>
        protected abstract void SendRigidbodyState();

        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        protected bool CanPredict()
        {
            if (base.IsServer || base.IsOwner)
                return false;

            return true;
        }


        /// <summary>
        /// Returns if this transform matches arguments.
        /// </summary>
        /// <returns></returns>
        protected bool TransformLocalMatches(Vector3 position, Quaternion rotation)
        {
            return (transform.localPosition == position && transform.localRotation == rotation);
        }
    }

}