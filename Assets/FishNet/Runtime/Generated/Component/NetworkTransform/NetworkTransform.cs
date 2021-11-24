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
        private enum Changed
        {
            Unset = 0,
            X = 1,
            Y = 2,
            Z = 4,
            Rotation = 8,
            Scale = 16
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

            public RateData(float position, float rotation, float scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
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
        /// True to compress small values. If you find accuracy of transform properties or speed simiulation to be less than desirable try disabling this option.
        /// </summary>
        [Tooltip("True to compress small values. If you find accuracy of transform properties or speed simiulation to be less than desirable try disabling this option.")]
        [SerializeField]
        private bool _compressSmall = true;
        /// <summary>
        /// True to enable teleport threshhold.
        /// </summary>
        [Tooltip("True to enable teleport threshhold.")]
        [SerializeField]
        private bool _enableTeleport = false;
        /// <summary>
        /// How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.
        /// </summary>
        [Tooltip("How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.")]
        [SerializeField]
        private float _teleportThreshold = 0f;
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
        #endregion


        /// <summary>
        /// Values changed over time that server has sent to clients since last reliable has been sent.
        /// </summary>
        private Changed _serverChangedSinceReliable = Changed.Unset;
        /// <summary>
        /// Values changed over time that client has sent to server since last reliable has been sent.
        /// </summary>
        private Changed _clientChangedSinceReliable = Changed.Unset;
        /// <summary>
        /// Last frame tick occurred on.
        /// </summary>
        private int _lastTickFrame = 0;
        /// <summary>
        /// Last tick an ObserverRpc passed checks.
        /// </summary>
        private uint _lastObserversRpcTick = 0;
        /// <summary>
        /// Last tick a ServerRpc passed checks.
        /// </summary>
        private uint _lastServerRpcTick = 0;
        /// <summary>
        /// Last received data from an authoritative client.
        /// </summary>
        private PooledWriter _receivedClientBytes = null;
        /// <summary>
        /// True when receivedClientBytes contains new data.
        /// </summary>
        private bool _clientBytesChanged = false;
        /// <summary>
        /// Data on how the server should move the transform.
        /// </summary> 
        private GoalData _serverGoalData = default;
        /// <summary>
        /// Goals for how the client should modify the transform.
        /// </summary>
        private GoalData _clientGoalData = default;
        /// <summary>
        /// Move rates for how fast the transform should update on server.
        /// </summary>
        private RateData _serverRateData = default;
        /// <summary>
        /// Move rates for how fast the transform should update on client.
        /// </summary>
        private RateData _clientRateData = default;
        /// <summary>
        /// True if subscribed to TimeManager for ticks.
        /// </summary>
        private bool _subscribedToTicks = false;
        /// <summary>
        /// Last sent transform values. Can be used for client or server.
        /// </summary>
        private GoalData _lastTransformValues = default;

        /// <summary>
        /// How many ticks to use as a buffer for interpolation.
        /// </summary>
        private const uint INTERPOLATION = 2;

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

        public override void OnOwnershipServer(NetworkConnection newOwner)
        {
            base.OnOwnershipServer(newOwner);
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
            /* There is no reason to send data twice in the same frame,
             * nothing would have changed. */
            if (Time.frameCount == _lastTickFrame)
                return;
            _lastTickFrame = Time.frameCount;

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
         * the case then the packet can still be parsed out and recoveredbecause the updateflags
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
            if (forServer)
                _serverGoalData = new GoalData(0, transform.position, transform.rotation, transform.localScale);
            if (forClient)
                _clientGoalData = new GoalData(0, transform.position, transform.rotation, transform.localScale);
        }

        /// <summary>
        /// Serializes only changed data into writer.
        /// </summary>
        /// <param name="changed"></param>
        /// <param name="writer"></param>
        private void SerializeChanged(Changed changed, PooledWriter writer)
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

            //X
            if (ChangedContains(changed, Changed.X))
            {
                original = transform.position.x;
                compressed = original * multiplier;
                if (_compressSmall && Math.Abs(compressed) <= maxValue)
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
            if (ChangedContains(changed, Changed.Y))
            {
                original = transform.position.y;
                compressed = original * multiplier;
                if (_compressSmall && Math.Abs(compressed) <= maxValue)
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
            if (ChangedContains(changed, Changed.Z))
            {
                original = transform.position.z;
                compressed = original * multiplier;
                if (_compressSmall && Math.Abs(compressed) <= maxValue)
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
            if (ChangedContains(changed, Changed.Rotation))
            {
                updateFlags |= UpdateFlag.Rotation;
                writer.WriteQuaternion(transform.rotation);
            }

            //Scale.
            if (ChangedContains(changed, Changed.Scale))
            {
                updateFlags |= UpdateFlag.Scale;
                writer.WriteVector3(transform.localScale);
            }

            //Insert flags at start.
            writer.FastInsertByte((byte)updateFlags, startIndex);

            bool ChangedContains(Changed whole, Changed part)
            {
                return (whole & part) == part;
            }
        }

        /// <summary>
        /// Deerializes a received packet.
        /// </summary>
        private void DeserializePacket(ArraySegment<byte> data, ref GoalData goalData)
        {
            using (PooledReader r = ReaderPool.GetReader(data, base.NetworkManager))
            {
                UpdateFlag updateFlags = (UpdateFlag)r.ReadByte();

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


                //Rotation.
                if (UpdateFlagContains(updateFlags, UpdateFlag.Rotation))
                    goalData.Rotation = r.ReadQuaternion();

                //Scale.
                if (UpdateFlagContains(updateFlags, UpdateFlag.Scale))
                    goalData.Scale = r.ReadVector3();
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

            //Position.
            rate = rateData.Position;
            if (rate == -1f)
                transform.position = goalData.Position;
            else
                transform.position = Vector3.MoveTowards(transform.position, goalData.Position, rate * Time.deltaTime);
            //Rotation.
            rate = rateData.Rotation;
            if (rate == -1f)
                transform.rotation = goalData.Rotation;
            else
                transform.rotation = Quaternion.RotateTowards(transform.rotation, goalData.Rotation, rate * Time.deltaTime);
            //Scale.
            rate = rateData.Scale;
            if (rate == -1f)
                transform.localScale = goalData.Scale;
            else
                transform.localScale = Vector3.MoveTowards(transform.localScale, goalData.Scale, rate * Time.deltaTime);
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
                Changed changed = GetChanged(_lastTransformValues);

                //If no change.
                if (changed == Changed.Unset)
                {
                    //No changes since last reliable; transform is up to date.
                    if (_serverChangedSinceReliable == Changed.Unset)
                        return;

                    //Set changed to all changes over time and unset changes over time.
                    changed = _serverChangedSinceReliable;
                    _serverChangedSinceReliable = Changed.Unset;
                    channel = Channel.Reliable;
                }
                //There is change.
                else
                {
                    _serverChangedSinceReliable |= changed;
                }

                /* If here a send for transform values will occur. Update last values.
                 * Tick doesn't need to be set for whoever controls transform. */
                _lastTransformValues = new GoalData(0,
                    transform.position, transform.rotation, transform.localScale);

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
            Changed changed = GetChanged(_lastTransformValues);

            //If no change.
            if (changed == Changed.Unset)
            {
                //No changes since last reliable; transform is up to date.
                if (_clientChangedSinceReliable == Changed.Unset)
                    return;

                //Set changed to all changes over time and unset changes over time.
                changed = _clientChangedSinceReliable;
                _clientChangedSinceReliable = Changed.Unset;
                channel = Channel.Reliable;
            }
            //There is change.
            else
            {
                _clientChangedSinceReliable |= changed;
            }

            /* If here a send for transform values will occur. Update last values.
            * Tick doesn't need to be set for whoever controls transform. */
            _lastTransformValues = new GoalData(0,
                transform.position, transform.rotation, transform.localScale);

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
        private bool HasChanged(ref GoalData a, ref GoalData b)
        {
            return !(a.Position == b.Position && a.Rotation == b.Rotation && a.Scale == b.Scale);
        }
        /// <summary>
        /// Gets transform values that have changed against goalData.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Changed GetChanged(GoalData goalData)
        {
            return GetChanged(ref goalData.Position, ref goalData.Rotation, ref goalData.Scale);
        }
        /// <summary>
        /// Gets transform values that have changed against specified proprties.
        /// </summary>
        private Changed GetChanged(ref Vector3 lastPosition, ref Quaternion lastRotation, ref Vector3 lastScale)
        {
            Changed changed = Changed.Unset;

            Vector3 position = transform.position;
            if (position.x != lastPosition.x)
                changed |= Changed.X;
            if (position.y != lastPosition.y)
                changed |= Changed.Y;
            if (position.z != lastPosition.z)
                changed |= Changed.Z;

            Quaternion rotation = transform.rotation;
            if (rotation != lastRotation)
                changed |= Changed.Rotation;

            Vector3 scale = transform.localScale;
            if (scale != lastScale)
                changed |= Changed.Scale;

            return changed;
        }
        #endregion

        #region Rates.
        /// <summary>
        /// Sets move rates which will occur instantly.
        /// </summary>
        private void SetInstantRates(bool forServer, bool forClient)
        {
            if (forServer)
                _serverRateData = new RateData(-1f, -1f, -1f);
            if (forClient)
                _clientRateData = new RateData(-1f, -1f, -1f);
        }

        /// <summary>
        /// Sets move rates which will occur over time.
        /// </summary>
        private void SetCalculatedRates(uint lastTick, ref GoalData oldGoalData, ref GoalData goalData, bool forServer, Channel channel)
        {
            /* Only update rates if data has changed.
             * When data comes in reliably for eventual consistency
             * it's possible that it will be the same as the last
             * unreliable packet. When this happens no change has occurred
             * and the distance of change woudl also be 0; this prevents
             * the NT from moving. Only need to compare data if channel is reliable. */
            if (channel == Channel.Reliable && !HasChanged(ref oldGoalData, ref goalData))
                return;

            //Distance between properties.
            float distance;

            distance = Vector3.Distance(goalData.Position, oldGoalData.Position);
            //If distance teleports assume rest do.
            if (_enableTeleport && distance >= _teleportThreshold)
            {
                SetInstantRates(forServer, !forServer);
                return;
            }

            /* If last tick is not set then
             * use goalData tick minus INTERPOLATION.
             * This will make the calculations think
             * that the transform moved the distance over
             * INTERPOLATION ticks, which will reduce the speed
             * of movement.
             * 
             * For example:
             * If tickDifference is 2 as described and
             * TickDelta is 0.02f then TicksToTime
             * will return 0.04f. Then the calculations
             * will perform DISTANCE / TIME (0.04f), which
             * in result is a lower value than if only one
             * tick behind.
             * 
             * Such as, if 2 ticks is 0.04f time then 1 tick
             * must be 0.02f time. Lets say the distance if 
             * 1 unit.
             * 1u / 0.04f = 25rate.
             * 1u / 0.02f = 50rate.
             * As demonstrated 2 ticks will result in a slower rate.
             * This is done to generate a buffer. Later on
             * the transform might sit the expected ticks instead of
             * moving slow to start. This is still undecided. */
            if (lastTick == 0)
                lastTick = goalData.Tick - INTERPOLATION;

            //How much time has passed between last update and current.
            uint tickDifference = (goalData.Tick - lastTick);
            float timePassed = base.NetworkManager.TimeManager.TicksToTime(tickDifference);

            //Position distance already calculated.
            float positionRate = distance / timePassed;
            distance = Quaternion.Angle(oldGoalData.Rotation, goalData.Rotation);
            float rotationRate = distance / timePassed;
            distance = Vector3.Distance(oldGoalData.Scale, goalData.Scale);
            float scaleRate = distance / timePassed;

            //Update appropriate rate.
            if (forServer)
                _serverRateData = new RateData(positionRate, rotationRate, scaleRate);
            else
                _clientRateData = new RateData(positionRate, rotationRate, scaleRate);
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
            UpdateGoalData(data, ref _serverGoalData);

            //If server only teleport.
            if (!base.IsClient)
                SetInstantRates(true, false);
            //Otherwise use timed.
            else
                SetCalculatedRates(lastTick, ref oldGoalData, ref _serverGoalData, true, channel);

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
            UpdateGoalData(data, ref _clientGoalData);

            SetCalculatedRates(lastTick, ref oldGoalData, ref _clientGoalData, false, channel);
        }

        /// <summary>
        /// Updates a GoalData from packetData.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateGoalData(ArraySegment<byte> packetData, ref GoalData goalData)
        {
            DeserializePacket(packetData, ref goalData);
            goalData.Tick = base.TimeManager.LastPacketTick;
        }
    }


}