#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Transforming
{
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Component/NetworkTransform")]
    public sealed class NetworkTransform : NetworkBehaviour
    {
        #region Types.
        [System.Serializable]
        public enum ComponentConfigurationType
        {
            Disabled = 0,
            CharacterController = 1,
            Rigidbody = 2,
            Rigidbody2D = 3,
        }
        private struct ReceivedData
        {
            public List<bool> HasData;
            public PooledWriter Writer;
            public Channel Channel;

            /// <summary>
            /// Sets has data value for all LODs.
            /// </summary>
            public void SetHasData(bool value)
            {
                for (int i = 0; i < HasData.Count; i++)
                    HasData[i] = value;
            }
            /// <summary>
            /// Sets the data is available for a single LOD.
            /// </summary>
            /// <param name="index"></param>
            public void SetHasData(bool value, byte index)
            {
                if (index >= HasData.Count)
                    return;

                HasData[index] = value;
            }
        }

        [System.Serializable]
        public struct SnappedAxes
        {
            public bool X;
            public bool Y;
            public bool Z;
        }
        private enum ChangedDelta
        {
            Unset = 0,
            PositionX = 1,
            PositionY = 2,
            PositionZ = 4,
            Rotation = 8,
            Extended = 16,
            ScaleX = 32,
            ScaleY = 64,
            ScaleZ = 128,
            Nested = 256
        }
        private enum ChangedFull
        {
            Unset = 0,
            Position = 1,
            Rotation = 2,
            Scale = 4,
            Nested = 8
        }

        private enum UpdateFlagA : byte
        {
            Unset = 0,
            X2 = 1,
            X4 = 2,
            Y2 = 4,
            Y4 = 8,
            Z2 = 16,
            Z4 = 32,
            Rotation = 64,
            Extended = 128
        }
        private enum UpdateFlagB : byte
        {
            Unset = 0,
            X2 = 1,
            X4 = 2,
            Y2 = 4,
            Y4 = 8,
            Z2 = 16,
            Z4 = 32,
            Nested = 64
        }
        public class GoalData
        {
            public uint ReceivedTick;
            public RateData Rates = new RateData();
            public TransformData Transforms = new TransformData();

            public GoalData() { }
            public void Reset()
            {
                ReceivedTick = 0;
                Transforms.Reset();
                Rates.Reset();
            }
        }
        public class RateData
        {
            /// <summary>
            /// Rate for position after smart calculations.
            /// </summary>
            public float Position;
            /// <summary>
            /// Rate for rotation after smart calculations.
            /// </summary>
            public float Rotation;
            /// <summary>
            /// Rate for scale after smart calculations.
            /// </summary>
            public float Scale;
            /// <summary>
            /// Unaltered rate for position calculated through position change and tickspan.
            /// </summary>
            public float LastUnalteredPositionRate;
            /// <summary>
            /// Number of ticks the rates are calculated for.
            /// If TickSpan is 2 then the rates are calculated under the assumption the transform changed over 2 ticks.
            /// </summary>
            public uint TickSpan;
            /// <summary>
            /// True if the rate is believed to be fluctuating unusually.
            /// </summary>
            internal bool AbnormalRateDetected;
            /// <summary>
            /// Time remaining until transform is expected to reach it's goal.
            /// </summary>
            internal float TimeRemaining;

            public RateData() { }

            public void Reset()
            {
                Position = 0f;
                Rotation = 0f;
                Scale = 0f;
                LastUnalteredPositionRate = 0f;
                TickSpan = 0;
                AbnormalRateDetected = false;
                TimeRemaining = 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Update(RateData rd)
            {
                Update(rd.Position, rd.Rotation, rd.Scale, rd.LastUnalteredPositionRate, rd.TickSpan, rd.AbnormalRateDetected, rd.TimeRemaining);
            }

            /// <summary>
            /// Updates rates.
            /// </summary>
            public void Update(float position, float rotation, float scale, float unalteredPositionRate, uint tickSpan, bool abnormalRateDetected, float timeRemaining)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
                LastUnalteredPositionRate = unalteredPositionRate;
                TickSpan = tickSpan;
                AbnormalRateDetected = abnormalRateDetected;
                TimeRemaining = timeRemaining;
            }
        }

        public class TransformData
        {
            public enum ExtrapolateState : byte
            {
                Disabled = 0,
                Available = 1,
                Active = 2
            }
            public uint Tick;
            public bool Snapped;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Vector3 ExtrapolatedPosition;
            public ExtrapolateState ExtrapolationState;
            public NetworkBehaviour ParentBehaviour;
            public TransformData() { }

            internal void Reset()
            {
                Tick = 0;
                Snapped = false;
                Position = Vector3.zero;
                Rotation = Quaternion.identity;
                Scale = Vector3.zero;
                ExtrapolatedPosition = Vector3.zero;
                ExtrapolationState = ExtrapolateState.Disabled;
                ParentBehaviour = null;
            }
            internal void Update(TransformData copy)
            {
                Update(copy.Tick, copy.Position, copy.Rotation, copy.Scale, copy.ExtrapolatedPosition, copy.ParentBehaviour);
            }
            internal void Update(uint tick, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 extrapolatedPosition, NetworkBehaviour parentBehaviour)
            {
                Tick = tick;
                Position = position;
                Rotation = rotation;
                Scale = scale;
                ExtrapolatedPosition = extrapolatedPosition;
                ParentBehaviour = parentBehaviour;
            }
        }

        #endregion

        #region Public.
        /// <summary>
        /// 
        /// </summary>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        [APIExclude]
        public delegate void DataReceivedChanged(TransformData prev, TransformData next);
        /// <summary>
        /// Called when new data is received. Previous and next data are provided. Next data may be manipulated.
        /// </summary>
        public event DataReceivedChanged OnDataReceived;
        /// <summary>
        /// Called when GoalData is updated.
        /// </summary>
        public event Action<GoalData> OnNextGoal;
        /// <summary>
        /// Called when the transform has reached it's goal.
        /// </summary>
        public event Action OnInterpolationComplete;
        /// <summary>
        /// True if the local client used TakeOwnership and is awaiting an ownership change.
        /// </summary>
        public bool TakenOwnership { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// Attached movement component to automatically configure.
        /// </summary>
        [Tooltip("Attached movement component to automatically configure.")]
        [SerializeField]
        private ComponentConfigurationType _componentConfiguration = ComponentConfigurationType.Disabled;
        /// <summary>
        /// True to synchronize when this transform changes parent.
        /// </summary>
        [Tooltip("True to synchronize when this transform changes parent.")]
        [SerializeField]
        private bool _synchronizeParent;
        /// <summary>
        /// How much to compress each transform property.
        /// </summary>
        [Tooltip("How much to compress each transform property.")]
        [SerializeField]
        private TransformPackingData _packing = new TransformPackingData()
        {
            Position = AutoPackType.Packed,
            Rotation = AutoPackType.Packed,
            Scale = AutoPackType.Unpacked
        };
        /// <summary>
        /// How many ticks to interpolate.
        /// </summary>
        [Tooltip("How many ticks to interpolate.")]
        [Range(1, MAX_INTERPOLATION)]
        [SerializeField]
        private ushort _interpolation = 2;
        /// <summary>
        /// How many ticks to extrapolate.
        /// </summary>
        [Tooltip("How many ticks to extrapolate.")]
        [Range(0, 1024)]
        [SerializeField]
#pragma warning disable CS0414 //Not in use.
        private ushort _extrapolation = 2;
#pragma warning restore CS0414 //Not in use.
        /// <summary>
        /// True to enable teleport threshhold.
        /// </summary>
        [Tooltip("True to enable teleport threshhold.")]
        [SerializeField]
        private bool _enableTeleport;
        /// <summary>
        /// How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.
        /// </summary>
        [Tooltip("How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.")]
        [Range(0f, float.MaxValue)]
        [SerializeField]
        private float _teleportThreshold = 1f;
        /// <summary>
        /// True if owner controls how the object is synchronized.
        /// </summary>
        [Tooltip("True if owner controls how the object is synchronized.")]
        [SerializeField]
        private bool _clientAuthoritative = true;
        /// <summary>
        /// True to synchronize movements on server to owner when not using client authoritative movement.
        /// </summary>
        [Tooltip("True to synchronize movements on server to owner when not using client authoritative movement.")]
        [SerializeField]
        private bool _sendToOwner = true;
        /// <summary>
        /// Gets SendToOwner.
        /// </summary>
        public bool GetSendToOwner() => _sendToOwner;
        /// <summary>
        /// Sets SendToOwner. Only the server may call this method.
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetSendToOwner(bool value)
        {
            _sendToOwner = value;
            if (base.IsServer)
                ObserversSetSendToOwner(value);
        }
        /// <summary>
        /// How often in ticks to synchronize. This is default to 1 but will change depending on LOD to ensure proper smoothing calculations
        /// </summary>
        private byte _interval = 1;
        /// <summary>
        /// True to synchronize position. Even while checked only changed values are sent.
        /// </summary>
        [Tooltip("True to synchronize position. Even while checked only changed values are sent.")]
        [SerializeField]
        private bool _synchronizePosition = true;
        /// <summary>
        /// Sets if to synchronize position.
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetSynchronizePosition(bool value) => _synchronizePosition = value;
        /// <summary>
        /// Axes to snap on position.
        /// </summary>
        [Tooltip("Axes to snap on position.")]
        [SerializeField]
        private SnappedAxes _positionSnapping = new SnappedAxes();
        /// <summary>
        /// Sets which Position axes to snap.
        /// </summary>
        /// <param name="axes">Axes to snap.</param>
        public void SetPositionSnapping(SnappedAxes axes) => _positionSnapping = axes;
        /// <summary>
        /// True to synchronize rotation. Even while checked only changed values are sent.
        /// </summary>
        [Tooltip("True to synchronize rotation. Even while checked only changed values are sent.")]
        [SerializeField]
        private bool _synchronizeRotation = true;
        /// <summary>
        /// Sets if to synchronize rotation.
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetSynchronizeRotation(bool value) => _synchronizeRotation = value;
        /// <summary>
        /// Axes to snap on rotation.
        /// </summary>
        [Tooltip("Axes to snap on rotation.")]
        [SerializeField]
        private SnappedAxes _rotationSnapping = new SnappedAxes();
        /// <summary>
        /// Sets which Scale axes to snap.
        /// </summary>
        /// <param name="axes">Axes to snap.</param>
        public void SetRotationSnapping(SnappedAxes axes) => _rotationSnapping = axes;
        /// <summary>
        /// True to synchronize scale. Even while checked only changed values are sent.
        /// </summary>
        [Tooltip("True to synchronize scale. Even while checked only changed values are sent.")]
        [SerializeField]
        private bool _synchronizeScale = true;
        /// <summary>
        /// Sets if to synchronize scale.
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetSynchronizeScale(bool value) => _synchronizeScale = value;
        /// <summary>
        /// Axes to snap on scale.
        /// </summary>
        [Tooltip("Axes to snap on scale.")]
        [SerializeField]
        private SnappedAxes _scaleSnapping = new SnappedAxes();
        /// <summary>
        /// Sets which Scale axes to snap.
        /// </summary>
        /// <param name="axes">Axes to snap.</param>
        public void SetScaleSnapping(SnappedAxes axes) => _scaleSnapping = axes;
        #endregion

        #region Private.
        /// <summary>
        /// Packing data with all values set to uncompressed.
        /// </summary>
        private TransformPackingData _unpacked = new TransformPackingData()
        {
            Position = AutoPackType.Unpacked,
            Rotation = AutoPackType.Unpacked,
            Scale = AutoPackType.Unpacked
        };
        /// <summary>
        /// True if the last DataReceived was on the reliable channel. Default to true so initial values do not extrapolate.
        /// </summary>
        private bool _lastReceiveReliable = true;
        /// <summary>
        /// NetworkBehaviour this transform is a child of.
        /// </summary>
        private NetworkBehaviour _parentBehaviour;
        /// <summary>
        /// Last transform which this object was a child of.
        /// </summary>
        private Transform _parentTransform;
        /// <summary>
        /// Values changed over time that server has sent to clients since last reliable has been sent.
        /// </summary>
        private List<ChangedDelta> _serverChangedSinceReliable = new List<ChangedDelta>();
        /// <summary>
        /// Values changed over time that client has sent to server since last reliable has been sent.
        /// </summary>
        private ChangedDelta _clientChangedSinceReliable = ChangedDelta.Unset;
        /// <summary>
        /// Last tick an ObserverRpc passed checks.
        /// </summary>
        private uint _lastObserversRpcTick;
        /// <summary>
        /// Last tick a ServerRpc passed checks.
        /// </summary>
        private uint _lastServerRpcTick;
        /// <summary>
        /// Last received data from an authoritative client.
        /// </summary>
        private ReceivedData _receivedClientData = new ReceivedData();
        /// <summary>
        /// True if subscribed to TimeManager for ticks.
        /// </summary>
        private bool _subscribedToTicks;
        /// <summary>
        /// Last TransformData to be received.
        /// </summary>
        private TransformData _lastReceivedTransformData = new TransformData();
        /// <summary>
        /// Last RateData to be calculated from LastReceivedTransformData.
        /// </summary>
        private RateData _lastCalculatedRateData = new RateData();
        /// <summary>
        /// GoalDatas to move towards.
        /// </summary>
        private Queue<GoalData> _goalDataQueue = new Queue<GoalData>();
        /// <summary>
        /// Current GoalData being used.
        /// </summary>
        private GoalData _currentGoalData = new GoalData();
        /// <summary>
        /// True if queue can be read. While true objects will move to CurrentGoalData.
        /// </summary>
        private bool _queueReady = false;
        /// <summary>
        /// Cache of GoalDatas to prevent allocations.
        /// </summary>
        private static Stack<GoalData> _goalDataCache = new Stack<GoalData>();
        /// <summary>
        /// True if the transform has changed since it started.
        /// </summary>
        private bool _changedSinceStart;
        /// <summary>
        /// Last sent transform data for every LOD.
        /// </summary>
        private List<TransformData> _lastSentTransformDatas = new List<TransformData>();
        /// <summary>
        /// Writers for changed data for each level of detail.
        /// </summary>
        private List<PooledWriter> _changedWriters = new List<PooledWriter>();
        #endregion

        #region Const.
        /// <summary>
        /// Maximum possible interpolation value.
        /// </summary>
        public const ushort MAX_INTERPOLATION = 250;
        #endregion

        private void Awake()
        {
            _interval = Math.Max(_interval, (byte)1);
        }

        private void OnDestroy()
        {
            if (_receivedClientData.Writer != null)
            {
                _receivedClientData.Writer.Dispose();
                _receivedClientData.Writer = null;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            ConfigureComponents();
            _receivedClientData.HasData = new List<bool>();

            //Initialize for LODs.
            for (int i = 0; i < base.ObserverManager.GetLevelOfDetailDistances().Count; i++)
            {
                _changedWriters.Add(WriterPool.GetWriter());
                _lastSentTransformDatas.Add(new TransformData());
                _receivedClientData.HasData.Add(false);
                _serverChangedSinceReliable.Add(ChangedDelta.Unset);
            }

            SetDefaultGoalData();
            /* Server must always subscribe.
             * Server needs to relay client auth in
             * ticks or send non-auth/non-owner to
             * clients in tick. */
            ChangeTickSubscription(true);
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            /* If not on the root then the initial properties may need to be synchronized
             * since the spawn message only sends root information. If initial
             * properties have changed update spawning connection. */
            if (base.NetworkObject.gameObject != gameObject && _changedSinceStart)
            {
                //Send latest.
                using (PooledWriter writer = WriterPool.GetWriter())
                {
                    ChangedDelta fullTransform = (ChangedDelta.PositionX | ChangedDelta.PositionY | ChangedDelta.PositionZ | ChangedDelta.Extended | ChangedDelta.ScaleX | ChangedDelta.ScaleY | ChangedDelta.ScaleZ | ChangedDelta.Rotation);
                    SerializeChanged(fullTransform, writer);
                    TargetUpdateTransform(connection, writer.GetArraySegment(), Channel.Reliable);
                }
            }

            
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ConfigureComponents();

            //Initialize for LOD if client only.
            if (base.IsClientOnly)
            {
                for (int i = 0; i < base.ObserverManager.GetLevelOfDetailDistances().Count; i++)
                {
                    _changedWriters.Add(WriterPool.GetWriter());
                    _lastSentTransformDatas.Add(new TransformData());
                }
            }

            SetDefaultGoalData();
        }

        public override void OnOwnershipServer(NetworkConnection prevOwner)
        {
            base.OnOwnershipServer(prevOwner);
            //Reset last tick since each client sends their own ticks.
            _lastServerRpcTick = 0;
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            /* If newOwner is self then client
             * must subscribe to ticks. Client can also
             * unsubscribe from ticks if not owner,
             * long as the server is also not active. */
            if (base.IsOwner)
            {
                ChangeTickSubscription(true);
            }
            //Not new owner.
            else
            {
                /* If client authoritative and ownership was lost
                 * then default goals must be set to force the
                 * object to it's last transform. */
                if (_clientAuthoritative)
                    SetDefaultGoalData();

                if (!base.IsServer)
                    ChangeTickSubscription(false);
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            //Always unsubscribe; if the server stopped so did client.
            ChangeTickSubscription(false);
            for (int i = 0; i < base.ObserverManager.GetLevelOfDetailDistances().Count; i++)
            {
                _lastSentTransformDatas[i].Reset();
                _serverChangedSinceReliable[i] = ChangedDelta.Unset;
                _receivedClientData.SetHasData(false, (byte)i);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            //If not also server unsubscribe from ticks.
            if (!base.IsServer)
                ChangeTickSubscription(false);
        }

        private void Update()
        {
            MoveToTarget();
        }

        /// <summary>
        /// Configures components automatically.
        /// </summary>
        private void ConfigureComponents()
        {
            //Disabled.
            if (_componentConfiguration == ComponentConfigurationType.Disabled)
            {
                return;
            }
            //RB.
            else if (_componentConfiguration == ComponentConfigurationType.Rigidbody)
            {

                if (TryGetComponent<Rigidbody>(out Rigidbody c))
                {
                    bool isKinematic = (!base.IsOwner || base.IsServerOnly);
                    c.isKinematic = isKinematic;
                    c.interpolation = RigidbodyInterpolation.None;
                }
            }
            //RB2D
            else if (_componentConfiguration == ComponentConfigurationType.Rigidbody2D)
            {
                //Only client authoritative needs to be configured.
                if (!_clientAuthoritative)
                    return;
                if (TryGetComponent<Rigidbody2D>(out Rigidbody2D c))
                {
                    bool isKinematic = (!base.IsOwner || base.IsServerOnly);
                    c.isKinematic = isKinematic;
                    c.simulated = !isKinematic;
                    c.interpolation = RigidbodyInterpolation2D.None;
                }
            }
            //CC
            else if (_componentConfiguration == ComponentConfigurationType.CharacterController)
            {
                if (TryGetComponent<CharacterController>(out CharacterController c))
                {
                    //Client auth.
                    if (_clientAuthoritative)
                    {
                        c.enabled = base.IsOwner;
                    }
                    //Server auth.
                    else
                    {
                        //Not CSP.
                        if (_sendToOwner)
                            c.enabled = base.IsServer;
                        //Most likely CSP.
                        else
                            c.enabled = (base.IsServer || base.IsOwner);

                    }
                }
            }
        }

        /// <summary>
        /// Called when a tick occurs.
        /// </summary>
        private void TimeManager_OnPostTick()
        {
            
            if (base.IsServer)
            {
                byte lodIndex = base.ObserverManager.LevelOfDetailIndex;
                SendToClients(lodIndex);
            }

            if (base.IsClient)
                SendToServer(_lastSentTransformDatas[0]);
        }

        /// <summary>
        /// Tries to subscribe to TimeManager ticks.
        /// </summary>
        private void ChangeTickSubscription(bool subscribe)
        {
            if (subscribe == _subscribedToTicks)
                return;

            _subscribedToTicks = subscribe;
            if (subscribe)
                base.NetworkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
            else
                base.NetworkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        /// <summary>
        /// Sets SendToOwner value.
        /// </summary>
        /// <param name="value"></param>
        [ObserversRpc(BufferLast = true, ExcludeServer = true)]
        private void ObserversSetSendToOwner(bool value)
        {
            _sendToOwner = value;
        }

        /// <summary>
        /// Resets last sent information to force a resend of current values.
        /// </summary>
        public void ForceSend()
        {
            for (int i = 0; i < _lastSentTransformDatas.Count; i++)
                _lastSentTransformDatas[i].Reset();
            if (_receivedClientData.Writer != null)
                _receivedClientData.SetHasData(true);
        }

        /// <summary>
        /// Updates the interval value over the network.
        /// </summary>
        /// <param name="value">New interval.</param>
        public void SetInterval(byte value)
        {
            bool canSet = (base.IsServer && !_clientAuthoritative)
                || (base.IsServer && _clientAuthoritative && !base.Owner.IsValid)
                || (_clientAuthoritative && base.IsOwner);

            if (!canSet)
                return;

            if (base.IsServer)
                ObserversSetInterval(value);
            else
                ServerSetInterval(value);
        }

        /// <summary>
        /// Updates the interval value.
        /// </summary>
        /// <param name="value"></param>
        private void SetIntervalInternal(byte value)
        {
            value = (byte)Mathf.Max(value, 1);
            _interval = value;
        }

        /// <summary>
        /// Sets interval over the network.
        /// </summary>
        [ServerRpc(RunLocally = true)]
        private void ServerSetInterval(byte value)
        {
            if (!_clientAuthoritative)
            {

                base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
                return;
            }

            SetIntervalInternal(value);
        }
        /// <summary>
        /// Sets interval over the network.
        /// </summary>
        [ObserversRpc(RunLocally = true)]
        private void ObserversSetInterval(byte value)
        {
            SetIntervalInternal(value);
        }


        /// <summary>
        /// Creates goal data using current position.
        /// </summary>
        private void SetDefaultGoalData()
        {
            Transform t = transform;
            NetworkBehaviour parentBehaviour = null;
            //If there is a parent try to output the behaviour on it.
            if (_synchronizeParent && transform.parent != null)
            {
                transform.parent.TryGetComponent<NetworkBehaviour>(out parentBehaviour);
                if (parentBehaviour == null)
                {
                    LogInvalidParent();
                }
                else
                {
                    _parentTransform = transform.parent;
                    _parentBehaviour = parentBehaviour;
                }
            }

            _lastReceivedTransformData.Update(0, t.localPosition, t.localRotation, t.localScale, t.localPosition, parentBehaviour);
            SetInstantRates(_currentGoalData.Rates);
        }

        /// <summary>
        /// Prints an invalid parent debug.
        /// </summary>
        private void LogInvalidParent()
        {
            Debug.LogWarning($"{gameObject.name} [Id {base.ObjectId}] is nested but the parent {transform.parent.name} does not contain a NetworkBehaviour component. To synchronize parents the parent object must have a NetworkBehaviour component, even if empty.");
        }

        /// <summary>
        /// Serializes only changed data into writer.
        /// </summary>
        /// <param name="changed"></param>
        /// <param name="writer"></param>
        private void SerializeChanged(ChangedDelta changed, PooledWriter writer)
        {
            UpdateFlagA flagsA = UpdateFlagA.Unset;
            UpdateFlagB flagsB = UpdateFlagB.Unset;
            /* Do not use compression when nested. Depending
             * on the scale of the parent compression may
             * not be accurate enough. */
            TransformPackingData packing = (ChangedContains(changed, ChangedDelta.Nested)) ?
                _unpacked : _packing;

            int startIndexA = writer.Position;
            writer.Reserve(1);
            //Original axis value.
            float original;
            //Compressed axis value.
            float compressed;
            //Multiplier for compression.
            float multiplier = 100f;
            /* Maximum value compressed may be 
             * to send as compressed. */
            float maxValue = (short.MaxValue - 1);

            Transform t = transform;
            /* Position. */
            if (_synchronizePosition)
            {
                AutoPackType localPacking = packing.Position;
                //PositionX
                if (ChangedContains(changed, ChangedDelta.PositionX))
                {
                    original = t.localPosition.x;
                    compressed = original * multiplier;
                    if (localPacking != AutoPackType.Unpacked && Math.Abs(compressed) <= maxValue)
                    {
                        flagsA |= UpdateFlagA.X2;
                        writer.WriteInt16((short)compressed);
                    }
                    else
                    {
                        flagsA |= UpdateFlagA.X4;
                        writer.WriteSingle(original);
                    }
                }
                //PositionY
                if (ChangedContains(changed, ChangedDelta.PositionY))
                {
                    original = t.localPosition.y;
                    compressed = original * multiplier;
                    if (localPacking != AutoPackType.Unpacked && Math.Abs(compressed) <= maxValue)
                    {
                        flagsA |= UpdateFlagA.Y2;
                        writer.WriteInt16((short)compressed);
                    }
                    else
                    {
                        flagsA |= UpdateFlagA.Y4;
                        writer.WriteSingle(original);
                    }
                }
                //PositionZ
                if (ChangedContains(changed, ChangedDelta.PositionZ))
                {
                    original = t.localPosition.z;
                    compressed = original * multiplier;
                    if (localPacking != AutoPackType.Unpacked && Math.Abs(compressed) <= maxValue)
                    {
                        flagsA |= UpdateFlagA.Z2;
                        writer.WriteInt16((short)compressed);
                    }
                    else
                    {
                        flagsA |= UpdateFlagA.Z4;
                        writer.WriteSingle(original);
                    }
                }
            }

            /* Rotation. */
            if (_synchronizeRotation)
            {
                if (ChangedContains(changed, ChangedDelta.Rotation))
                {
                    flagsA |= UpdateFlagA.Rotation;
                    /* Rotation can always use pack settings even
                     * if nested. Unsual transform scale shouldn't affect rotation. */
                    writer.WriteQuaternion(t.localRotation, _packing.Rotation);
                }
            }

            if (ChangedContains(changed, ChangedDelta.Extended))
            {
                AutoPackType localPacking = packing.Scale;
                flagsA |= UpdateFlagA.Extended;
                int startIndexB = writer.Position;
                writer.Reserve(1);

                /* Scale. */
                if (_synchronizeScale)
                {
                    //ScaleX
                    if (ChangedContains(changed, ChangedDelta.ScaleX))
                    {
                        original = t.localScale.x;
                        compressed = original * multiplier;
                        if (localPacking != AutoPackType.Unpacked && Math.Abs(compressed) <= maxValue)
                        {
                            flagsB |= UpdateFlagB.X2;
                            writer.WriteInt16((short)compressed);
                        }
                        else
                        {
                            flagsB |= UpdateFlagB.X4;
                            writer.WriteSingle(original);
                        }
                    }
                    //ScaleY
                    if (ChangedContains(changed, ChangedDelta.ScaleY))
                    {
                        original = t.localScale.y;
                        compressed = original * multiplier;
                        if (localPacking != AutoPackType.Unpacked && Math.Abs(compressed) <= maxValue)
                        {
                            flagsB |= UpdateFlagB.Y2;
                            writer.WriteInt16((short)compressed);
                        }
                        else
                        {
                            flagsB |= UpdateFlagB.Y4;
                            writer.WriteSingle(original);
                        }
                    }
                    //ScaleZ
                    if (ChangedContains(changed, ChangedDelta.ScaleZ))
                    {
                        original = t.localScale.z;
                        compressed = original * multiplier;
                        if (localPacking != AutoPackType.Unpacked && Math.Abs(compressed) <= maxValue)
                        {
                            flagsB |= UpdateFlagB.Z2;
                            writer.WriteInt16((short)compressed);
                        }
                        else
                        {
                            flagsB |= UpdateFlagB.Z4;
                            writer.WriteSingle(original);
                        }
                    }
                }

                //Nested.
                if (ChangedContains(changed, ChangedDelta.Nested) && _parentBehaviour != null)
                {
                    flagsB |= UpdateFlagB.Nested;
                    writer.WriteNetworkBehaviour(_parentBehaviour);
                }

                writer.FastInsertByte((byte)flagsB, startIndexB);
            }

            //Insert flags.
            writer.FastInsertByte((byte)flagsA, startIndexA);
            bool ChangedContains(ChangedDelta whole, ChangedDelta part)
            {
                return (whole & part) == part;
            }
        }

        /// <summary>
        /// Deerializes a received packet.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeserializePacket(ArraySegment<byte> data, TransformData prevTransformData, TransformData nextTransformData, ref ChangedFull changedFull)
        {
            using (PooledReader r = ReaderPool.GetReader(data, base.NetworkManager))
            {
                UpdateFlagA flagsA = (UpdateFlagA)r.ReadByte();

                int readerRemaining;
                readerRemaining = r.Remaining;
                //X
                if (UpdateFlagAContains(flagsA, UpdateFlagA.X2))
                    nextTransformData.Position.x = r.ReadInt16() / 100f;
                else if (UpdateFlagAContains(flagsA, UpdateFlagA.X4))
                    nextTransformData.Position.x = r.ReadSingle();
                else
                    nextTransformData.Position.x = prevTransformData.Position.x;
                //Y
                if (UpdateFlagAContains(flagsA, UpdateFlagA.Y2))
                    nextTransformData.Position.y = r.ReadInt16() / 100f;
                else if (UpdateFlagAContains(flagsA, UpdateFlagA.Y4))
                    nextTransformData.Position.y = r.ReadSingle();
                else
                    nextTransformData.Position.y = prevTransformData.Position.y;
                //Z
                if (UpdateFlagAContains(flagsA, UpdateFlagA.Z2))
                    nextTransformData.Position.z = r.ReadInt16() / 100f;
                else if (UpdateFlagAContains(flagsA, UpdateFlagA.Z4))
                    nextTransformData.Position.z = r.ReadSingle();
                else
                    nextTransformData.Position.z = prevTransformData.Position.z;
                //If remaining has changed then a position was read.
                if (readerRemaining != r.Remaining)
                    changedFull |= ChangedFull.Position;

                //Rotation.
                if (UpdateFlagAContains(flagsA, UpdateFlagA.Rotation))
                {
                    //Always use _packing value even if nested.
                    nextTransformData.Rotation = r.ReadQuaternion(_packing.Rotation);
                    changedFull |= ChangedFull.Rotation;
                }
                else
                {
                    nextTransformData.Rotation = prevTransformData.Rotation;
                }

                //Extended settings.
                if (UpdateFlagAContains(flagsA, UpdateFlagA.Extended))
                {
                    UpdateFlagB flagsB = (UpdateFlagB)r.ReadByte();
                    readerRemaining = r.Remaining;

                    //X
                    if (UpdateFlagBContains(flagsB, UpdateFlagB.X2))
                        nextTransformData.Scale.x = r.ReadInt16() / 100f;
                    else if (UpdateFlagBContains(flagsB, UpdateFlagB.X4))
                        nextTransformData.Scale.x = r.ReadSingle();
                    else
                        nextTransformData.Scale.x = prevTransformData.Scale.x;
                    //Y
                    if (UpdateFlagBContains(flagsB, UpdateFlagB.Y2))
                        nextTransformData.Scale.y = r.ReadInt16() / 100f;
                    else if (UpdateFlagBContains(flagsB, UpdateFlagB.Y4))
                        nextTransformData.Scale.y = r.ReadSingle();
                    else
                        nextTransformData.Scale.y = prevTransformData.Scale.y;
                    //X
                    if (UpdateFlagBContains(flagsB, UpdateFlagB.Z2))
                        nextTransformData.Scale.z = r.ReadInt16() / 100f;
                    else if (UpdateFlagBContains(flagsB, UpdateFlagB.Z4))
                        nextTransformData.Scale.z = r.ReadSingle();
                    else
                        nextTransformData.Scale.z = prevTransformData.Scale.z;

                    if (r.Remaining != readerRemaining)
                        changedFull |= ChangedFull.Scale;
                    else
                        nextTransformData.Scale = prevTransformData.Scale;

                    if (UpdateFlagBContains(flagsB, UpdateFlagB.Nested))
                    {
                        nextTransformData.ParentBehaviour = r.ReadNetworkBehaviour();
                        changedFull |= ChangedFull.Nested;
                    }
                    else
                    {
                        Unnest();
                    }
                }
                //No extended settings.
                else
                {
                    nextTransformData.Scale = prevTransformData.Scale;
                    Unnest();
                }
            }

            void Unnest()
            {
                nextTransformData.ParentBehaviour = null;
            }

            //Returns if whole contains part.
            bool UpdateFlagAContains(UpdateFlagA whole, UpdateFlagA part)
            {
                return (whole & part) == part;
            }
            //Returns if whole contains part.
            bool UpdateFlagBContains(UpdateFlagB whole, UpdateFlagB part)
            {
                return (whole & part) == part;
            }
        }

        

        /// <summary>
        /// Moves to a GoalData. Automatically determins if to use data from server or client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveToTarget(float deltaOverride = -1f)
        {
            if (!_queueReady)
                return;
            //Cannot move if neither is active.
            if (!base.IsServer && !base.IsClient)
                return;
            //If client auth and the owner don't move towards target.
            if (_clientAuthoritative)
            {
                if (base.IsOwner || TakenOwnership)
                    return;
            }
            else
            {
                //If not client authoritative, is owner, and don't sync to owner.
                if (base.IsOwner && !_sendToOwner)
                    return;
            }

            //True if not client controlled.
            bool controlledByClient = (_clientAuthoritative && base.Owner.IsActive);
            //If not controlled by client and is server then no reason to move.
            if (!controlledByClient && base.IsServer)
                return;

            float delta = (deltaOverride != -1f) ? deltaOverride : Time.deltaTime;
            /* Once here it's safe to assume the object will be moving.
             * Any checks which would stop it from moving be it client
             * auth and owner, or server controlled and server, ect,
             * would have already been run. */
            TransformData td = _currentGoalData.Transforms;
            RateData rd = _currentGoalData.Rates;

            

            float multiplier = 1f;
            int queueCount = _goalDataQueue.Count;
            //For every entry past interpolation increase move rate.
            if (queueCount > (_interpolation + 1))
                multiplier += (0.05f * queueCount);

            //Rate to update. Changes per property.
            float rate;
            Transform t = transform;

            //Snap any positions that should be.
            SnapProperties(td);

            //Position.
            if (_synchronizePosition)
            {
                rate = rd.Position;
                Vector3 posGoal = (td.ExtrapolationState == TransformData.ExtrapolateState.Active && !_lastReceiveReliable) ? td.ExtrapolatedPosition : td.Position;
                if (rate == -1f)
                    t.localPosition = td.Position;
                else
                    t.localPosition = Vector3.MoveTowards(t.localPosition, posGoal, rate * delta * multiplier);
            }

            //Rotation.
            if (_synchronizeRotation)
            {
                rate = rd.Rotation;
                if (rate == -1f)
                    t.localRotation = td.Rotation;
                else
                    t.localRotation = Quaternion.RotateTowards(t.localRotation, td.Rotation, rate * delta);
            }

            //Scale.
            if (_synchronizeScale)
            {
                rate = rd.Scale;
                if (rate == -1f)
                    t.localScale = td.Scale;
                else
                    t.localScale = Vector3.MoveTowards(t.localScale, td.Scale, rate * delta);
            }

            float timeRemaining = rd.TimeRemaining - (delta * multiplier);
            if (timeRemaining < -delta)
                timeRemaining = -delta;
            rd.TimeRemaining = timeRemaining;

            if (rd.TimeRemaining <= 0f)
            {
                float leftOver = Mathf.Abs(rd.TimeRemaining);
                //If more in buffer then run next buffer.
                if (queueCount > 0)
                {
                    _currentGoalData.Reset();
                    _goalDataCache.Push(_currentGoalData);
                    SetCurrentGoalData(_goalDataQueue.Dequeue());
                    if (leftOver > 0f)
                        MoveToTarget(leftOver);
                }
                //No more in buffer, see if can extrapolate.
                else
                {
                    
                        /* If everything matches up then end queue.
                        * Otherwise let it play out until stuff
                        * aligns. Generally the time remaining is enough
                        * but every once in awhile something goes funky
                        * and it's thrown off. */
                        if (!HasChanged(td))
                            _queueReady = false;
                        OnInterpolationComplete?.Invoke();
                        
                }
            }

        }

        /// <summary>
        /// Sends transform data to clients if needed.
        /// </summary>
        private void SendToClients(byte lodIndex)
        {
            //True if clientAuthoritative and there is an owner.
            bool clientAuthoritativeWithOwner = (_clientAuthoritative && base.Owner.IsValid && !base.Owner.IsLocalClient);
            //Channel to send rpc on.
            Channel channel = Channel.Unreliable;
            //If relaying from client.
            if (clientAuthoritativeWithOwner)
            {
                if (_receivedClientData.HasData[lodIndex])
                {
                    _changedSinceStart = true;
                    //Resend data from clients.
                    ObserversUpdateTransform(_receivedClientData.Writer.GetArraySegment(), _receivedClientData.Channel);
                    _receivedClientData.SetHasData(false, lodIndex);
                }
            }
            //Sending server transform state.
            else
            {
                //Becomes true when any lod changes.
                bool dataChanged = false;
                //Check changes for every lod at and below passed in index.
                for (int i = lodIndex; i >= 0; i--)
                {
                    /* Reset writer. If does not have value 
                     * after these checks then we know
                     * there's nothing to send for this lod. */
                    PooledWriter writer = _changedWriters[i];
                    writer.Reset();

                    TransformData lastSentData = _lastSentTransformDatas[i];
                    ChangedDelta changed = GetChanged(lastSentData);
                    //If no change.
                    if (changed == ChangedDelta.Unset)
                    {
                        //No changes since last reliable; transform is up to date.
                        if (_serverChangedSinceReliable[i] == ChangedDelta.Unset)
                            continue;

                        //Set changed to all changes over time and unset changes over time.
                        changed = _serverChangedSinceReliable[lodIndex];
                        _serverChangedSinceReliable[i] = ChangedDelta.Unset;
                        channel = Channel.Reliable;
                    }
                    //There is change.
                    else
                    {
                        _serverChangedSinceReliable[i] |= changed;
                    }

                    dataChanged = true;
                    _changedSinceStart = true;
                    Transform t = transform;
                    /* If here a send for transform values will occur. Update last values.
                     * Tick doesn't need to be set for whoever controls transform. */
                    lastSentData.Update(0, t.localPosition, t.localRotation, t.localScale, t.localPosition, _parentBehaviour);

                    SerializeChanged(changed, writer);
                }

                //Send out changes.
                if (dataChanged)
                {
                    foreach (NetworkConnection nc in base.Observers)
                    {
                        //If to not send to owner.
                        if (!_sendToOwner && nc == base.Owner)
                            continue;

                        byte lod;
                        if (!nc.LevelOfDetails.TryGetValue(base.NetworkObject, out lod))
                            lod = 0;
                        //Not high enough index to send to conn.
                        if (lod > lodIndex)
                            continue;
                        //No need for server to send to local client (clientHost).
                        //Still send if development for stat tracking.
#if !DEVELOPMENT
                        if (!nc.IsLocalClient)
#endif
                        TargetUpdateTransform(nc, _changedWriters[lodIndex].GetArraySegment(), channel);
                    }
                }
            }

        }

        /// <summary>
        /// Sends transform data to server if needed.
        /// </summary>
        private void SendToServer(TransformData lastSentTransformData)
        {
            /* ClientHost does not need to send to the server.
             * Ideally this would still occur and the data be ignored
             * for statistics tracking but to keep the code more simple
             * we won't be doing that. Server out however still is tracked,
             * which is generally considered more important data. */
            if (base.IsServer)
                return;

            //Not client auth or not owner.
            if (!_clientAuthoritative || !base.IsOwner)
                return;

            //Channel to send on.
            Channel channel = Channel.Unreliable;
            //Values changed since last check.
            ChangedDelta changed = GetChanged(lastSentTransformData);

            //If no change.
            if (changed == ChangedDelta.Unset)
            {
                //No changes since last reliable; transform is up to date.
                if (_clientChangedSinceReliable == ChangedDelta.Unset)
                    return;

                //Set changed to all changes over time and unset changes over time.
                changed = _clientChangedSinceReliable;
                _clientChangedSinceReliable = ChangedDelta.Unset;
                channel = Channel.Reliable;
            }
            //There is change.
            else
            {
                _clientChangedSinceReliable |= changed;
            }

            /* If here a send for transform values will occur. Update last values.
            * Tick doesn't need to be set for whoever controls transform. */
            Transform t = transform;
            lastSentTransformData.Update(0, t.localPosition, t.localRotation, t.localScale, t.localPosition, _parentBehaviour);

            //Send latest.
            using (PooledWriter writer = WriterPool.GetWriter())
            {
                SerializeChanged(changed, writer);
                ServerUpdateTransform(writer.GetArraySegment(), channel);
            }
        }


        #region GetChanged.
        /// <summary>
        /// Returns if the transform differs from td.
        /// </summary>
        private bool HasChanged(TransformData td)
        {
            bool changed = (td.Position != transform.localPosition ||
                td.Rotation != transform.localRotation ||
                td.Scale != transform.localScale);

            return changed;
        }
        /// <summary>
        /// Returns if there is any change between two datas.
        /// </summary>
        private bool HasChanged(TransformData a, TransformData b)
        {
            return (a.Position != b.Position) ||
                (a.Rotation != b.Rotation) ||
                (a.Scale != b.Scale) ||
                (a.ParentBehaviour != b.ParentBehaviour);
        }
        /// <summary>
        /// Returns if there is any change between two datas and outputs what has changed.
        /// </summary>
        private bool HasChanged(TransformData a, TransformData b, ref ChangedFull changedFull)
        {
            bool hasChanged = false;

            if (a.Position != b.Position)
            {
                hasChanged = true;
                changedFull |= ChangedFull.Position;
            }
            if (a.Rotation != b.Rotation)
            {
                hasChanged = true;
                changedFull |= ChangedFull.Rotation;
            }
            if (a.Scale != b.Scale)
            {
                hasChanged = true;
                changedFull |= ChangedFull.Scale;
            }
            if (a.ParentBehaviour != b.ParentBehaviour)
            {
                hasChanged = true;
                changedFull |= ChangedFull.Nested;
            }

            return hasChanged;
        }
        /// <summary>
        /// Gets transform values that have changed against goalData.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ChangedDelta GetChanged(TransformData transformData)
        {
            /* If parent behaviour exist.
            * Parent isn't sent as a delta so
            * if it exist always send regardless
            * of the previously sent transform
            * data. */
            return GetChanged(ref transformData.Position, ref transformData.Rotation, ref transformData.Scale, transformData.ParentBehaviour);
        }
        /// <summary>
        /// Gets transform values that have changed against specified proprties.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ChangedDelta GetChanged(ref Vector3 lastPosition, ref Quaternion lastRotation, ref Vector3 lastScale, NetworkBehaviour lastParentBehaviour)
        {
            ChangedDelta changed = ChangedDelta.Unset;
            Transform t = transform;

            Vector3 position = t.localPosition;
            if (position.x != lastPosition.x)
                changed |= ChangedDelta.PositionX;
            if (position.y != lastPosition.y)
                changed |= ChangedDelta.PositionY;
            if (position.z != lastPosition.z)
                changed |= ChangedDelta.PositionZ;

            Quaternion rotation = t.localRotation;
            if (!rotation.Matches(lastRotation, true))
                changed |= ChangedDelta.Rotation;

            ChangedDelta startChanged;
            startChanged = changed;

            Vector3 scale = t.localScale;
            if (scale.x != lastScale.x)
                changed |= ChangedDelta.ScaleX;
            if (scale.y != lastScale.y)
                changed |= ChangedDelta.ScaleY;
            if (scale.z != lastScale.z)
                changed |= ChangedDelta.ScaleZ;

            //Only include parent if there is additional data to send.
            bool sendParent = (_parentBehaviour == null && lastParentBehaviour != null) || (changed != ChangedDelta.Unset && _parentBehaviour != null);
            if (sendParent)
                changed |= ChangedDelta.Nested;

            //If added scale or nested then also add extended.
            if (startChanged != changed)
                changed |= ChangedDelta.Extended;

            return changed;
        }
        #endregion

        #region Rates.
        /// <summary>
        /// Snaps transform properties using snapping settings.
        /// </summary>
        private void SnapProperties(TransformData transformData, bool force = false)
        {
            //Already snapped.
            if (transformData.Snapped)
                return;

            transformData.Snapped = true;
            Transform t = transform;

            //Position.
            if (_synchronizePosition)
            {
                Vector3 position;
                position.x = (force || _positionSnapping.X) ? transformData.Position.x : t.localPosition.x;
                position.y = (force || _positionSnapping.Y) ? transformData.Position.y : t.localPosition.y;
                position.z = (force || _positionSnapping.Z) ? transformData.Position.z : t.localPosition.z;
                t.localPosition = position;
            }

            //Rotation.
            if (_synchronizeRotation)
            {
                Vector3 eulers;
                Vector3 goalEulers = transformData.Rotation.eulerAngles;
                eulers.x = (force || _rotationSnapping.X) ? goalEulers.x : t.localEulerAngles.x;
                eulers.y = (force || _rotationSnapping.Y) ? goalEulers.y : t.localEulerAngles.y;
                eulers.z = (force || _rotationSnapping.Z) ? goalEulers.z : t.localEulerAngles.z;
                t.localEulerAngles = eulers;
            }

            //Scale.
            if (_synchronizeScale)
            {
                Vector3 scale;
                scale.x = (force || _scaleSnapping.X) ? transformData.Scale.x : t.localScale.x;
                scale.y = (force || _scaleSnapping.Y) ? transformData.Scale.y : t.localScale.y;
                scale.z = (force || _scaleSnapping.Z) ? transformData.Scale.z : t.localScale.z;
                t.localScale = scale;
            }
        }

        /// <summary>
        /// Sets move rates which will occur instantly.
        /// </summary>
        private void SetInstantRates(RateData rd)
        {
            rd.Update(-1f, -1f, -1f, -1f, 1, false, -1f);
        }

        /// <summary>
        /// Sets move rates which will occur over time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCalculatedRates(uint lastTick, RateData prevRd, TransformData prevTd, GoalData nextGd, ChangedFull changedFull, bool hasChanged, Channel channel)
        {
            /* Only update rates if data has changed.
             * When data comes in reliably for eventual consistency
             * it's possible that it will be the same as the last
             * unreliable packet. When this happens no change has occurred
             * and the distance of change woudl also be 0; this prevents
             * the NT from moving. Only need to compare data if channel is reliable. */
            TransformData td = nextGd.Transforms;
            if (channel == Channel.Reliable && !hasChanged)
            {
                nextGd.Rates.Update(prevRd);
                return;
            }

            /* How much time has passed between last update and current.
             * If set to 0 then that means the transform has
             * settled. */
            if (lastTick == 0)
                lastTick = (nextGd.Transforms.Tick - _interval);

            uint tickDifference = (td.Tick - lastTick);
            float timePassed = (float)base.NetworkManager.TimeManager.TicksToTime(tickDifference);

            //Distance between properties.
            float distance;
            float positionRate = 0f;
            float rotationRate = 0f;
            float scaleRate = 0f;

            RateData rd = nextGd.Rates;
            //Correction to apply towards rates when a rate change is detected as abnormal.
            float abnormalCorrection = 1f;
            bool abnormalRateDetected = false;
            float unalteredPositionRate = rd.LastUnalteredPositionRate;

            //Position.
            if (ChangedFullContains(changedFull, ChangedFull.Position))
            {
                Vector3 lastPosition = prevTd.Position;
                distance = Vector3.Distance(lastPosition, td.Position);
                //If distance teleports assume rest do.
                if (_enableTeleport && distance >= _teleportThreshold)
                {
                    SetInstantRates(rd);
                    return;
                }

                //Position distance already calculated.
                unalteredPositionRate = distance / timePassed;
                /* Try to detect abnormal rate changes.
                 * 
                 * This won't occur if the user
                 * is moving using the tick system but will likely happen when the transform
                 * is being moved in update.
                 * 
                 * Update will iterate a varying amount of times per tick,
                 * which will result in distances being slightly different. This is
                 * rarely an issue when the frame rate is high and the distance 
                 * variance is very little, but for games which are running at about
                 * the same frame rate as the tick it's possible the object will
                 * move twice the distance every few ticks. EG: if running 60 fps/50 tick.
                 * Execution may look like this..
                 * frame, tick, frame, tick, frame, frame, tick. The frame, frame would
                 * result in double movement distance. */

                //If last position rate is known then compare against it.
                if (unalteredPositionRate > 0f && rd.LastUnalteredPositionRate > 0f)
                {
                    float percentage = Mathf.Abs(1f - (unalteredPositionRate / rd.LastUnalteredPositionRate));
                    /* If percentage change is more than 25% then speed is considered
                     * to have changed drastically. */
                    if (percentage > 0.25f)
                    {
                        float c = (rd.LastUnalteredPositionRate / unalteredPositionRate);
                        /* Sometimes stop and goes can incorrectly trigger 
                         * an abnormal detection. Fortunately abnornalties tend
                         * to either skip a tick or send twice in one tick.
                         * Because of this it's fairly safe to assume that if the calculated
                         * correction is not ~0.5f or ~2f then it's a false detection. */
                        float allowedDifference = 0.1f;
                        if (
                            (c < 1f && Mathf.Abs(0.5f - c) < allowedDifference) ||
                            (c > 1f && Mathf.Abs(2f - c) < allowedDifference))
                        {
                            abnormalCorrection = c;
                            abnormalRateDetected = true;
                        }
                        /* If an abnormality has been marked then assume new rate
                         * is proper. When an abnormal rate occurs unintentionally
                         * the values will fix themselves next tick, therefor when
                         * rate changes drastically twice assume its intentional or
                         * that the rate had simply fixed itself, both which would unset
                         * abnormal rate detected. */
                    }
                }

                //abnormalCorrection = 1f;
                positionRate = (unalteredPositionRate * abnormalCorrection);
                if (positionRate <= 0f)
                    positionRate = -1f;
            }

            //Rotation.
            if (ChangedFullContains(changedFull, ChangedFull.Rotation))
            {
                Quaternion lastRotation = prevTd.Rotation;
                distance = lastRotation.Angle(td.Rotation, true);
                rotationRate = (distance / timePassed) * abnormalCorrection;
                if (rotationRate <= 0f)
                    rotationRate = -1f;
            }

            //Scale.
            if (ChangedFullContains(changedFull, ChangedFull.Scale))
            {
                Vector3 lastScale = prevTd.Scale;
                distance = Vector3.Distance(lastScale, td.Scale);
                scaleRate = (distance / timePassed) * abnormalCorrection;
                if (scaleRate <= 0f)
                    scaleRate = -1f;
            }

            rd.Update(positionRate, rotationRate, scaleRate, unalteredPositionRate, tickDifference, abnormalRateDetected, timePassed);

            //Returns if whole contains part.
            bool ChangedFullContains(ChangedFull whole, ChangedFull part)
            {
                return (whole & part) == part;
            }
        }
        #endregion

        /// <summary>
        /// Sets extrapolation data on next.
        /// </summary>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        /// <param name="channel"></param>
        private void SetExtrapolation(TransformData prev, TransformData next, Channel channel)
        {
            //Default value.
            next.ExtrapolationState = TransformData.ExtrapolateState.Disabled;

            
        }


        /// <summary>
        /// Updates a client with transform data.
        /// </summary>
        [TargetRpc(ValidateTarget = false)]
        private void TargetUpdateTransform(NetworkConnection conn, ArraySegment<byte> data, Channel channel)
        {
#if DEVELOPMENT
            //If receiver is client host then do nothing, clientHost need not process.
            if (base.IsServer && conn.IsLocalClient)
                return;
#endif

            byte lod;
            /* Get cached LOD for connection receiving this. It will of course
             * always be the local client for conn. */
            if (!conn.LevelOfDetails.TryGetValue(base.NetworkObject, out lod))
                lod = 0;

            //Update the interval so speed calculations are proper.
            _interval = base.ObserverManager.GetLevelOfDetailInterval(lod);
            DataReceived(data, channel, false);
        }

        /// <summary>
        /// Updates clients with transform data.
        /// </summary>
        [ObserversRpc]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ObserversUpdateTransform(ArraySegment<byte> data, Channel channel)
        {
            if (!_clientAuthoritative && base.IsOwner && !_sendToOwner)
                return;
            if (_clientAuthoritative && base.IsOwner)
                return;
            if (base.IsServer)
                return;

            //Not new data.
            uint lastPacketTick = base.TimeManager.LastPacketTick;
            if (lastPacketTick <= _lastObserversRpcTick)
                return;
            _lastObserversRpcTick = lastPacketTick;

            DataReceived(data, channel, false);
        }

        ///// <summary>
        ///// Updates clients with transform data.
        ///// </summary>
        //[TargetRpc(ValidateTarget = false)]
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private void TargetUpdateTransform(NetworkConnection conn, ArraySegment<byte> data, Channel channel)
        //{
        //    if (!_clientAuthoritative && base.IsOwner && !_sendToOwner)
        //        return;
        //    if (_clientAuthoritative && base.IsOwner)
        //        return;
        //    if (base.IsServer)
        //        return;

        //    //Not new data.
        //    uint lastPacketTick = base.TimeManager.LastPacketTick;
        //    if (lastPacketTick <= _lastObserversRpcTick)
        //        return;
        //    _lastObserversRpcTick = lastPacketTick;

        //    DataReceived(data, channel, false);
        //}

        /// <summary>
        /// Updates the transform on the server.
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="channel"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ServerRpc]
        private void ServerUpdateTransform(ArraySegment<byte> data, Channel channel)
        {
            if (!_clientAuthoritative)
            {
                base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
                return;
            }

            //Not new data.
            uint lastPacketTick = base.TimeManager.LastPacketTick;
            if (lastPacketTick <= _lastServerRpcTick)
                return;
            _lastServerRpcTick = lastPacketTick;

            //Populate writer if it doesn't exist.
            if (_receivedClientData.Writer == null)
                _receivedClientData.Writer = WriterPool.GetWriter();
            _receivedClientData.Channel = channel;
            _receivedClientData.Writer.Reset();
            _receivedClientData.Writer.WriteArraySegment(data);
            _receivedClientData.SetHasData(true);

            DataReceived(data, channel, true);
        }

        /// <summary>
        /// Processes received data for lcients and server.
        /// </summary>
        private void DataReceived(ArraySegment<byte> data, Channel channel, bool asServer)
        {
            TransformData prevTd = _lastReceivedTransformData;
            RateData prevRd = _lastCalculatedRateData;
            ChangedFull changedFull = new ChangedFull();

            GoalData nextGd = GetCachedGoalData();
            TransformData nextTd = nextGd.Transforms;
            UpdateTransformData(data, prevTd, nextTd, ref changedFull);
            OnDataReceived?.Invoke(prevTd, nextTd);
            SetExtrapolation(prevTd, nextTd, channel);

            bool hasChanged = HasChanged(prevTd, nextTd);
            //If server only teleport.
            if (asServer && !base.IsClient)
                SetInstantRates(nextGd.Rates);
            //Otherwise use timed.
            else
                SetCalculatedRates(prevTd.Tick, prevRd, prevTd, nextGd, changedFull, hasChanged, channel);

            _lastReceivedTransformData.Update(nextTd);

            _lastReceiveReliable = (channel == Channel.Reliable);
            /* If channel is reliable then this is a settled packet.
             * Reset last received tick so next starting move eases
             * in. */
            if (channel == Channel.Reliable)
                nextTd.Tick = 0;

            prevTd.Update(nextTd);
            prevRd.Update(nextGd.Rates);

            nextGd.ReceivedTick = base.TimeManager.LocalTick;

            /* If extrapolating then immediately break the extrapolation
            * in favor of newest results. This will keep the buffer
            * at 0 until the transform settles but the only other option is
            * to stop the movement, which would defeat purpose of extrapolation,
            * or slow down the transform while buffer rebuilds. Neither choice
            * is great but later on I might try slowing down the transform slightly
            * to give the buffer a chance to rebuild. */
            if (_currentGoalData.Transforms.ExtrapolationState == TransformData.ExtrapolateState.Active)
            {
                _queueReady = true;
                SetCurrentGoalData(nextGd);
            }
            /* If queue isn't started and its buffered enough
             * to satisfy interpolation then set ready
             * and set current data.
             * 
             * Also if reliable then begin moving. */
            else if (!_queueReady && _goalDataQueue.Count >= _interpolation
                || channel == Channel.Reliable)
            {
                _queueReady = true;
                if (_goalDataQueue.Count > 0)
                {
                    SetCurrentGoalData(_goalDataQueue.Dequeue());
                    /* If is reliable and has changed then also
                    * enqueue latest. */
                    if (hasChanged)
                        _goalDataQueue.Enqueue(nextGd);

                }
                else
                {
                    SetCurrentGoalData(nextGd);
                }
            }
            /* If here then there's not enough in buffer to begin
             * so add onto the buffer. */
            else
            {
                _goalDataQueue.Enqueue(nextGd);
            }

            /* If the queue is excessive beyond interpolation then
             * dequeue extras to prevent from dropping behind too
             * quickly. This shouldn't be an issue with normal movement
             * as the NT speeds up if the buffer unexpectedly grows, but
             * when connections are unstable results may come in chunks
             * and for a better experience the older parts of the chunks
             * will be dropped. */
            if (_goalDataQueue.Count > (_interpolation + 3))
            {
                while (_goalDataQueue.Count > _interpolation)
                {
                    GoalData tmpGd = _goalDataQueue.Dequeue();
                    _goalDataCache.Push(tmpGd);
                }
                //Snap to the next data to fix any smoothing timings.
                SetCurrentGoalData(_goalDataQueue.Dequeue());
                SetInstantRates(_currentGoalData.Rates);
                SnapProperties(_currentGoalData.Transforms, true);
            }
        }

        /// <summary>
        /// Sets CurrentGoalData value.
        /// </summary>
        /// <param name="data"></param>
        private void SetCurrentGoalData(GoalData data)
        {
            _currentGoalData = data;
            OnNextGoal?.Invoke(data);
        }

        /// <summary>
        /// Immediately sets the parent of this NetworkTransform for a single connection.
        /// </summary>
        [TargetRpc]
        private void TargetSetParent(NetworkConnection conn, NetworkBehaviour parent)
        {
            
        }

        /// <summary>
        /// Updates a TransformData from packetData.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTransformData(ArraySegment<byte> packetData, TransformData prevTransformData, TransformData nextTransformData, ref ChangedFull changedFull)
        {
            DeserializePacket(packetData, prevTransformData, nextTransformData, ref changedFull);
            nextTransformData.Tick = base.TimeManager.LastPacketTick;
        }

        /// <summary>
        /// Returns a GoalData from the cache.
        /// </summary>
        /// <returns></returns>
        private GoalData GetCachedGoalData()
        {
            GoalData result = (_goalDataCache.Count > 0) ? _goalDataCache.Pop() : new GoalData();
            result.Reset();
            return result;
        }

        /// <summary>
        /// Configures this NetworkTransform for CSP.
        /// </summary>
        internal void ConfigureForCSP()
        {
            _clientAuthoritative = false;
            if (base.IsServer)
                _sendToOwner = false;

            /* If other or CC then needs to be configured.
             * When CC it will be configured properly, if there
             * is no CC then no action will be taken. */
            _componentConfiguration = ComponentConfigurationType.CharacterController;
            ConfigureComponents();
        }

        /// <summary>
        /// Updates which properties are synchronized.
        /// </summary>
        /// <param name="value">Properties to synchronize.</param>
        public void SetSynchronizedProperties(SynchronizedProperty value)
        {
            /* Make sure permissions are proper to change values.
             * Let the server override client auth. 
             *
             * Can send if server.
             * Or owner + client auth.
             */
            bool canSend = (
                base.IsServer ||
                (_clientAuthoritative && base.IsOwner)
                );

            if (!canSend)
                return;

            //If server send out observerRpc.
            if (base.IsServer)
                ObserversSetSynchronizedProperties(value);
            //Otherwise send to the server.
            else
                ServerSetSynchronizedProperties(value);
        }

        /// <summary>
        /// Sets synchronized values based on value.
        /// </summary>
        [ServerRpc]
        private void ServerSetSynchronizedProperties(SynchronizedProperty value)
        {
            if (!_clientAuthoritative)
            {
                base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
                return;
            }

            SetSynchronizedPropertiesInternal(value);
            ObserversSetSynchronizedProperties(value);
        }

        /// <summary>
        /// Sets synchronized values based on value.
        /// </summary>
        [ObserversRpc(BufferLast = true)]
        private void ObserversSetSynchronizedProperties(SynchronizedProperty value)
        {
            //Would have already run on server if host.
            if (base.IsServer)
                return;

            SetSynchronizedPropertiesInternal(value);
        }

        /// <summary>
        /// Sets synchronized values based on value.
        /// </summary>
        private void SetSynchronizedPropertiesInternal(SynchronizedProperty value)
        {
            _synchronizeParent = SynchronizedPropertyContains(value, SynchronizedProperty.Parent);
            _synchronizePosition = SynchronizedPropertyContains(value, SynchronizedProperty.Position);
            _synchronizeRotation = SynchronizedPropertyContains(value, SynchronizedProperty.Rotation);
            _synchronizeScale = SynchronizedPropertyContains(value, SynchronizedProperty.Scale);

            bool SynchronizedPropertyContains(SynchronizedProperty whole, SynchronizedProperty part)
            {
                return (whole & part) == part;
            }
        }
    }


}