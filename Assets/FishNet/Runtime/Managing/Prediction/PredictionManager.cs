using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using GameKit.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;


namespace FishNet.Managing.Predicting
{

    /// <summary>
    /// Additional options for managing the observer system.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/PredictionManager")]
    public sealed class PredictionManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        public event Action<NetworkBehaviour> OnPreReconcile;
        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        public event Action<NetworkBehaviour> OnPostReconcile;
        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        public event Action<uint, PhysicsScene, PhysicsScene2D> OnPreReplicateReplay;
        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        public event Action<uint, PhysicsScene, PhysicsScene2D> OnPostReplicateReplay;
        /// <summary>
        /// Called before the server sends a reconcile.
        /// </summary>
        public event Action<NetworkBehaviour> OnPreServerReconcile;
        /// <summary>
        /// Called after the server sends a reconcile.
        /// </summary>
        public event Action<NetworkBehaviour> OnPostServerReconcile;
        /// <summary>
        /// Last tick any object reconciled.
        /// </summary>
        public uint LastReconcileTick { get; internal set; }
        /// <summary>
        /// Last tick any object replicated.
        /// </summary>
        public uint LastReplicateTick { get; internal set; }
        /// <summary>
        /// True if rigidbodies are being predicted.
        /// </summary>
        internal bool UsingRigidbodies => (_rigidbodies.Count > 0);
        /// <summary>
        /// Returns if any prediction is replaying.
        /// </summary>
        /// <returns></returns>
        public bool IsReplaying() => _isReplaying;
        private bool _isReplaying;
        /// <summary>
        /// Returns if scene is replaying.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public bool IsReplaying(UnityScene scene) => _replayingScenes.Contains(scene);
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Number of inputs to keep in queue should the server miss receiving an input update from the client. " +
            "Higher values will increase the likeliness of the server always having input from the client while lower values will allow the client input to run on the server faster. " +
            "This value cannot be higher than MaximumServerReplicates.")]
        [Range(1, 15)]
        [SerializeField]
        private ushort _queuedInputs = 1;
        /// <summary>
        /// Number of inputs to keep in queue should the server miss receiving an input update from the client.
        /// Higher values will increase the likeliness of the server always having input from the client while lower values will allow the client input to run on the server faster.
        /// This value cannot be higher than MaximumServerReplicates.
        /// </summary>
        public ushort QueuedInputs => (ushort)(_queuedInputs + 1);
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to drop replicates from clients which are being received excessively. This can help with attacks but may cause client to temporarily desynchronize during connectivity issues. When false the server will hold at most up to 3 seconds worth of replicates, consuming multiple per tick to clear out the buffer quicker. This is good to ensure all inputs are executed but potentially could allow speed hacking.")]
        [SerializeField]
        private bool _dropExcessiveReplicates = true;
        /// <summary>
        /// True to drop replicates from clients which are being received excessively. This can help with attacks but may cause client to temporarily desynchronize during connectivity issues.
        /// When false the server will hold at most up to 3 seconds worth of replicates, consuming multiple per tick to clear out the buffer quicker. This is good to ensure all inputs are executed but potentially could allow speed hacking.
        /// </summary>
        internal bool DropExcessiveReplicates => _dropExcessiveReplicates;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of replicates a server can queue per object. Higher values will put more load on the server and add replicate latency for the client.")]
        [SerializeField]
        private ushort _maximumServerReplicates = 15;
        /// <summary>
        /// Maximum number of replicates a server can queue per object. Higher values will put more load on the server and add replicate latency for the client.
        /// </summary>
        public ushort GetMaximumServerReplicates() => _maximumServerReplicates;
        /// <summary>
        /// Sets the maximum number of replicates a server can queue per object.
        /// </summary>
        /// <param name="value"></param>
        public void SetMaximumServerReplicates(ushort value)
        {
            _maximumServerReplicates = (ushort)Mathf.Clamp(value, MINIMUM_REPLICATE_QUEUE_SIZE, MAXIMUM_REPLICATE_QUEUE_SIZE);
        }
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of excessive replicates which can be consumed per tick. Consumption count will scale up to this value automatically.")]
        [SerializeField]
        private byte _maximumConsumeCount = 4;
        /// <summary>
        /// Maximum number of excessive replicates which can be consumed per tick. Consumption count will scale up to this value automatically.
        /// </summary>
        internal byte MaximumReplicateConsumeCount => _maximumConsumeCount;
        /// <summary>
        /// Clients should store no more than 2 seconds worth of replicates.
        /// </summary>
        internal ushort MaximumClientReplicates => (ushort)(_networkManager.TimeManager.TickRate * 5);
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of past inputs which may send.")]
        [Range(MINIMUM_PAST_INPUTS, MAXIMUM_PAST_INPUTS)]
        [SerializeField]
        private byte _redundancyCount = 2;
        /// <summary>
        /// Maximum number of past inputs which may send and resend redundancy.
        /// </summary>
        internal byte RedundancyCount => _redundancyCount;
        /// <summary>
        /// True to allow clients to use predicted spawning. While true, each NetworkObject prefab you wish to predicted spawn must be marked as to allow this feature.
        /// </summary>
        internal bool GetAllowPredictedSpawning() => _allowPredictedSpawning;
        [Tooltip("True to allow clients to use predicted spawning and despawning. While true, each NetworkObject prefab you wish to predicted spawn must be marked as to allow this feature.")]
        [SerializeField]
        private bool _allowPredictedSpawning = false;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of Ids to reserve on clients for predicted spawning. Higher values will allow clients to send more predicted spawns per second but may reduce availability of ObjectIds with high player counts.")]
        [Range(1, 100)]
        [SerializeField]
        private byte _reservedObjectIds = 15;
        /// <summary>
        /// Maximum number of Ids to reserve on clients for predicted spawning. Higher values will allow clients to send more predicted spawns per second but may reduce availability of ObjectIds with high player counts.
        /// </summary>
        /// <returns></returns>
        internal byte GetReservedObjectIds() => _reservedObjectIds;
        #endregion

        #region Private.
        /// <summary>
        /// Number of active predicted rigidbodies.
        /// </summary>
        [System.NonSerialized]
        private HashSet<UnityEngine.Component> _rigidbodies = new HashSet<UnityEngine.Component>();
        /// <summary>
        /// Cache to remove null entries from _rigidbodies.
        /// </summary>
        [System.NonSerialized]
        private HashSet<UnityEngine.Component> _componentCache = new HashSet<UnityEngine.Component>();
        /// <summary>
        /// Scenes which are currently replaying prediction.
        /// </summary>
        private HashSet<UnityScene> _replayingScenes = new HashSet<UnityScene>(new SceneHandleEqualityComparer());
        /// <summary>
        /// NetworkManager used with this.
        /// </summary>
        private NetworkManager _networkManager;
        #endregion

        #region Const.
        /// <summary>
        /// Minimum number of past inputs which can be sent.
        /// </summary>
        private const byte MINIMUM_PAST_INPUTS = 2;
        /// <summary>
        /// Maximum number of past inputs which can be sent.
        /// </summary>
        internal const byte MAXIMUM_PAST_INPUTS = 15;
        /// <summary>
        /// Minimum amount of replicate queue size.
        /// </summary>
        private const ushort MINIMUM_REPLICATE_QUEUE_SIZE = 10;
        /// <summary>
        /// Maxmimum amount of replicate queue size.
        /// </summary>
        private const ushort MAXIMUM_REPLICATE_QUEUE_SIZE = 500;
        #endregion

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;
        }

        internal void InitializeOnce(NetworkManager manager)
        {
            _networkManager = manager;
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        }
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            if (obj.ConnectionState != LocalConnectionState.Started)
                _replayingScenes.Clear();
        _isReplaying = false;
        }

        /// <summary>
        /// Called before and after server sends a reconcile.
        /// </summary>
        /// <param name="before">True if before the reconcile is sent.</param>
        internal void InvokeServerReconcile(NetworkBehaviour caller, bool before)
        {
            if (before)
                OnPreServerReconcile?.Invoke(caller);
            else
                OnPostServerReconcile?.Invoke(caller);
        }

        /// <summary>
        /// Increases Rigidbodies count by 1.
        /// </summary>
        [APIExclude]
        public void AddRigidbodyCount(UnityEngine.Component c)
        {
            _rigidbodies.Add(c);
        }

        /// <summary>
        /// Dencreases Rigidbodies count by 1.
        /// </summary>
        [APIExclude]
        public void RemoveRigidbodyCount(UnityEngine.Component c)
        {
            bool removed = _rigidbodies.Remove(c);
            /* If remove failed the rigidbodies may need to be rebuild.
             * This might happen when an object is destroyed as
             * the referenced is passed. Could be any number of things
             * but it seems to occur frequently enough in Unity,
             * especially when testing in editor.
             * 
             * This operation is not ideal in the hot path but
             * the odds of it happening are pretty slim and
             * it ensures stability against user error. */
            if (!removed)
            {
                //Cannt remove null entries from a hashset so have to rebuild.
                _componentCache.Clear();
                foreach (UnityEngine.Component item in _rigidbodies)
                {
                    if (item != null)
                        _componentCache.Add(item);
                }

                //Apply to rigidbodies.
                _rigidbodies.Clear();
                foreach (UnityEngine.Component item in _componentCache)
                    _rigidbodies.Add(item);
            }
        }

        /// <summary>
        /// Invokes OnPre/PostReconcile events.
        /// Internal use.
        /// </summary>
        [APIExclude]
        [CodegenMakePublic] //To internal.
        internal void InvokeOnReconcile(NetworkBehaviour nb, bool before)
        {
            nb.IsReconciling = before;
            if (before)
                OnPreReconcile?.Invoke(nb);
            else
                OnPostReconcile?.Invoke(nb);
        }
        /// <summary>
        /// Invokes OnReplicateReplay.
        /// Internal use.
        /// </summary>
        [APIExclude]
        internal void InvokeOnReplicateReplay(UnityScene scene, uint tick, PhysicsScene ps, PhysicsScene2D ps2d, bool before)
        {
            _isReplaying = before;
            if (before)
            {
                _replayingScenes.Add(scene);
                OnPreReplicateReplay?.Invoke(tick, ps, ps2d);
            }
            else
            {
                _replayingScenes.Remove(scene);
                OnPostReplicateReplay?.Invoke(tick, ps, ps2d);
            }
        }

        /// <summary>
        /// Called when a scene unloads.
        /// </summary>
        /// <param name="arg0"></param>
        private void SceneManager_sceneUnloaded(UnityScene s)
        {
            _replayingScenes.Remove(s);
        }

    }

}