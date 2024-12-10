// #if UNITY_EDITOR || DEVELOPMENT_BUILD
// #define DEVELOPMENT
// #endif
// using FishNet.Connection;
// using FishNet.Managing.Logging;
// using FishNet.Managing.Server;
// using FishNet.Object;
// using FishNet.Serializing;
// using FishNet.Transporting;
// using GameKit.Dependencies.Utilities;
// using System;
// using FishNet.Component.Ownership;
// using FishNet.Managing.Timing;
// using FishNet.Object.Prediction;
// using FishNet.Utility.Template;
// using UnityEngine;
// using UnityEngine.Scripting;
//
// // ReSharper disable CompareOfFloatsByEqualityOperator
//
// namespace FishNet.Component.Transforming
// {
//     [DisallowMultipleComponent]
//     [AddComponentMenu("FishNet/Component/NetworkTransform Pro")]
//     public sealed class NetworkTransformPro : TickNetworkBehaviour
//     {
//         #region Types.
//         /// <summary>
//         /// Axes to snap of properties.
//         /// </summary>
//         [Flags]
//         public enum SnappedAxes : uint
//         {
//             Unset = 0,
//             X = (1 << 0),
//             Y = (1 << 1),
//             Z = (1 << 2),
//             Everything = Enums.SHIFT_EVERYTHING_UINT,
//         }
//
//         public struct TransformData
//         {
//             /// <summary>
//             /// Changes which populated this data.
//             /// </summary>
//             public ChangedDelta Changed;
//             /// <summary>
//             /// Tick this arrived at or was created for.
//             /// </summary>
//             public uint ReceivedTick;
//             /// <summary>
//             /// LocalTick this data was created on.
//             /// </summary>
//             public uint LocalTick;
//             /// <summary>
//             /// Goal properties of the transform.
//             /// </summary>
//             public TransformProperties Properties;
//             /// <summary>
//             /// Parent of the transform.
//             /// </summary>
//             public NetworkBehaviour Parent;
//             /// <summary>
//             /// Channel this data arrived on.
//             /// </summary>
//             public Channel Channel;
//             /// <summary>
//             /// True if data has been set.
//             /// </summary>
//             public bool IsValid => Properties.IsValid;
//
//             public TransformData(ChangedDelta changed, uint localTick, uint receivedTick, TransformProperties properties, NetworkBehaviour parent, Channel channel)
//             {
//                 Changed = changed;
//                 LocalTick = localTick;
//                 ReceivedTick = receivedTick;
//                 Properties = properties;
//                 Parent = parent;
//                 Channel = channel;
//             }
//
//             public void ResetState()
//             {
//                 Changed = ChangedDelta.Unset;
//                 ReceivedTick = TimeManager.UNSET_TICK;
//                 Properties.ResetState();
//                 Parent = null;
//                 Channel = Channel.Unreliable;
//             }
//         }
//
//         /// <summary>
//         /// Written as a byte unless extended flag is set, then written as ushort.
//         /// </summary>
//         [Flags]
//         public enum ChangedDelta : uint
//         {
//             Unset = 0,
//             Position = (1 << 0),
//             Rotation = (1 << 1),
//             Scale = (1 << 2),
//             Teleport = (1 << 3),
//             Parent = (1 << 4),
//             IsDelta = (1 << 7),
//             Everything = Enums.SHIFT_EVERYTHING_UINT,
//         }
//
//         /// <summary>
//         /// Used to queue datas to move towards.
//         /// Also relays data to other clients if the server.
//         /// </summary>
//         public class TransformDataQueue : IResettable
//         {
//             /// <summary>
//             /// TransformProperties to move towards.
//             /// </summary>
//             public readonly BasicQueue<TransformData> TransformDatas = new();
//             /// <summary>
//             /// Last properties received from the server or client.
//             /// This will automatically have deltas completed.
//             /// </summary>
//             public TransformData LastReceivedCompleteTransformData;
//             /// <summary>
//             /// Current transform properties to use for movement.
//             /// </summary>
//             public TransformProperties MoveProperties;
//             /// <summary>
//             /// Current move rates.
//             /// </summary>
//             public readonly MoveRate MoveRates = new();
//             /// <summary>
//             /// Datas to forward to clients. This is only used by the server.
//             /// </summary>
//             public BasicQueue<TransformData> TransformDataToRelay = new();
//
//             public void ResetState()
//             {
//                 TransformDatas.Clear();
//                 LastReceivedCompleteTransformData.ResetState();
//                 MoveProperties.ResetState();
//                 MoveRates.ResetState();
//                 TransformDataToRelay.Clear();
//             }
//
//             public void InitializeState() { }
//         }
//
//         public class MoveRate : IResettable
//         {
//             /// <summary>
//             /// Rate for position after smart calculations.
//             /// </summary>
//             public float Position;
//             /// <summary>
//             /// Rate for rotation after smart calculations.
//             /// </summary>
//             public float Rotation;
//             /// <summary>
//             /// Rate for scale after smart calculations.
//             /// </summary>
//             public float Scale;
//             /// <summary>
//             /// Number of ticks the rates are calculated for.
//             /// If TickSpan is 2 then the rates are calculated under the assumption the transform changed over 2 ticks.
//             /// </summary>
//             public uint TickSpan;
//             /// <summary>
//             /// Time remaining until transform is expected to reach it's goal.
//             /// </summary>
//             internal float TimeRemaining;
//
//             [Preserve]
//             public MoveRate() { }
//
//             public void Update(MoveRate rd)
//             {
//                 Update(rd.Position, rd.Rotation, rd.Scale, rd.TickSpan, rd.TimeRemaining);
//             }
//
//             /// <summary>
//             /// Updates all rates.
//             /// </summary>
//             /// <param name="rate"></param>
//             public void Update(float rate, uint tickSpan, float timeRemaining) => Update(rate, rate, rate, tickSpan, timeRemaining);
//
//             /// <summary>
//             /// Updates rates.
//             /// </summary>
//             public void Update(float position, float rotation, float scale, uint tickSpan, float timeRemaining)
//             {
//                 Position = position;
//                 Rotation = rotation;
//                 Scale = scale;
//                 TickSpan = tickSpan;
//                 TimeRemaining = timeRemaining;
//             }
//
//             public void ResetState()
//             {
//                 Position = 0f;
//                 Rotation = 0f;
//                 Scale = 0f;
//                 TickSpan = 0;
//                 TimeRemaining = 0f;
//             }
//
//             public void InitializeState() { }
//         }
//         #endregion
//
//         #region Serialized.
//         /// <summary>
//         /// How many ticks to interpolate. This is how many ticks the transform will wait after receiving initial data before beginning to move.
//         /// </summary>
//         [Tooltip("How many ticks to interpolate. This is how many ticks the transform will wait after receiving initial data before beginning to move.")]
//         [Range(1, MAX_INTERPOLATION)]
//         [SerializeField]
//         private ushort _interpolation = 2;
//         /// <summary>
//         /// How many ticks to extrapolate. This is how many ticks the transform will move in the same direction without receiving data. Using large values is recommended to save bandwidth.
//         /// </summary>
//         [Tooltip("How many ticks to extrapolate. This is how many ticks the transform will move in the same direction without receiving data. Using large values is recommended to save bandwidth.")]
//         [Range(0, MAX_EXTRAPOLATION)]
//         [SerializeField]
//         private ushort _extrapolation = MAX_EXTRAPOLATION;
//         /// <summary>
//         /// True to enable teleport threshhold.
//         /// </summary>
//         [Tooltip("True to enable teleport threshhold.")]
//         [SerializeField]
//         private bool _enableTeleport;
//         /// <summary>
//         /// How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.
//         /// </summary>
//         [Tooltip("How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.")]
//         [Range(0f, float.MaxValue)]
//         [SerializeField]
//         private float _teleportThreshold = 1f;
//         /// <summary>
//         /// True if owner controls how the object is synchronized.
//         /// </summary>
//         [Tooltip("True if owner controls how the object is synchronized.")]
//         [SerializeField]
//         private bool _clientAuthoritative = true;
//         /// <summary>
//         /// True to synchronize movements on server to owner when not using client authoritative movement.
//         /// </summary>
//         [Tooltip("True to synchronize movements on server to owner when not using client authoritative movement.")]
//         [SerializeField]
//         private bool _sendToOwner = true;
//
//         /// <summary>
//         /// Transform properties to synchronize.
//         /// </summary>
//         [Tooltip("Transform properties to synchronize.")]
//         [SerializeField]
//         private TransformPropertiesFlag _synchronizedProperties = TransformPropertiesFlag.Everything;
//
//         /// <summary>
//         /// Axes to snap on position.
//         /// </summary>
//         [Tooltip("Axes to snap on position.")]
//         [SerializeField]
//         private SnappedAxes _positionSnapping = SnappedAxes.Unset;
//
//         /// <summary>
//         /// Axes to snap on rotation.
//         /// </summary>
//         [Tooltip("Axes to snap on rotation.")]
//         [SerializeField]
//         private SnappedAxes _rotationSnapping = SnappedAxes.Unset;
//
//         /// <summary>
//         /// Axes to snap on scale.
//         /// </summary>
//         [Tooltip("Axes to snap on scale.")]
//         [SerializeField]
//         private SnappedAxes _scaleSnapping = SnappedAxes.Unset;
//         #endregion
//
//         #region Private.
//         /// <summary>
//         /// Last tick an ObserverRpc passed checks.
//         /// </summary>
//         private uint _lastObserversRpcTick;
//         /// <summary>
//         /// Last tick a ServerRpc passed checks.
//         /// </summary>
//         private uint _lastServerRpcTick;
//         /// <summary>
//         /// True if the transform has changed since it started.
//         /// </summary>
//         private bool _changedSinceStart;
//         /// <summary>
//         /// Writers for changed data.
//         /// </summary>
//         private PooledWriter _toClientChangedWriter;
//         /// <summary>
//         /// Last data sent. This can be the server relaying data from an owner, server sending authoritative data, or client sending authoritative data to server.
//         /// </summary>
//         private TransformData _lastSentTransformData;
//         /// <summary>
//         /// If not unset a force send will occur on or after this tick.
//         /// </summary>
//         private uint _forceSendTick = TimeManager.UNSET_TICK;
//         /// <summary>
//         /// When true teleport will be sent with the next changed data.
//         /// </summary>
//         private bool _teleport;
//         /// <summary>
//         /// Cached transform
//         /// </summary>
//         private Transform _cachedTransform;
//         /// <summary>
//         /// Cached network TickDelta.
//         /// </summary>
//         private float _cachedTickDelta;
//         /// <summary>
//         /// PredictedOwner on the NetworkObject.
//         /// </summary>
//         private PredictedOwner _cachedPredictedOwner;
//         /// <summary>
//         /// Cached TimeManager reference for performance.
//         /// </summary>
//         private TimeManager _timeManager;
//         /// <summary>
//         /// All MoveDatas received.
//         /// </summary>
//         private TransformDataQueue _moveDatas;
//         /// <summary>
//         /// Cached synchronize property.
//         /// </summary>
//         private bool _synchronizePosition;
//         /// <summary>
//         /// Cached synchronize property.
//         /// </summary>
//         private bool _synchronizeRotation;
//         /// <summary>
//         /// Cached synchronize property.
//         /// </summary>
//         private bool _synchronizeScale;
//         #endregion
//
//         #region Const.
//         /// <summary>
//         /// Maximum possible interpolation value.
//         /// </summary>
//         public const ushort MAX_INTERPOLATION = 30;
//         /// <summary>
//         /// Maximum possible interpolation value.
//         /// </summary>
//         public const ushort MAX_EXTRAPOLATION = 1280;
//         #endregion
//
//         private void Awake()
//         {
//             InitializeState();
//         }
//
//         private void OnDestroy()
//         {
//             ResetState();
//         }
//
//         public override void OnStartNetwork()
//         {
//             _timeManager = base.TimeManager;
//             _cachedTickDelta = (float)_timeManager.TickDelta;
//             _cachedPredictedOwner = base.NetworkObject.PredictedOwner;
//         }
//
//         public override void OnStartServer()
//         {
//             SetDefaultGoalData();
//         }
//
//         public override void OnSpawnServer(NetworkConnection connection)
//         {
//             base.OnSpawnServer(connection);
//             /* If not on the root then the initial properties may need to be synchronized
//              * since the spawn message only sends root information. If initial
//              * properties have changed update spawning connection. */
//             if (base.NetworkObject.gameObject != gameObject && _changedSinceStart)
//             {
//                 //Send latest.
//                 PooledWriter writer = SerializeCompleteTransformData(ChangedDelta.Everything, default, new(_cachedTransform));
//                 TargetUpdateTransform(connection, writer.GetArraySegment(), Channel.Reliable);
//                 writer.Store();
//             }
//         }
//
//         public override void OnStartClient()
//         {
//             SetDefaultGoalData();
//         }
//
//         public override void OnOwnershipServer(NetworkConnection prevOwner)
//         {
//             //Reset last tick since each client sends their own ticks.
//             _lastServerRpcTick = 0;
//             ResetDatas_OwnershipChange(prevOwner, true);
//         }
//
//         public override void OnOwnershipClient(NetworkConnection prevOwner)
//         {
//             //Not new owner.
//             if (!base.IsOwner)
//             {
//                 /* If client authoritative and ownership was lost
//                  * then default goals must be set to force the
//                  * object to it's last transform. */
//                 if (_clientAuthoritative)
//                     SetDefaultGoalData();
//             }
//
//             ResetDatas_OwnershipChange(prevOwner, false);
//         }
//
//         /// <summary>
//         /// Tries to clear the GoalDatas queue during an ownership change.
//         /// </summary>
//         private void ResetDatas_OwnershipChange(NetworkConnection prevOwner, bool asServer)
//         {
//             if (_clientAuthoritative)
//             {
//                 //If not server
//                 if (!asServer)
//                 {
//                     //If previous or new owner then clear datas.
//                     if (base.IsOwner || prevOwner.IsLocalClient)
//                     {
//                         _lastObserversRpcTick = TimeManager.UNSET_TICK;
//                         _lastSentTransformData.ResetState();
//                         _moveDatas.ResetState();
//                     }
//                 }
//                 //as Server.
//                 else
//                 {
//                     _moveDatas.ResetState();
//                     _lastServerRpcTick = TimeManager.UNSET_TICK;
//                 }
//             }
//             /* Server authoritative never clears because the
//              * clients do not control this object thus should always
//              * follow the queue. */
//         }
//
//         protected override void TimeManager_OnUpdate()
//         {
//             MoveToGoal(Time.deltaTime);
//         }
//
//         /// <summary>
//         /// Called when a tick occurs.
//         /// </summary>
//         protected override void TimeManager_OnPostTick()
//         {
//             PrepareForceSend(force: false);
//
//             /* If client is not initialized then
//              * call a move to target on post tick to ensure
//              * anything with instant rates gets moved. */
//             if (base.IsServerInitialized && !base.IsClientInitialized)
//                 MoveToGoal((float)_timeManager.TickDelta);
//
//             SendToClientsAsController();
//             SendToClientsAsRelay();
//             SendToServer();
//         }
//
//         /// <summary>
//         /// Returns if server or client can send current transform updates.
//         /// </summary>
//         /// <returns></returns>
//         private bool CanSendAsController(bool asServer)
//         {
//             if (asServer)
//             {
//                 //False if server is not active.
//                 if (!base.IsServerInitialized)
//                     return false;
//
//                 /* When clientHost owns the object the data
//                  * is sent as if the server owns the object, since
//                  * they are one. */
//                 if (base.Owner.IsLocalClient)
//                     return true;
//                 //False if client authoritative and there is an owner.
//                 if (_clientAuthoritative && base.Owner.IsValid)
//                     return false;
//
//                 return true;
//             }
//             //Not asServer.
//             else
//             {
//                 //False if client is not active.
//                 if (!base.IsClientInitialized)
//                     return false;
//                 //False if not client authoritative.
//                 if (!_clientAuthoritative)
//                     return false;
//                 /* False if server and owner is local client.
//                  * In this scenario the server sends as the
//                  * controller. */
//                 if (base.IsServerInitialized && base.Owner.IsLocalClient)
//                     return false;
//                 //False if not owner, or taking owner.
//                 PredictedOwner po = base.NetworkObject.PredictedOwner;
//                 if (!base.IsOwner && (po != null && !po.TakingOwnership))
//                     return false;
//
//                 return true;
//             }
//         }
//
//         /// <summary>
//         /// Returns if the server can send data received from an owner, to other clients.
//         /// </summary>
//         /// <returns></returns>
//         private bool CanSendToClientsAsRelay()
//         {
//             //Server not started or not client auth, thus should be no data from clients.
//             if (!base.IsServerInitialized || !_clientAuthoritative)
//                 return false;
//             //No owner, cannot relay.
//             if (!base.Owner.IsValid)
//                 return false;
//             //Owner is self, self sends as controller.
//             if (base.Owner.IsLocalClient)
//                 return false;
//
//             return true;
//         }
//
//         /// <summary>
//         /// Returns true if the transform can move to goals.
//         /// </summary>
//         /// <returns></returns>
//         private bool CanMoveToGoals()
//         {
//             //If client auth and the owner don't move towards target.
//             if (_clientAuthoritative)
//             {
//                 PredictedOwner po = _cachedPredictedOwner;
//                 if (base.IsOwner || (po != null && po.TakingOwnership))
//                     return false;
//             }
//             else
//             {
//                 //If not client authoritative, is owner, and don't sync to owner.
//                 if (base.IsOwner && !_sendToOwner)
//                     return false;
//             }
//
//             //True if not client controlled.
//             bool controlledByClient = (_clientAuthoritative && base.Owner.IsActive);
//             //If not controlled by client and is server then no reason to move.
//             if (!controlledByClient && base.IsServerInitialized)
//                 return false;
//
//             return true;
//         }
//
//         /// <summary>
//         /// Prepares for a force send if needed.
//         /// </summary>
//         private void PrepareForceSend(bool force)
//         {
//             if (!force && (_forceSendTick != TimeManager.UNSET_TICK))
//                 return;
//
//             _forceSendTick = TimeManager.UNSET_TICK;
//
//             if (_authoritativeClientData.Writer != null)
//                 _authoritativeClientData.SendReliably();
//         }
//
//         /// <summary>
//         /// Resets last sent information to force a resend of current values after a number of ticks.
//         /// </summary>
//         /// <param name="sendPending">True to force send as soon as possible if there is a previous pending force send. Next force send will still be queued.</param>
//         public void ForceSend(uint ticks, bool sendPending)
//         {
//             /* If there is a pending delayed force send then queue it
//              * immediately and set a new delay tick. */
//             if (sendPending && _forceSendTick != TimeManager.UNSET_TICK)
//                 ForceSend(force: true);
//             _forceSendTick = _timeManager.LocalTick + ticks;
//         }
//
//         /// <summary>
//         /// Resets last sent information to force a resend of current values.
//         /// </summary>
//         public void ForceSend(bool force)
//         {
//             PrepareForceSend(force);
//         }
//
//         /// <summary>
//         /// Creates goal data using current position.
//         /// </summary>
//         private void SetDefaultGoalData()
//         {
//             _teleport = false;
//             _moveDatas.ResetState();
//         }
//
//         /// <summary>
//         /// Serializes only changed data and returns the writer. 
//         /// </summary>
//         private PooledWriter SerializeCompleteTransformData(ChangedDelta changed, TransformProperties lastProperties, TransformProperties currentProperties)
//         {
//             PooledWriter writer = WriterPool.Retrieve();
//             SerializeCompleteTransformData(writer, changed, lastProperties, currentProperties);
//
//             return writer;
//         }
//
//         /// <summary>
//         /// Serializes only changed data using last properties as a delta when possible.
//         /// </summary>
//         private void SerializeCompleteTransformData(PooledWriter writer, ChangedDelta changed, TransformProperties lastProperties, TransformProperties currentProperties)
//         {
//             //Set isDelta if able to write as delta.
//             if (lastProperties.IsValid)
//                 changed |= ChangedDelta.IsDelta;
//
//             writer.WriteUInt8Unpacked((byte)changed);
//
//             //If last sent values are known then we can send deltas of last changed.
//             if (ChangedDeltaContains(changed, ChangedDelta.IsDelta))
//             {
//                 writer.WriteDeltaTransformProperties(lastProperties, currentProperties);
//
//                 // /* Parent has to always be sent in case delta drops.
//                 //  * For example: if parent wasn't sent because it was the same
//                 //  * as the last send, but last send dropped, then no parent would send.
//                 //  * In result the client would incorrectly receive that there is no parent. */
//                 // if (ChangedContains(changed, ChangedDelta.Parent))
//                 //     writer.WriteNetworkBehaviour(_parentBehaviour);
//             }
//             //Cannot write deltas, have to full all values.
//             else
//             {
//                 writer.WriteTransformProperties(currentProperties);
//                 //writer.WriteNetworkBehaviour(_parentBehaviour);
//             }
//         }
//
//         /// <summary>
//         /// Deserializes only changes using last properties as delta when possible. Returns reader used.
//         /// </summary>
//         private PooledReader DeserializeCompleteTransformData(ArraySegment<byte> data, TransformProperties lastProperties, uint receivedLocalTick, Channel channel, out TransformData result)
//         {
//             PooledReader reader = ReaderPool.Retrieve(data, base.NetworkManager);
//             result = DeserializeCompleteTransformData(reader, lastProperties, receivedLocalTick, channel);
//             return reader;
//         }
//
//         /// <summary>
//         /// Deserializes only changes using last properties as delta when possible.
//         /// </summary>
//         private TransformData DeserializeCompleteTransformData(ArraySegment<byte> data, TransformProperties lastProperties, uint receivedLocalTick, Channel channel)
//         {
//             PooledReader reader = ReaderPool.Retrieve(data, base.NetworkManager);
//             TransformData result = DeserializeCompleteTransformData(reader, lastProperties, receivedLocalTick, channel);
//             ReaderPool.Store(reader);
//
//             return result;
//         }
//
//         /// <summary>
//         /// Deserializes changed data using last properties as a delta when possible.
//         /// </summary>
//         private TransformData DeserializeCompleteTransformData(PooledReader reader, TransformProperties lastProperties, uint receivedLocalTick, Channel channel)
//         {
//             ChangedDelta changed = (ChangedDelta)reader.ReadUInt8Unpacked();
//
//             TransformData result = new();
//
//             //If last reader values are known then we can read deltas of last changed.
//             if (ChangedDeltaContains(changed, ChangedDelta.IsDelta))
//             {
//                 /* If last properties are not known then use current transform as delta.
//                  * This shouldn't ever happen, but playing it safe. This at the very least will
//                  * prevent the object from ending up in invalid world space. */
//                 if (!lastProperties.IsValid)
//                     lastProperties = new(_cachedTransform);
//
//                 result.Properties = reader.ReadDeltaTransformProperties(lastProperties);
//
//                 // if (ChangedContains(changed, ChangedDelta.Parent))
//                 //     result.Parent = reader.ReadNetworkBehaviour();
//             }
//             //Was not sent as delta.
//             else
//             {
//                 result.Properties = reader.ReadTransformProperties();
//                 //result.Parent = reader.ReadNetworkBehaviour();
//             }
//
//             return result;
//         }
//
//         /// <summary>
//         /// Moves to a GoalData. Automatically determins if to use data from server or client.
//         /// </summary>
//         private void MoveToGoal(float delta)
//         {
//             TransformDataQueue moveDatas = _moveDatas;
//             TransformProperties tp = moveDatas.MoveProperties;
//
//             if (!tp.IsValid)
//                 return;
//
//             if (!CanMoveToGoals())
//                 return;
//
//             MoveRate rd = moveDatas.MoveRates;
//
//             float multiplier = 1f;
//             int queueCount = moveDatas.TransformDatas.Count;
//             //Increase move rate slightly if over queue count.
//             if (queueCount > (_interpolation + 1))
//                 multiplier += 0.05f;
//
//             float deltaWithMultiplier = (delta * multiplier);
//
//             Transform t = _cachedTransform;
//
//             //Position.
//             if (_synchronizePosition)
//                 t.localPosition = Vector3.MoveTowards(t.localPosition, tp.Position, rd.Position * deltaWithMultiplier);
//             //Rotation.
//             if (_synchronizeRotation)
//                 t.localRotation = Quaternion.RotateTowards(t.localRotation, tp.Rotation, rd.Rotation * deltaWithMultiplier);
//             //Scale.
//             if (_synchronizeScale)
//                 t.localScale = Vector3.MoveTowards(t.localScale, tp.Scale, rd.Scale * deltaWithMultiplier);
//
//             float timeRemaining = (rd.TimeRemaining - deltaWithMultiplier);
//             if (timeRemaining < -delta)
//                 timeRemaining = -delta;
//             rd.TimeRemaining = timeRemaining;
//
//             if (rd.TimeRemaining <= 0f)
//             {
//                 float leftOver = Mathf.Abs(rd.TimeRemaining);
//                 //If more in buffer then run next buffer.
//                 if (queueCount > 0)
//                 {
//                     SetNextMoveData();
//                     if (leftOver > 0f)
//                         MoveToGoal(leftOver);
//                 }
//             }
//         }
//
//         /// <summary>
//         /// Sends transform data to clients if needed.
//         /// </summary>
//         private void SendToClientsAsController()
//         {
//             if (!CanSendAsController(asServer: true))
//                 return;
//
//             Channel channel = Channel.Unreliable;
//
//             TransformData lastSentData = _lastSentTransformData;
//             ChangedDelta changed = GetChanged(lastSentData);
//
//             //If no change.
//             if (changed == ChangedDelta.Unset)
//             {
//                 //Last sent was reliable so there is no need to continue.
//                 if (lastSentData.Channel == Channel.Reliable)
//                     return;
//
//                 /* If here there is no change but data has not been sent reliably yet.
//                  * Set channel to reliable and send the same information again to
//                  * ensure others get the latest information. */
//                 channel = Channel.Reliable;
//                 //Also make changed everything to ensure all properties are up to date.
//                 changed = ChangedDelta.Everything;
//             }
//
//             PooledWriter writer = _toClientChangedWriter;
//             writer.ResetState();
//
//             _changedSinceStart = true;
//
//             uint localTick = _timeManager.LocalTick;
//             TransformData currentData = new(changed, localTick, receivedTick: localTick, new(_cachedTransform), parent: null, channel);
//             //CompleteTransformData currentData = new(changed, _timeManager.LocalTick, new(_cachedTransform), ParentBehaviour, channel);
//             SerializeCompleteTransformData(writer, changed, lastSentData.Properties, currentData.Properties);
//             _lastSentTransformData = currentData;
//
//             ObserversUpdateClientAuthoritativeTransform(writer.GetArraySegment(), channel);
//         }
//
//         /// <summary>
//         /// Relays data from authoritative client to other clients.
//         /// </summary>
//         private void SendToClientsAsRelay()
//         {
//             if (!CanSendToClientsAsRelay())
//                 return;
//
//             BasicQueue<TransformData> datasToRelay = _moveDatas.TransformDataToRelay;
//             /* No data available to relay. Make sure the last data sent as
//              * reliable so spectators get the latest values. */
//             if (datasToRelay.Count == 0)
//             {
//                 //Already sent as a reliable.
//                 if (_lastSentTransformData.Channel == Channel.Reliable)
//                     return;
//
//                 /* If ticks have passed beyond interpolation then force
//                  * to send reliably.
//                  * Allow up to interpolation and an extra 2 ticks. */
//                 uint tickPassedToSendReliably = (_lastSentTransformData.LocalTick + 2 + _interpolation);
//                 //Not enough time has passed to send reliably.
//                 if (_timeManager.LocalTick < tickPassedToSendReliably)
//                     return;
//
//                 //Send reliably the last data.
//                 _lastSentTransformData.Channel = Channel.Reliable;
//                 PooledWriter writer = _toClientChangedWriter;
//                 writer.ResetState();
//                 
//                 
//                 ObserversUpdateClientAuthoritativeTransform( _authoritativeClientData.Writer.GetArraySegment(), _authoritativeClientData.Channel);
//             }
//
//             /* Maximum number of sends allowed per tick.
//              *
//              * This has several benefits.
//              * - Clients can get multiple datas at once in instances
//              * of latency spikes; this will significantly reduce the
//              * chance of a client waiting on new data since the burst
//              * will be queued.
//              *
//              * - Having a limit on sends per tick also prevents owning
//              * clients from exploiting the server; without a cap the server
//              * would push data out fast as the client sent it in, and that of course
//              * would not be great. */
//             const int maxSend = 2;
//             /* Maximum number of datas allowed in the queue. Excessive entries will be dropped.
//              * This is used to prevent stackoverflow attacks as well prevent some speed
//              * hacking. Though, the owning client could just as easily modify other values
//              * in their game to move faster without sending extra datas. */
//             const int maxInQueue = 4;
//
//             while (datasToRelay.Count > maxInQueue)
//                 datasToRelay.Dequeue();
//
//             //Channel to send rpc on.
//             Channel channel = Channel.Unreliable;
//
//             /* If the channel is not reliable
//             /* If there is not new data yet and the last received was not reliable
//              * then a packet maybe did not arrive when expected. See if we need
//              * to force a reliable with the last data based on ticks passed since
//              * last update.*/
//             if (!_authoritativeClientData.HasData && _authoritativeClientData.Channel != Channel.Reliable && _authoritativeClientData.Writer != null)
//             {
//                 /* If ticks have passed beyond interpolation then force
//                  * to send reliably. */
//                 uint maxPassedTicks = (uint)(1 + _interpolation + _extrapolation);
//                 uint localTick = _timeManager.LocalTick;
//                 if ((localTick - _authoritativeClientData.LocalTick) > maxPassedTicks)
//                     _authoritativeClientData.SendReliably();
//                 //Not enough time to send reliably, just don't need update.
//                 else
//                     return;
//             }
//
//             if (_authoritativeClientData.HasData)
//             {
//                 _changedSinceStart = true;
//                 //Resend data from clients.
//                 ObserversUpdateClientAuthoritativeTransform(_authoritativeClientData.Writer.GetArraySegment(), _authoritativeClientData.Channel);
//                 //Now being sent data can unset.
//                 _authoritativeClientData.HasData = false;
//             }
//         }
//
//         /// <summary>
//         /// Sends transform data to server if needed.
//         /// </summary>
//         private void SendToServer()
//         {
//             if (!CanControl(asServer: false))
//                 return;
//
//             //Channel to send on.
//             Channel channel = Channel.Unreliable;
//             //Values changed since last check.
//             ChangedDelta changed = GetChanged(lastSentTransformData);
//
//             //If no change.
//             if (changed == ChangedDelta.Unset)
//             {
//                 //No changes since last reliable; transform is up to date.
//                 if (_clientChangedSinceReliable == ChangedDelta.Unset)
//                     return;
//
//                 //Set changed to all changes over time and unset changes over time.
//                 changed = _clientChangedSinceReliable;
//                 _clientChangedSinceReliable = ChangedDelta.Unset;
//                 channel = Channel.Reliable;
//             }
//             //There is change.
//             else
//             {
//                 _clientChangedSinceReliable |= changed;
//             }
//
//             /* If here a send for transform values will occur. Update last values.
//              * Tick doesn't need to be set for whoever controls transform. */
//             Transform t = _cachedTransform;
//             lastSentTransformData.Update(0, t.localPosition, t.localRotation, t.localScale, t.localPosition, ParentBehaviour);
//
//             //Send latest.
//             PooledWriter writer = WriterPool.Retrieve();
//             SerializeTransform(changed, writer);
//             ServerUpdateTransform(writer.GetArraySegment(), channel);
//
//             writer.Store();
//         }
//
//         #region GetChanged.
//         /// <summary>
//         /// Gets transform values that have changed against goalData.
//         /// </summary>
//         private ChangedDelta GetChanged(TransformData previousData)
//         {
//             //If default return full changed.
//             if (!previousData.IsValid)
//                 return ChangedDelta.Everything;
//
//             TransformProperties tp = previousData.Properties;
//             return GetChanged(tp.Position, tp.Rotation, tp.Scale, previousData.Parent);
//         }
//
//         /// <summary>
//         /// Gets transform values that have changed against specified proprties.
//         /// </summary>
//         private ChangedDelta GetChanged(Vector3 lastPosition, Quaternion lastRotation, Vector3 lastScale, NetworkBehaviour lastParentBehaviour)
//         {
//             ChangedDelta changed = ChangedDelta.Unset;
//             Transform t = _cachedTransform;
//
//             //Position check.
//             if (_synchronizePosition)
//             {
//                 float positionVariance = (0.001f * 0.001f);
//                 if (Vector3.SqrMagnitude(lastPosition - t.localPosition) >= positionVariance)
//                     changed |= ChangedDelta.Position;
//             }
//
//             //Rotation check.
//             if (_synchronizeRotation)
//             {
//                 if (!lastRotation.Matches(t.rotation, precise: true))
//                     changed |= ChangedDelta.Rotation;
//             }
//
//             //Scale check.
//             if (_synchronizeScale)
//             {
//                 float scaleVariance = (0.001f * 0.001f);
//                 if (Vector3.SqrMagnitude(lastScale - t.localScale) >= scaleVariance)
//                     changed |= ChangedDelta.Scale;
//             }
//
//             // if (changed != ChangedDelta.Unset && t.parent != null)
//             //     changed |= ChangedDelta.Parent;
//
//             return changed;
//         }
//         #endregion
//
//         #region Rates.
//         /// <summary>
//         /// Snaps transform properties using snapping settings.
//         /// This should be called when setting a CompleteTransformProperties as current.
//         /// </summary>
//         private void SnapToTransformProperties(TransformData data, bool force = false)
//         {
//             Transform t = _cachedTransform;
//             TransformProperties properties = data.Properties;
//
//             //Parent must be set first since snapping will be in local space.
//             if (data.Parent != null)
//                 base.NetworkObject.SetParent(data.Parent);
//
//             //Position.
//             if (_synchronizePosition)
//                 t.localPosition = GetSnappedVector3(properties.Position, t.localPosition, _positionSnapping);
//
//             //Rotation.
//             if (_synchronizeRotation)
//                 t.localEulerAngles = GetSnappedVector3(properties.Rotation.eulerAngles, t.localEulerAngles, _rotationSnapping);
//
//             //Scale.
//             if (_synchronizeScale)
//                 t.localScale = GetSnappedVector3(properties.Scale, t.localScale, _scaleSnapping);
//
//             Vector3 GetSnappedVector3(Vector3 dataValue, Vector3 currentTransformValue, SnappedAxes snappedAxes)
//             {
//                 Vector3 result = dataValue;
//
//                 if (force || SnappedAxesContains(snappedAxes, SnappedAxes.X))
//                     result.x = currentTransformValue.x;
//
//                 if (force || SnappedAxesContains(snappedAxes, SnappedAxes.Y))
//                     result.y = currentTransformValue.y;
//
//                 if (force || SnappedAxesContains(snappedAxes, SnappedAxes.Z))
//                     result.z = currentTransformValue.z;
//
//                 return result;
//             }
//         }
//
//         /// <summary>
//         /// Sets move rates which will occur instantly.
//         /// </summary>
//         private void SetInstantRates(MoveRate rd, uint tickDifference, float timeRemaining)
//         {
//             rd.Update(MoveRatesCls.INSTANT_VALUE, tickDifference, timeRemaining);
//         }
//
//         /// <summary>
//         /// Sets move rates which will occur over time.
//         /// </summary>
//         private void SetCalculatedRates(MoveRate rd, TransformData previousData, TransformData nextData)
//         {
//             //Previous data is not set, use transform current.
//             if (!previousData.IsValid)
//             {
//                 //Creating locals to make code easier to read.
//                 uint receivedTick = (nextData.ReceivedTick - 1);
//                 uint localTick = (nextData.LocalTick - 1);
//                 TransformProperties tp = new(_cachedTransform);
//
//                 previousData = new(ChangedDelta.Everything, localTick, receivedTick, tp, base.NetworkObject.CurrentParentNetworkBehaviour, Channel.Unreliable);
//             }
//
//             /* If nothing was changed then the rates remain the same.
//              * But the time remaining does need to be reset. */
//             if (previousData.Changed == nextData.Changed)
//             {
//                 rd.TimeRemaining = (float)base.TimeManager.TickDelta;
//                 return;
//             }
//
//             uint tickDifference = GetTickDifference(previousData.ReceivedTick, nextData.ReceivedTick, minimum: 1);
//             float timePassed = (_cachedTickDelta * tickDifference);
//
//             //Distance between properties.
//             float positionRate = 0f;
//             float rotationRate = 0f;
//             float scaleRate = 0f;
//
//             ChangedDelta nextChanged = nextData.Changed;
//             //Quick exit/check for teleport.
//             if (ChangedDeltaContains(nextChanged, ChangedDelta.Teleport))
//             {
//                 LSetInstantRates();
//                 return;
//             }
//
//             TransformProperties prevProperties = previousData.Properties;
//             TransformProperties nextProperties = nextData.Properties;
//
//             //Position.
//             if (_synchronizePosition && ChangedDeltaContains(nextChanged, ChangedDelta.Position))
//             {
//                 positionRate = Vector3.Distance(prevProperties.Position, nextProperties.Position);
//
//                 //If distance teleports assume rest do.
//                 if (_enableTeleport && (positionRate >= _teleportThreshold))
//                 {
//                     LSetInstantRates();
//                     return;
//                 }
//                 else if (LowDistance(positionRate, rotation: false))
//                 {
//                     positionRate = MoveRatesCls.INSTANT_VALUE;
//                 }
//             }
//
//             //Rotation.
//             if (_synchronizeRotation && ChangedDeltaContains(nextChanged, ChangedDelta.Rotation))
//             {
//                 rotationRate = prevProperties.Rotation.Angle(nextProperties.Rotation, precise: true);
//
//                 if (LowDistance(rotationRate, rotation: true))
//                     rotationRate = MoveRatesCls.INSTANT_VALUE;
//             }
//
//             //Scale.
//             if (_synchronizeScale && ChangedDeltaContains(nextChanged, ChangedDelta.Scale))
//             {
//                 scaleRate = Vector3.Distance(prevProperties.Scale, nextProperties.Scale);
//
//                 if (LowDistance(scaleRate, rotation: false))
//                     scaleRate = MoveRatesCls.INSTANT_VALUE;
//             }
//
//             rd.Update(positionRate, rotationRate, scaleRate, tickDifference, timePassed);
//
//             /* Returns if the provided distance is minuscule.
//              * This is used to decide if a property should be teleported.
//              * When distances are exceptionally small smoothing rate
//              * calculations may result as an invalid value. */
//             bool LowDistance(float dist, bool rotation)
//             {
//                 if (rotation)
//                     return (dist < 1f);
//                 else
//                     return (dist < 0.001f);
//             }
//
//             void LSetInstantRates()
//             {
//                 SetInstantRates(rd, tickDifference, timePassed);
//             }
//         }
//
//         /// <summary>
//         /// Gets the tick difference between two GoalDatas.
//         /// </summary>
//         private uint GetTickDifference(uint previousTick, uint nextTick, uint minimum)
//         {
//             long tickDifference = (nextTick - previousTick);
//
//             //Invalid/impossible value.
//             if (tickDifference <= TimeManager.UNSET_TICK)
//                 tickDifference = 1;
//
//             if (tickDifference < minimum)
//                 tickDifference = minimum;
//
//             return (uint)tickDifference;
//         }
//         #endregion
//
//         /// <summary>
//         /// Sets extrapolation data on next.
//         /// </summary>
//         private TransformProperties GetExtrapolatedTransformProperties(TransformProperties previousProperties, TransformProperties nextProperties)
//         {
//             
//         }
//
//         /// <summary>
//         /// Updates a client with transform data.
//         /// </summary>
//         [TargetRpc(ValidateTarget = false)]
//         private void TargetUpdateTransform(NetworkConnection conn, ArraySegment<byte> data, Channel channel)
//         {
// #if DEVELOPMENT
//             //If receiver is client host then do nothing, clientHost need not process.
//             if (base.IsServerInitialized && conn.IsLocalClient)
//                 return;
// #endif
//             /* Zero data was sent, this should not be possible.
//              * This is a patch to a NetworkLOD bug until it can
//              * be resolved properly. */
//             if (data.Count == 0)
//                 return;
//
//             DataReceived(data, channel, false);
//         }
//
//         /// <summary>
//         /// Updates clients with transform data.
//         /// </summary>
//         [ObserversRpc]
//         private void ObserversUpdateClientAuthoritativeTransform(ArraySegment<byte> data, Channel channel)
//         {
//             if (!_clientAuthoritative && base.IsOwner && !_sendToOwner)
//                 return;
//             if (_clientAuthoritative && base.IsOwner)
//                 return;
//             if (base.IsServerInitialized)
//                 return;
//             //Not new data.
//             uint lastPacketTick = _timeManager.LastPacketTick.LastRemoteTick;
//             if (lastPacketTick <= _lastObserversRpcTick)
//                 return;
//
//             _lastObserversRpcTick = lastPacketTick;
//             DataReceived(data, channel, false);
//         }
//
//         /// <summary>
//         /// Updates the transform on the server.
//         /// </summary>
//         [ServerRpc]
//         private void ServerUpdateTransform(ArraySegment<byte> data, Channel channel)
//         {
//             if (!_clientAuthoritative)
//             {
//                 base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
//                 return;
//             }
//
//             TimeManager tm = base.TimeManager;
//             //Not new data.
//             uint lastPacketTick = tm.LastPacketTick.LastRemoteTick;
//             if (lastPacketTick <= _lastServerRpcTick)
//                 return;
//             _lastServerRpcTick = lastPacketTick;
//
//             _authoritativeClientData.Update(data, channel, updateHasData: true, tm.LocalTick);
//             DataReceived(data, channel, true);
//         }
//
//         /// <summary>
//         /// Processes received data for lcients and server.
//         /// </summary>
//         private void DataReceived(ArraySegment<byte> data, Channel channel, bool asServer)
//         {
//             if (base.IsDeinitializing)
//                 return;
//
//             TransformData prevTd = (asServer) ? _lastReceivedClientTransformData : _lastReceivedServerTransformData;
//             MoveRate prevRd = _moveRates;
//
//             ChangedFull changedFull = ChangedFull.Unset;
//             NetworkTransform.GoalData nextGd = ResettableObjectCaches<NetworkTransform.GoalData>.Retrieve();
//             TransformData nextTd = nextGd.Transforms;
//             UpdateTransformData(data, prevTd, nextTd, ref changedFull);
//
//             OnDataReceived?.Invoke(prevTd, nextTd);
//             SetExtrapolation(prevTd, nextTd, channel);
//
//             bool hasChanged = HasChanged(prevTd, nextTd);
//
//             //If server only teleport.
//             if (asServer && !base.IsClientStarted)
//             {
//                 uint tickDifference = GetTickDifference(prevTd, nextGd, 1, asServer: true, out float timePassed);
//                 SetInstantRates(nextGd.Rates, tickDifference, timePassed);
//             }
//             //Otherwise use timed.
//             else
//             {
//                 SetCalculatedRates(prevTd, prevRd, nextGd, changedFull, hasChanged, channel, asServer);
//             }
//
//             _lastReceiveReliable = (channel == Channel.Reliable);
//             /* If channel is reliable then this is a settled packet.
//              * Set tick to UNSET. When this occurs time calculations
//              * assume only 1 tick has passed. */
//             if (channel == Channel.Reliable)
//                 nextTd.ReceivedTick = TimeManager.UNSET_TICK;
//
//             prevTd.Update(nextTd);
//             prevRd.Update(nextGd.Rates);
//
//             nextGd.ReceivedTick = _timeManager.LocalTick;
//
//             bool currentDataNull = (_currentGoalData == null);
//             /* If extrapolating then immediately break the extrapolation
//              * in favor of newest results. This will keep the buffer
//              * at 0 until the transform settles but the only other option is
//              * to stop the movement, which would defeat purpose of extrapolation,
//              * or slow down the transform while buffer rebuilds. Neither choice
//              * is great but later on I might try slowing down the transform slightly
//              * to give the buffer a chance to rebuild. */
//             if (!currentDataNull && _currentGoalData.Transforms.ExtrapolationState == TransformData.ExtrapolateState.Active)
//             {
//                 SetCurrentGoalData(nextGd);
//             }
//             /* If queue isn't started and its buffered enough
//              * to satisfy interpolation then set ready
//              * and set current data.
//              *
//              * Also if reliable then begin moving. */
//             else if (currentDataNull && _goalDataQueue.Count >= _interpolation || channel == Channel.Reliable)
//             {
//                 if (_goalDataQueue.Count > 0)
//                 {
//                     SetCurrentGoalData(_goalDataQueue.Dequeue());
//                     /* If is reliable and has changed then also
//                      * enqueue latest. */
//                     if (hasChanged)
//                         _goalDataQueue.Enqueue(nextGd);
//                 }
//                 else
//                 {
//                     SetCurrentGoalData(nextGd);
//                 }
//             }
//             /* If here then there's not enough in buffer to begin
//              * so add onto the buffer. */
//             else
//             {
//                 _goalDataQueue.Enqueue(nextGd);
//             }
//
//             /* If the queue is excessive beyond interpolation then
//              * dequeue extras to prevent from dropping behind too
//              * quickly. This shouldn't be an issue with normal movement
//              * as the NT speeds up if the buffer unexpectedly grows, but
//              * when connections are unstable results may come in chunks
//              * and for a better experience the older parts of the chunks
//              * will be dropped. */
//             if (_goalDataQueue.Count > (_interpolation + 3))
//             {
//                 while (_goalDataQueue.Count > _interpolation)
//                 {
//                     NetworkTransform.GoalData tmpGd = _goalDataQueue.Dequeue();
//                     ResettableObjectCaches<NetworkTransform.GoalData>.Store(tmpGd);
//                 }
//
//                 //Snap to the next data to fix any smoothing timings.
//                 SetCurrentGoalData(_goalDataQueue.Dequeue());
//                 SetInstantRates(_currentGoalData!.Rates, 1, -1f);
//                 SnapProperties(_currentGoalData.Transforms, true);
//             }
//         }
//
//         /// <summary>
//         /// Sets the next move data using received datas.
//         /// </summary>
//         private void SetNextMoveData()
//         {
//             BasicQueue<DetailedTransformProperties> received = _moveDatas.TransformProperties;
//             int count = received.Count;
//             //Minimum 2 entries are required to set rates.
//             if (count < 2)
//                 return;
//
//             DetailedTransformProperties a = received[0];
//             DetailedTransformProperties b = received[1];
//
//             SetMoveRates(a, b);
//
//             if (count == 2)
//                 SetExtrapolatedTransformProperties();
//
//             _currentMoveProperties = received.Dequeue().Properties;
//         }
//
//         /// <summary>
//         /// Updates a TransformData from packetData.
//         /// </summary>
//         private void UpdateTransformData(ArraySegment<byte> packetData, TransformData prevTransformData, TransformData nextTransformData, ref ChangedFull changedFull)
//         {
//             DeserializePacket(packetData, prevTransformData, nextTransformData, ref changedFull);
//             nextTransformData.ReceivedTick = _timeManager.LastPacketTick.LastRemoteTick;
//         }
//
//         /// <summary>
//         /// Returns true if whole contains part.
//         /// </summary>
//         private bool ChangedDeltaContains(ChangedDelta whole, ChangedDelta part) => (whole & part) == part;
//
//         /// <summary>
//         /// Returns true if whole contains part.
//         /// </summary>
//         private bool SnappedAxesContains(SnappedAxes whole, SnappedAxes part) => (whole & part) == part;
//
//         /// <summary>
//         /// Sets synchronized values based on value.
//         /// </summary>
//         private void CacheSynchronizedProperties(TransformPropertiesFlag value)
//         {
//             _synchronizePosition = FlagContains(value, TransformPropertiesFlag.Position);
//             _synchronizeRotation = FlagContains(value, TransformPropertiesFlag.Rotation);
//             _synchronizeScale = FlagContains(value, TransformPropertiesFlag.LocalScale);
//
//             bool FlagContains(TransformPropertiesFlag whole, TransformPropertiesFlag part) => (whole & part) == part;
//         }
//
//         private void InitializeState()
//         {
//             _moveDatas = ObjectCaches<TransformDataQueue>.Retrieve();
//             _toClientChangedWriter = WriterPool.Retrieve();
//             _cachedTransform = transform;
//
//             CacheSynchronizedProperties(_synchronizedProperties);
//
//             base.SetTickCallbacks((TickCallback.PostTick | TickCallback.Update));
//         }
//
//         private void ResetState()
//         {
//             ObjectCaches<TransformDataQueue>.StoreAndDefault(ref _moveDatas);
//             ResettableObjectCaches<PooledWriter>.StoreAndDefault(ref _toClientChangedWriter);
//             ObjectCaches<bool>.StoreAndDefault(ref _authoritativeClientData.HasData);
//             _cachedTransform = null;
//             _cachedPredictedOwner = null;
//             _timeManager = null;
//         }
//     }
// }