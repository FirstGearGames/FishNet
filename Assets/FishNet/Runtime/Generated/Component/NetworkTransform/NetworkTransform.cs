using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Transforming
{
    /// <summary> 
    /// A somewhat basic but reliable NetworkTransform that will be improved upon greatly after release.
    /// </summary>   
    public class NetworkTransform : NetworkBehaviour
    {
        #region Types.
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
            X = 1,
            Y = 2,
            Z = 4,
            Rotation = 8,
            Scale = 16
        }
        private enum ChangedFull
        {
            Unset = 0,
            Position = 1,
            Rotation = 2,
            Scale = 4
        }
        private enum SpecialFlag : byte
        {

        }

        private enum UpdateFlag : byte
        {
            Unset = 0,
            X2 = 1,
            X4 = 2,
            Y2 = 4,
            Y4 = 8,
            Z2 = 16,
            Z4 = 32,
            Rotation = 64,
            Scale = 128
        }

        private struct RateData
        {
            public float Position;
            public float Rotation;
            public float Scale;
            public float LastUnalteredPositionRate;
            public bool AbnormalRateDetected;

            public RateData(float position, float rotation, float scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
                LastUnalteredPositionRate = -1f;
                AbnormalRateDetected = false;
            }

            /// <summary>
            /// Updates rates.
            /// </summary>
            public void Update(float position, float rotation, float scale, float unalteredPositionRate, bool abnormalRateDetected)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
                LastUnalteredPositionRate = unalteredPositionRate;
                AbnormalRateDetected = abnormalRateDetected;
            }
        }

        private struct GoalData
        {
            public uint Tick;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;

            public GoalData(uint tick, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Tick = tick;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }

        #endregion

        #region Serialized.
        /// <summary>
        /// True to use clasic methods to calculate move rates. This setting will be removed when NetworkTransform is finalized.
        /// </summary>
        [Tooltip("True to use clasic methods to calculate move rates. This setting will be removed when NetworkTransform is finalized.")]
        [SerializeField]
        private bool _classic = true;
        /// <summary>
        /// True to compress values. If you find accuracy of transform properties to be less than desirable try disabling this option.
        /// </summary>
        [Tooltip("True to compress values. If you find accuracy of transform properties to be less than desirable try disabling this option.")]
        [SerializeField]
        private bool _compress = true;
        /// <summary>
        /// True to synchronize when this transform changes parent.
        /// </summary>
        [Tooltip("True to synchronize when this transform changes parent.")]
        [SerializeField]
        private bool _synchronizeParent;
        /// <summary>
        /// How many ticks to interpolate.
        /// </summary>
        [Tooltip("How many ticks to interpolate.")]
        [Range(1, 9999)]
        [SerializeField]
        private ushort _interpolation = 2;
        /// <summary>
        /// How many ticks to extrapolate.
        /// </summary>
        [Tooltip("How many ticks to extrapolate.")]
        [Range(0, 9999)]
        [SerializeField]
        private ushort _extrapolation;
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
        /// Axes to snap on position.
        /// </summary>
        [Tooltip("Axes to snap on position.")]
        [SerializeField]
        private SnappedAxes _positionSnapping = new SnappedAxes();
        /// <summary>
        /// Axes to snap on rotation.
        /// </summary>
        [Tooltip("Axes to snap on rotation.")]
        [SerializeField]
        private SnappedAxes _rotationSnapping = new SnappedAxes();
        /// <summary>
        /// Axes to snap on scale.
        /// </summary>
        [Tooltip("Axes to snap on scale.")]
        [SerializeField]
        private SnappedAxes _scaleSnapping = new SnappedAxes();
        #endregion


        /// <summary>
        /// Values changed over time that server has sent to clients since last reliable has been sent.
        /// </summary>
        private ChangedDelta _serverChangedSinceReliable = ChangedDelta.Unset;
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
        private PooledWriter _receivedClientBytes;
        /// <summary>
        /// True when receivedClientBytes contains new data.
        /// </summary>
        private bool _clientBytesChanged;
        /// <summary>
        /// Data on how the server should move the transform.
        /// </summary> 
        private GoalData _serverGoalData;
        /// <summary>
        /// Goals for how the client should modify the transform.
        /// </summary>
        private GoalData _clientGoalData;
        /// <summary>
        /// Move rates for how fast the transform should update on server.
        /// </summary>
        private RateData _serverRateData;
        /// <summary>
        /// Move rates for how fast the transform should update on client.
        /// </summary>
        private RateData _clientRateData;
        /// <summary>
        /// True if subscribed to TimeManager for ticks.
        /// </summary>
        private bool _subscribedToTicks;
        /// <summary>
        /// Last sent transform values. Can be used for client or server.
        /// </summary>
        private GoalData _lastTransformValues;

        private void OnDisable()
        {
            if (_receivedClientBytes != null)
            {
                _receivedClientBytes.Dispose();
                _receivedClientBytes = null;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SetDefaultGoalDatas(true, false);
            SetInstantRates(true, false);

            /* Server must always subscribe.
             * Server needs to relay client auth in
             * ticks or send non-auth/non-owner to
             * clients in tick. */
            ChangeTickSubscription(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetDefaultGoalDatas(false, true);
            SetInstantRates(false, true);
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
                    SetDefaultGoalDatas(false, true);

                if (!base.IsServer)
                    ChangeTickSubscription(false);
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            //Always unsubscribe; if the server stopped so did client.
            ChangeTickSubscription(false);
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
        /// Called when a tick occurs.
        /// </summary>
        private void TimeManager_OnTick()
        {
            if (base.IsServer)
                SendToClients();
            if (base.IsClient)
                SendToServer();
        }

        /* 
         * //todo make a special method for handling network transforms that iterates all
         * of them at once and ALWAYS send the packetId TransformUpdate. This packet will
         * have the total length of all updates. theres a chance a nob might not exist since
         * these packets are unreliable and can arrive after a nob destruction. if thats
         * the case then the packet can still be parsed out and recovered because the updateflags
         * indicates exactly what data needs to be read.
         */


        /// <summary>
        /// Tries to subscribe to TimeManager ticks.
        /// </summary>
        private void ChangeTickSubscription(bool subscribe)
        {
            if (subscribe == _subscribedToTicks)
                return;

            _subscribedToTicks = subscribe;
            if (subscribe)
                base.NetworkManager.TimeManager.OnTick += TimeManager_OnTick;
            else
                base.NetworkManager.TimeManager.OnTick -= TimeManager_OnTick;
        }


        /// <summary>
        /// Creates goal data using current position.
        /// </summary>
        private void SetDefaultGoalDatas(bool forServer, bool forClient)
        {
            Transform t = transform;
            if (forServer)
                _serverGoalData = new GoalData(0, t.localPosition, t.localRotation, t.localScale);
            if (forClient)
                _clientGoalData = new GoalData(0, t.localPosition, t.localRotation, t.localScale);
        }

        /// <summary>
        /// Serializes only changed data into writer.
        /// </summary>
        /// <param name="changed"></param>
        /// <param name="writer"></param>
        private void SerializeChanged(ChangedDelta changed, PooledWriter writer)
        {
            UpdateFlag updateFlags = UpdateFlag.Unset;

            int startIndex = writer.Position;
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
            //X
            if (ChangedContains(changed, ChangedDelta.X))
            {
                original = t.localPosition.x;
                compressed = original * multiplier;
                if (_compress && Math.Abs(compressed) <= maxValue)
                {
                    updateFlags |= UpdateFlag.X2;
                    writer.WriteInt16((short)compressed);
                }
                else
                {
                    updateFlags |= UpdateFlag.X4;
                    writer.WriteSingle(original);
                }
            }
            //Y
            if (ChangedContains(changed, ChangedDelta.Y))
            {
                original = t.localPosition.y;
                compressed = original * multiplier;
                if (_compress && Math.Abs(compressed) <= maxValue)
                {
                    updateFlags |= UpdateFlag.Y2;
                    writer.WriteInt16((short)compressed);
                }
                else
                {
                    updateFlags |= UpdateFlag.Y4;
                    writer.WriteSingle(original);
                }
            }
            //Z
            if (ChangedContains(changed, ChangedDelta.Z))
            {
                original = t.localPosition.z;
                compressed = original * multiplier;
                if (_compress && Math.Abs(compressed) <= maxValue)
                {
                    updateFlags |= UpdateFlag.Z2;
                    writer.WriteInt16((short)compressed);
                }
                else
                {
                    updateFlags |= UpdateFlag.Z4;
                    writer.WriteSingle(original);
                }
            }

            //Rotation.
            if (ChangedContains(changed, ChangedDelta.Rotation))
            {
                updateFlags |= UpdateFlag.Rotation;
                writer.WriteQuaternion(t.localRotation);
            }

            //Scale.
            if (ChangedContains(changed, ChangedDelta.Scale))
            {
                updateFlags |= UpdateFlag.Scale;
                writer.WriteVector3(t.localScale);
            }

            //Insert flags at start.
            writer.FastInsertByte((byte)updateFlags, startIndex);

            bool ChangedContains(ChangedDelta whole, ChangedDelta part)
            {
                return (whole & part) == part;
            }
        }

        /// <summary>
        /// Deerializes a received packet.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeserializePacket(ArraySegment<byte> data, ref GoalData goalData, ref ChangedFull changedFull)
        {
            using (PooledReader r = ReaderPool.GetReader(data, base.NetworkManager))
            {
                UpdateFlag updateFlags = (UpdateFlag)r.ReadByte();

                int readerRemaining = r.Remaining;

                //X
                if (UpdateFlagContains(updateFlags, UpdateFlag.X2))
                    goalData.Position.x = r.ReadInt16() / 100f;
                else if (UpdateFlagContains(updateFlags, UpdateFlag.X4))
                    goalData.Position.x = r.ReadSingle();
                //Y
                if (UpdateFlagContains(updateFlags, UpdateFlag.Y2))
                    goalData.Position.y = r.ReadInt16() / 100f;
                else if (UpdateFlagContains(updateFlags, UpdateFlag.Y4))
                    goalData.Position.y = r.ReadSingle();
                //Z
                if (UpdateFlagContains(updateFlags, UpdateFlag.Z2))
                    goalData.Position.z = r.ReadInt16() / 100f;
                else if (UpdateFlagContains(updateFlags, UpdateFlag.Z4))
                    goalData.Position.z = r.ReadSingle();

                //If remaining has changed then a position was read.
                if (readerRemaining != r.Remaining)
                    changedFull |= ChangedFull.Position;

                //Rotation.
                if (UpdateFlagContains(updateFlags, UpdateFlag.Rotation))
                {
                    goalData.Rotation = r.ReadQuaternion();
                    changedFull |= ChangedFull.Rotation;
                }

                //Scale.
                if (UpdateFlagContains(updateFlags, UpdateFlag.Scale))
                {
                    goalData.Scale = r.ReadVector3();
                    changedFull |= ChangedFull.Scale;
                }
            }


            //Returns if whole contains part.
            bool UpdateFlagContains(UpdateFlag whole, UpdateFlag part)
            {
                return (whole & part) == part;
            }
        }

        /// <summary>
        /// Moves to a GoalData. Automatically determins if to use data from server or client.
        /// </summary>
        private void MoveToTarget()
        {
            //Cannot move if neither is active.
            if (!base.IsServer && !base.IsClient)
                return;
            //If client auth and the owner don't move towards target.
            if (_clientAuthoritative && base.IsOwner)
                return;
            //If not client authoritative, is owner, and don't sync to owner.
            if (!_clientAuthoritative && base.IsOwner && !_sendToOwner)
                return;
            //True if not client controlled.
            bool controlledByClient = (_clientAuthoritative && base.OwnerIsActive);
            //If not controlled by client and is server then no reason to move.
            if (!controlledByClient && base.IsServer)
                return;

            /* Once here it's safe to assume the object will be moving.
             * Any checks which would stop it from moving be it client
             * auth and owner, or server controlled and server, ect,
             * would have already been run. */
            GoalData goalData;
            RateData rateData;


            /* If not the server then client data
             * will always be used since server data
             * would not be available. */
            if (!base.IsServer)
            {
                goalData = _clientGoalData;
                rateData = _clientRateData;
            }
            /* If the server then server data will be used,
             * even if also a client. The server side will
             * have the latest values from clients so
             * it makes sense to use server data rather than
             * client which is host. */
            else
            {
                goalData = _serverGoalData;
                rateData = _serverRateData;
            }

            //Rate to update. Changes per property.
            float rate;

            Transform t = transform;
            //Position.
            rate = rateData.Position;
            if (rate == -1f)
                t.localPosition = goalData.Position;
            else
                t.localPosition = Vector3.MoveTowards(t.localPosition, goalData.Position, rate * Time.deltaTime);
            //Rotation.
            rate = rateData.Rotation;
            if (rate == -1f)
                t.localRotation = goalData.Rotation;
            else
                t.localRotation = Quaternion.RotateTowards(t.localRotation, goalData.Rotation, rate * Time.deltaTime);
            //Scale.
            rate = rateData.Scale;
            if (rate == -1f)
                t.localScale = goalData.Scale;
            else
                t.localScale = Vector3.MoveTowards(t.localScale, goalData.Scale, rate * Time.deltaTime);
        }

        /// <summary>
        /// Sends transform data to clients if needed.
        /// </summary>
        private void SendToClients()
        {
            //True if to send transform state rather than received state from client.
            bool sendServerState = (_receivedClientBytes == null || _receivedClientBytes.Length == 0 || !base.OwnerIsValid);
            //Channel to send rpc on.
            Channel channel = Channel.Unreliable;
            //If relaying from client.
            if (!sendServerState)
            {
                //No new data from clients.
                if (!_clientBytesChanged)
                    return;

                //Resend data from clients.
                ObserversUpdateTransform(_receivedClientBytes.GetArraySegment(), channel);
            }
            //Sending server transform state.
            else
            {
                ChangedDelta changed = GetChanged(_lastTransformValues);

                //If no change.
                if (changed == ChangedDelta.Unset)
                {
                    //No changes since last reliable; transform is up to date.
                    if (_serverChangedSinceReliable == ChangedDelta.Unset)
                        return;

                    //Set changed to all changes over time and unset changes over time.
                    changed = _serverChangedSinceReliable;
                    _serverChangedSinceReliable = ChangedDelta.Unset;
                    channel = Channel.Reliable;
                }
                //There is change.
                else
                {
                    _serverChangedSinceReliable |= changed;
                }

                Transform t = transform;
                /* If here a send for transform values will occur. Update last values.
                 * Tick doesn't need to be set for whoever controls transform. */
                _lastTransformValues = new GoalData(0,
                    t.localPosition, t.localRotation, t.localScale);

                //Send latest.
                using (PooledWriter writer = WriterPool.GetWriter())
                {
                    SerializeChanged(changed, writer);
                    ObserversUpdateTransform(writer.GetArraySegment(), channel);
                }
            }

        }

        /// <summary>
        /// Sends transform data to server if needed.
        /// </summary>
        private void SendToServer()
        {
            //Not client auth or not owner.
            if (!_clientAuthoritative || !base.IsOwner)
                return;

            //Channel to send on.
            Channel channel = Channel.Unreliable;
            //Values changed since last check.
            ChangedDelta changed = GetChanged(_lastTransformValues);

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
            _lastTransformValues = new GoalData(0,
                t.localPosition, t.localRotation, t.localScale);

            //Send latest.
            using (PooledWriter writer = WriterPool.GetWriter())
            {
                SerializeChanged(changed, writer);
                ServerUpdateTransform(writer.GetArraySegment(), channel);
            }
        }

        #region GetChanged.
        /// <summary>
        /// Returns if there is any change between two datas.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasChanged(ref GoalData a, ref GoalData b, ref ChangedFull changedFull)
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

            return hasChanged;
        }
        /// <summary>
        /// Gets transform values that have changed against goalData.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ChangedDelta GetChanged(GoalData goalData)
        {
            return GetChanged(ref goalData.Position, ref goalData.Rotation, ref goalData.Scale);
        }
        /// <summary>
        /// Gets transform values that have changed against specified proprties.
        /// </summary>
        private ChangedDelta GetChanged(ref Vector3 lastPosition, ref Quaternion lastRotation, ref Vector3 lastScale)
        {
            ChangedDelta changed = ChangedDelta.Unset;
            Transform t = transform;

            Vector3 position = t.localPosition;
            if (position.x != lastPosition.x)
                changed |= ChangedDelta.X;
            if (position.y != lastPosition.y)
                changed |= ChangedDelta.Y;
            if (position.z != lastPosition.z)
                changed |= ChangedDelta.Z;

            Quaternion rotation = t.localRotation;
            //if (rotation.eulerAngles != lastRotation.eulerAngles)
            if (rotation != lastRotation)
                changed |= ChangedDelta.Rotation;

            Vector3 scale = t.localScale;
            if (scale != lastScale)
                changed |= ChangedDelta.Scale;

            return changed;
        }
        #endregion

        #region Rates.
        /// <summary>
        /// Snaps transform properties using snapping settings.
        /// </summary>
        private void SnapProperties(ref GoalData goalData)
        {
            Transform t = transform;
            //Position.
            Vector3 position;
            position.x = (_positionSnapping.X) ? goalData.Position.x : t.localPosition.x;
            position.y = (_positionSnapping.Y) ? goalData.Position.y : t.localPosition.y;
            position.z = (_positionSnapping.Z) ? goalData.Position.z : t.localPosition.z;
            t.localPosition = position;
            //Rotation.
            Vector3 eulers;
            Vector3 goalEulers = goalData.Rotation.eulerAngles;
            eulers.x = (_rotationSnapping.X) ? goalEulers.x : t.localEulerAngles.x;
            eulers.y = (_rotationSnapping.Y) ? goalEulers.y : t.localEulerAngles.y;
            eulers.z = (_rotationSnapping.Z) ? goalEulers.z : t.localEulerAngles.z;
            t.localEulerAngles = eulers;
            //Scale.
            Vector3 scale;
            scale.x = (_scaleSnapping.X) ? goalData.Scale.x : t.localScale.x;
            scale.y = (_scaleSnapping.Y) ? goalData.Scale.y : t.localScale.y;
            scale.z = (_scaleSnapping.Z) ? goalData.Scale.z : t.localScale.z;
            t.localScale = scale;
        }

        /// <summary>
        /// Sets move rates which will occur instantly.
        /// </summary>
        private void SetInstantRates(bool forServer, bool forClient)
        {
            RateData rd = new RateData(-1f, -1f, -1f);
            if (forServer)
                _serverRateData = rd;
            if (forClient)
                _clientRateData = rd;
        }

        /// <summary>
        /// Sets move rates which will occur over time.
        /// </summary>
        // 
        private void SetCalculatedRates(uint lastTick, ref GoalData oldGoalData, ref GoalData goalData, ChangedFull changedFull, bool forServer, Channel channel)
        {
            /* Only update rates if data has changed.
             * When data comes in reliably for eventual consistency
             * it's possible that it will be the same as the last
             * unreliable packet. When this happens no change has occurred
             * and the distance of change woudl also be 0; this prevents
             * the NT from moving. Only need to compare data if channel is reliable. */
            if (channel == Channel.Reliable && !HasChanged(ref oldGoalData, ref goalData, ref changedFull))
                return;

            //How much time has passed between last update and current.
            float timePassed;
            if (_classic)
            {
                float tickDelta = (float)base.NetworkManager.TimeManager.TickDelta;
                //Save another call to timemanager by calculating locally.
                timePassed = tickDelta + (tickDelta * _interpolation);
            }
            else
            {
                if (lastTick == 0)
                    lastTick = (goalData.Tick - 1);

                uint tickDifference = (goalData.Tick - lastTick);
                timePassed = base.NetworkManager.TimeManager.TicksToTime(tickDifference);
            }            

            //Distance between properties.
            float distance;
            float positionRate;
            float rotationRate;
            float scaleRate;

            RateData rd = (forServer) ? _serverRateData : _clientRateData;
            //Correction to apply towards rates when a rate change is detected as abnormal.
            float abnormalCorrection = 1f;
            bool abnormalRateDetected = false;
            float unalteredPositionRate = rd.LastUnalteredPositionRate;

            //Position.
            if (ChangedFullContains(changedFull, ChangedFull.Position))
            {
                Vector3 lastPosition = (_classic) ? transform.localPosition : oldGoalData.Position;
                distance = Vector3.Distance(lastPosition, goalData.Position);
                //If distance teleports assume rest do.
                if (_enableTeleport && distance >= _teleportThreshold)
                {
                    SetInstantRates(forServer, !forServer);
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

                positionRate = (unalteredPositionRate * abnormalCorrection);
            }
            else
            {
                positionRate = rd.Position;
            }

            //Rotation.
            if (ChangedFullContains(changedFull, ChangedFull.Rotation))
            {
                Quaternion lastRotation = (_classic) ? transform.localRotation : oldGoalData.Rotation;
                distance = Quaternion.Angle(lastRotation, goalData.Rotation);
                rotationRate = (distance / timePassed) * abnormalCorrection;
            }
            else
            {
                rotationRate = rd.Rotation;
            }

            //Scale.
            if (ChangedFullContains(changedFull, ChangedFull.Scale))
            {
                Vector3 lastScale = (_classic) ? transform.localScale : oldGoalData.Scale;
                distance = Vector3.Distance(lastScale, goalData.Scale);
                scaleRate = (distance / timePassed) * abnormalCorrection;
            }
            else
            {
                scaleRate = rd.Scale;
            }            

            rd.Update(positionRate, rotationRate, scaleRate, unalteredPositionRate, abnormalRateDetected);
            //Update appropriate rate.
            if (forServer)
                _serverRateData = rd;
            else
                _clientRateData = rd;

            //Returns if whole contains part.
            bool ChangedFullContains(ChangedFull whole, ChangedFull part)
            {
                return (whole & part) == part;
            }
        }
        #endregion       

        /// <summary>
        /// Updates the transform on the server.
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="channel"></param>
        [ServerRpc]
        private void ServerUpdateTransform(ArraySegment<byte> data, Channel channel)
        {
            //Not new data.
            uint lastPacketTick = base.TimeManager.LastPacketTick;
            if (lastPacketTick <= _lastServerRpcTick)
                return;
            _lastServerRpcTick = lastPacketTick;

            //Set to received bytes.
            if (_receivedClientBytes == null)
                _receivedClientBytes = WriterPool.GetWriter();
            _receivedClientBytes.Reset();
            _receivedClientBytes.WriteArraySegment(data);

            //Indicates new data has been received from client.
            _clientBytesChanged = true;

            GoalData oldGoalData = _serverGoalData;
            //Tick from last goal data.
            uint lastTick = oldGoalData.Tick;
            ChangedFull changedFull = new ChangedFull();
            UpdateGoalData(data, ref _serverGoalData, ref changedFull);

            //If server only teleport.
            if (!base.IsClient)
                SetInstantRates(true, false);
            //Otherwise use timed.
            else
                SetCalculatedRates(lastTick, ref oldGoalData, ref _serverGoalData, changedFull, true, channel);
            SnapProperties(ref _serverGoalData);

            /* If channel is reliable then this is a settled packet.
             * Reset last received tick so next starting move eases
             * in. */
            if (channel == Channel.Reliable)
                _serverGoalData.Tick = 0;
        }

        /// <summary>
        /// Updates clients with transform data.
        /// </summary>
        /// <param name="tb"></param>
        /// <param name="channel"></param>
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

            GoalData oldGoalData = _clientGoalData;
            //Tick from last goal data.
            uint lastTick = oldGoalData.Tick;
            ChangedFull changedFull = new ChangedFull();
            UpdateGoalData(data, ref _clientGoalData, ref changedFull);
            SetCalculatedRates(lastTick, ref oldGoalData, ref _clientGoalData, changedFull, false, channel);
            SnapProperties(ref _clientGoalData);
        }

        /// <summary>
        /// Updates a GoalData from packetData.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateGoalData(ArraySegment<byte> packetData, ref GoalData goalData, ref ChangedFull changedFull)
        {
            DeserializePacket(packetData, ref goalData, ref changedFull);
            goalData.Tick = base.TimeManager.LastPacketTick;
        }
    }


}