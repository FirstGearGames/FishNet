#if UNITYAI
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

namespace FishNet.Component.Navigation
{
    /// <summary>
    /// A leg of a NavMeshAgent's path, with the agent settings needed to walk it.
    /// </summary>
    public struct NavMeshAgentLeg
    {
        /// <summary>
        /// Where the leg began, in world space, being where the controlling agent stood when the leg was captured.
        /// </summary>
        public Vector3 PreviousPoint;
        /// <summary>
        /// The path point the agent is walking toward, in world space.
        /// </summary>
        public Vector3 NextPoint;
        /// <summary>
        /// True while the agent is halted or holds no path.
        /// </summary>
        public bool IsStopped;
        /// <summary>
        /// The agent's speed, in metres per second.
        /// </summary>
        public float Speed;
        /// <summary>
        /// The agent's angular speed, in degrees per second.
        /// </summary>
        public float AngularSpeed;
        /// <summary>
        /// The agent's acceleration, in metres per second squared.
        /// </summary>
        public float Acceleration;
    }

    /// <summary>
    /// Synchronizes a NavMeshAgent as the path it is walking rather than as the pose it holds.
    /// The controller sends the point the agent set off from and the point it is heading for, only when the agent begins a new leg.
    /// Every other peer walks its own copy along the leg at the sent speed, turning toward its direction of travel, with its own agent disabled.
    /// </summary>
    /// <remarks>
    /// Use this instead of NetworkTransform, not alongside it; both would move the transform.
    /// A walking agent sends nothing between corners, which costs far less than a pose stream. In exchange followers approximate rather than mirror:
    /// they walk at the sent speed and acceleration, while the controller's agent may be slowed further by avoidance or braking. Each new leg corrects that error.
    /// The controller is the server, or the owner when client authoritative and the object has an owner.
    /// </remarks>
    [AddComponentMenu("FishNet/Component/NetworkNavMeshAgent")]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NetworkNavMeshAgent : NetworkBehaviour
    {
        #region Public.
        /// <summary>
        /// The leg this object is walking, as last captured by the controller or received by a follower.
        /// </summary>
        public NavMeshAgentLeg Leg => _leg;
        /// <summary>
        /// The speed a follower is currently walking this object at. Zero on the controller, which walks its real agent instead.
        /// </summary>
        public float FollowerSpeed => _followerVelocity.magnitude;
        /// <summary>
        /// True if this peer drives the agent. False until the object is initialized.
        /// </summary>
        public bool IsAgentController
        {
            get
            {
                if (NetworkObject == null)
                    return false;

                return _clientAuthoritative && Owner.IsValid ? IsOwner : IsServerInitialized;
            }
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// True to let the owner drive the agent. When false, or while the object has no owner, the server drives it.
        /// </summary>
        [Tooltip("True to let the owner drive the agent. When false, or while the object has no owner, the server drives it.")]
        [SerializeField]
        private bool _clientAuthoritative;
        /// <summary>
        /// How far the agent's next path point must move before a new leg is sent, in metres.
        /// </summary>
        [Tooltip("How far the agent's next path point must move before a new leg is sent, in metres. Small values send more legs; large values let a real turn go unreported.")]
        [SerializeField]
        private float _pointChangeTolerance = 0.05f;
        /// <summary>
        /// How far a follower may be from a new leg's start before it begins closing the gap, in metres.
        /// </summary>
        [Tooltip("How far a follower may be from a new leg's start before it begins closing the gap, in metres. Below this it just retargets, so small corrections do not show.")]
        [SerializeField]
        private float _resyncDistance = 0.5f;
        /// <summary>
        /// How far a follower may be from a new leg's start before it is placed there outright rather than walking the gap off, in metres.
        /// </summary>
        [Tooltip("How far a follower may be from a new leg's start before it is placed there outright rather than walking the gap off, in metres.")]
        [SerializeField]
        private float _teleportDistance = 6f;
        /// <summary>
        /// True to turn followers toward their direction of travel. Rotation is derived from the path, never sent.
        /// </summary>
        [Tooltip("Turn followers toward their direction of travel. Rotation is derived from the path, never sent.")]
        [SerializeField]
        private bool _synchronizeRotation = true;
        /// <summary>
        /// True to enable the NavMeshAgent on the controller and disable it on followers, so nothing path-finds twice.
        /// </summary>
        [Tooltip("Enable the NavMeshAgent on the controller and disable it on followers, so nothing path-finds twice. Disable when other code owns the agent's enabled state.")]
        [SerializeField]
        private bool _manageAgentEnabled = true;
        #endregion

        #region Private.
        /// <summary>
        /// The leg the agent is walking.
        /// </summary>
        private NavMeshAgentLeg _leg;
        /// <summary>
        /// The agent on this object.
        /// </summary>
        private NavMeshAgent _navMeshAgent;
        /// <summary>
        /// True once the controller has captured a leg.
        /// </summary>
        private bool _hasCapturedLeg;
        /// <summary>
        /// True once a follower has been placed, by the server's seat or by its first received leg.
        /// </summary>
        private bool _hasSeatedFollower;
        /// <summary>
        /// True once a follower has received its first leg.
        /// </summary>
        private bool _hasReceivedLeg;
        /// <summary>
        /// True once the agent's enabled state has been applied for the current controller.
        /// </summary>
        private bool _hasAppliedAgentState;
        /// <summary>
        /// The controller state the agent's enabled state was last applied for.
        /// </summary>
        private bool _lastAppliedController;
        /// <summary>
        /// Consecutive captures which found the agent without a path, so a short gap between two paths is not mistaken for a stop.
        /// </summary>
        private int _idleCaptureCount;
        /// <summary>
        /// Displacement a follower owes from where it stands to where the last leg said it should be, walked off over a short time.
        /// </summary>
        private Vector3 _catchUpOffset;
        /// <summary>
        /// The velocity a follower is currently walking with.
        /// </summary>
        private Vector3 _followerVelocity;
        /// <summary>
        /// True while subscribed to the TimeManager tick.
        /// </summary>
        private bool _subscribedToTick;
        #endregion

        #region Const.
        /// <summary>
        /// How long a follower takes to walk off the ground it owes, in seconds.
        /// </summary>
        private const float CATCH_UP_SECONDS = 0.5f;
        /// <summary>
        /// The most a follower may add to its own speed while catching up, as a fraction of that speed.
        /// </summary>
        private const float MAXIMUM_CATCH_UP_FRACTION = 1f;
        /// <summary>
        /// Owed distance below which a correction is considered settled, in metres.
        /// </summary>
        private const float SETTLED_CATCH_UP_DISTANCE = 0.001f;
        /// <summary>
        /// Consecutive pathless captures needed before an agent is considered stopped.
        /// </summary>
        private const int IDLE_CAPTURES_BEFORE_STOP = 3;
        /// <summary>
        /// Squared horizontal length a heading must exceed to name a direction.
        /// </summary>
        private const float MINIMUM_FACING_SQUARED_DISTANCE = 1e-6f;
        #endregion

        private void Awake()
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnStartNetwork()
        {
            //The server already stands where the object is; clients wait to be placed.
            _hasSeatedFollower = IsServerStarted;
            ApplyAgentState();
            ChangeTickSubscription(true);
        }

        public override void OnStopNetwork()
        {
            ChangeTickSubscription(false);
            _leg = default;
            _hasCapturedLeg = false;
            _hasSeatedFollower = false;
            _hasReceivedLeg = false;
            _hasAppliedAgentState = false;
            _idleCaptureCount = 0;
            _catchUpOffset = Vector3.zero;
            _followerVelocity = Vector3.zero;
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            /* The last leg alone would place a joining client where the leg began, which may be well behind
             * the agent. Send where the agent is now and how it is moving. */
            Vector3 velocity = IsAgentController && _navMeshAgent.enabled ? _navMeshAgent.velocity : _followerVelocity;
            TargetSeatFollower(connection, transform.position, transform.rotation, velocity);
        }

        public override void OnOwnershipServer(NetworkConnection prevOwner) => ApplyAgentState();

        public override void OnOwnershipClient(NetworkConnection prevOwner) => ApplyAgentState();

        /// <summary>
        /// Sends the agent to a destination. Only works on the controller; the path is found there and only its legs are sent.
        /// </summary>
        /// <param name = "destination">World position to walk to.</param>
        /// <returns>True if the destination was accepted by the agent.</returns>
        public bool SetDestination(Vector3 destination) => CanDriveAgent() && _navMeshAgent.SetDestination(destination);

        /// <summary>
        /// Halts the agent where it stands. Only works on the controller; followers stop when the halt is received.
        /// </summary>
        public void Stop()
        {
            if (CanDriveAgent())
                _navMeshAgent.isStopped = true;
        }

        /// <summary>
        /// Returns the agent to walking its path. Only works on the controller.
        /// </summary>
        public void Resume()
        {
            if (CanDriveAgent())
                _navMeshAgent.isStopped = false;
        }

        /// <summary>
        /// Moves the agent to a position without walking there. Only works on the controller.
        /// </summary>
        /// <param name = "position">World position to move to.</param>
        /// <returns>True if the agent was placed.</returns>
        /// <remarks>Followers learn of the move through the next leg; they are placed there outright only when it is at least the teleport distance away.</remarks>
        public bool Warp(Vector3 position) => CanDriveAgent() && _navMeshAgent.Warp(position);

        private void Update()
        {
            if (!IsSpawned || IsAgentController || !_hasSeatedFollower)
                return;

            AdvanceFollower(Time.deltaTime);
        }

        /// <summary>
        /// Subscribes or unsubscribes from the TimeManager tick.
        /// </summary>
        private void ChangeTickSubscription(bool subscribe)
        {
            if (subscribe == _subscribedToTick || TimeManager == null)
                return;

            _subscribedToTick = subscribe;
            if (subscribe)
                TimeManager.OnTick += TimeManager_OnTick;
            else
                TimeManager.OnTick -= TimeManager_OnTick;
        }

        private void TimeManager_OnTick()
        {
            ApplyAgentState();
            if (IsAgentController)
                CaptureLeg();
        }

        /// <summary>
        /// Enables the agent on the controller and disables it on followers, only when the role changes.
        /// </summary>
        private void ApplyAgentState()
        {
            bool isController = IsAgentController;
            if (_hasAppliedAgentState && _lastAppliedController == isController)
                return;

            bool isRoleChange = _hasAppliedAgentState;
            _hasAppliedAgentState = true;
            _lastAppliedController = isController;

            if (isController)
            {
                //A new controller announces a fresh leg from where it stands.
                _hasCapturedLeg = false;
                _idleCaptureCount = 0;
                _catchUpOffset = Vector3.zero;
                _followerVelocity = Vector3.zero;
            }
            else if (isRoleChange)
            {
                //A former controller already stands where the object is.
                _hasSeatedFollower = true;
                _hasReceivedLeg = true;
            }

            if (_manageAgentEnabled && _navMeshAgent != null)
                _navMeshAgent.enabled = isController;
        }

        /// <summary>
        /// Reads the agent and sends a leg when it changes.
        /// </summary>
        private void CaptureLeg()
        {
            if (_navMeshAgent == null || !_navMeshAgent.enabled)
                return;

            //A path being recalculated is not a stop; the agent is still walking the leg it already announced.
            bool isPathResolving = _navMeshAgent.pathPending;
            bool isOffMesh = !_navMeshAgent.isOnNavMesh;
            bool isIdle = isOffMesh || _navMeshAgent.isStopped || (!_navMeshAgent.hasPath && !isPathResolving);

            /* Nor is the gap between two paths: code giving the agent its next destination on arrival
             * may run after this tick, leaving it briefly pathless. Only a lasting idle is a stop. */
            _idleCaptureCount = isIdle ? _idleCaptureCount + 1 : 0;
            bool isStopped = isOffMesh || _idleCaptureCount > IDLE_CAPTURES_BEFORE_STOP;

            Vector3 position = transform.position;
            if (isStopped)
            {
                WriteLeg(position, position, isStopped: true);
                return;
            }

            if (isIdle || isPathResolving || !_navMeshAgent.hasPath)
            {
                //Always send a first leg so followers are placed where the object stands.
                if (!_hasCapturedLeg)
                    WriteLeg(position, position, isStopped: false);
                return;
            }

            WriteLeg(position, _navMeshAgent.steeringTarget, isStopped: false);
        }

        /// <summary>
        /// Sends a leg if its point moved far enough from the last one, its stopped state changed, or the agent settings changed.
        /// </summary>
        private void WriteLeg(Vector3 previousPoint, Vector3 nextPoint, bool isStopped)
        {
            float speed = _navMeshAgent.speed;
            float angularSpeed = _navMeshAgent.angularSpeed;
            float acceleration = _navMeshAgent.acceleration;

            if (_hasCapturedLeg && _leg.IsStopped == isStopped && _leg.Speed == speed && _leg.AngularSpeed == angularSpeed && _leg.Acceleration == acceleration && Vector3.Distance(nextPoint, _leg.NextPoint) <= _pointChangeTolerance)
                return;

            _hasCapturedLeg = true;
            _leg = new()
            {
                PreviousPoint = previousPoint,
                NextPoint = nextPoint,
                IsStopped = isStopped,
                Speed = speed,
                AngularSpeed = angularSpeed,
                Acceleration = acceleration
            };

            if (IsServerInitialized)
                ObserversLeg(_leg);
            else
                ServerLeg(_leg);
        }

        /// <summary>
        /// Receives a leg from an owner driving the agent, follows it, and relays it to observers.
        /// </summary>
        [ServerRpc]
        private void ServerLeg(NavMeshAgentLeg leg)
        {
            if (!_clientAuthoritative)
                return;

            ApplyLeg(leg);
            ObserversLeg(leg);
        }

        /// <summary>
        /// Receives a leg on clients. The last one is buffered for clients which join later.
        /// </summary>
        [ObserversRpc(BufferLast = true, ExcludeServer = true)]
        private void ObserversLeg(NavMeshAgentLeg leg)
        {
            //The controlling owner sent this leg itself.
            if (IsAgentController)
                return;

            ApplyLeg(leg);
        }

        /// <summary>
        /// Places a joining client where the agent is on the server, moving as it moves.
        /// </summary>
        [TargetRpc]
        private void TargetSeatFollower(NetworkConnection connection, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (IsServerStarted || IsAgentController)
                return;

            transform.SetPositionAndRotation(position, rotation);
            _followerVelocity = velocity;
            _catchUpOffset = Vector3.zero;
            _hasSeatedFollower = true;
        }

        /// <summary>
        /// Takes a received leg, placing or correcting the follower as needed.
        /// </summary>
        private void ApplyLeg(NavMeshAgentLeg next)
        {
            NavMeshAgentLeg prev = _leg;
            _leg = next;

            /* The first leg arrives with the spawn and places the object outright, as it has no position
             * of its own to keep. When the server's seat arrived first it is more accurate, so it is kept. */
            if (!_hasReceivedLeg)
            {
                _hasReceivedLeg = true;
                if (!_hasSeatedFollower)
                {
                    SeatOnLegOrigin(next);
                    _hasSeatedFollower = true;
                }
                return;
            }

            if (next.NextPoint == prev.NextPoint)
                return;

            Vector3 position = transform.position;
            float error = Vector3.Distance(position, next.PreviousPoint);
            if (error <= _resyncDistance)
                return;

            //Too far to walk off in a reasonable time, such as after a warp.
            if (error >= _teleportDistance)
            {
                SeatOnLegOrigin(next);
                return;
            }

            /* Owed as an offset rather than a distance, so a follower ahead of its leg is held back
             * instead of being pushed further ahead. */
            _catchUpOffset = next.PreviousPoint - position;
        }

        /// <summary>
        /// Places the transform on a leg's origin with no walk toward it.
        /// </summary>
        private void SeatOnLegOrigin(NavMeshAgentLeg leg)
        {
            transform.position = leg.PreviousPoint;
            _catchUpOffset = Vector3.zero;
        }

        /// <summary>
        /// Walks the transform along the received leg on a follower.
        /// </summary>
        /// <param name = "deltaTime">Seconds since the last advance.</param>
        private void AdvanceFollower(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            NavMeshAgentLeg leg = _leg;
            Vector3 position = transform.position;
            Vector3 toPoint = leg.NextPoint - position;

            if (leg.IsStopped)
            {
                _followerVelocity = Vector3.MoveTowards(_followerVelocity, Vector3.zero, leg.Acceleration * deltaTime);
                //A halt still settles what is owed, so a follower which stopped short of the controller is not left there.
                transform.position = position + ConsumeCatchUp(deltaTime, leg.Speed);
                return;
            }

            /* Steered rather than snapped to speed, as the controller's agent is limited by its acceleration
             * and loses ground at every turn. A follower pivoting for free would drift ahead at each corner. */
            Vector3 desiredVelocity = toPoint.sqrMagnitude <= Mathf.Epsilon ? Vector3.zero : toPoint.normalized * leg.Speed;
            _followerVelocity = Vector3.MoveTowards(_followerVelocity, desiredVelocity, leg.Acceleration * deltaTime);

            //A follower which reaches its point early rests there until the next leg arrives.
            float step = _followerVelocity.magnitude * deltaTime;
            transform.position = Vector3.MoveTowards(position, leg.NextPoint, step) + ConsumeCatchUp(deltaTime, leg.Speed);

            if (!_synchronizeRotation)
                return;

            Vector3 facing = new(toPoint.x, 0f, toPoint.z);
            if (facing.sqrMagnitude <= MINIMUM_FACING_SQUARED_DISTANCE)
                return;

            Quaternion facingRotation = Quaternion.LookRotation(facing, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facingRotation, leg.AngularSpeed * deltaTime);
        }

        /// <summary>
        /// Spends part of the ground a follower owes and returns it, capped so a correction never exceeds the walking speed.
        /// </summary>
        private Vector3 ConsumeCatchUp(float deltaTime, float speed)
        {
            if (_catchUpOffset == Vector3.zero)
                return Vector3.zero;

            Vector3 catchUpStep = Vector3.ClampMagnitude(_catchUpOffset * (deltaTime / CATCH_UP_SECONDS), speed * MAXIMUM_CATCH_UP_FRACTION * deltaTime);
            _catchUpOffset -= catchUpStep;
            if (_catchUpOffset.sqrMagnitude <= SETTLED_CATCH_UP_DISTANCE * SETTLED_CATCH_UP_DISTANCE)
                _catchUpOffset = Vector3.zero;

            return catchUpStep;
        }

        /// <summary>
        /// Returns if this peer may drive the agent.
        /// </summary>
        private bool CanDriveAgent() => IsAgentController && _navMeshAgent != null && _navMeshAgent.enabled;
    }
}
#endif
