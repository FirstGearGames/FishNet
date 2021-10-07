using FishNet.Managing.Object;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Utility;

namespace FishNet.Managing.Server.Object
{
    public partial class ServerObjects : ManagedObjects
    {
        #region Public.
        /// <summary>
        /// Called right before client objects are destroyed when a client disconnects.
        /// Clearing Objects for NetworkConnection will prevent them from being destroyed.
        /// </summary>
        public event Action<NetworkConnection> OnPreDestroyClientObjects;
        #endregion

        #region Private.
        /// <summary>
        /// Next ObjectId which may be used for NetworkObjects.
        /// </summary>
        private int _nextNetworkObjectId = 0;
        /// <summary>
        /// Cached ObjectIds which may be used when exceeding available ObjectIds.
        /// </summary>
        private Queue<int> _objectIdCache = new Queue<int>();
        /// <summary>
        /// Cache for network objects a disconnected client owned.
        /// </summary>
        private ListCache<NetworkObject> _disconnectedClientObjectsCache = new ListCache<NetworkObject>();
        /// <summary>
        /// NetworkBehaviours which have dirty SyncVars.
        /// </summary>
        private List<NetworkBehaviour> _dirtySyncVarBehaviours = new List<NetworkBehaviour>(20);
        /// <summary>
        /// NetworkBehaviours which have dirty SyncObjects.
        /// </summary>
        private List<NetworkBehaviour> _dirtySyncObjectBehaviours = new List<NetworkBehaviour>(20);
        #endregion

        public ServerObjects(NetworkManager networkManager)
        {
            base.NetworkManager = networkManager;
            InitializeObservers();
        }

        #region Checking dirty SyncTypes.
        /// <summary>
        /// Iterates NetworkBehaviours with dirty SyncTypes.
        /// </summary>
        internal void CheckDirtySyncTypes()
        {
            /* Tells networkbehaviours to check their
             * dirty synctypes. */
            IterateCollection(_dirtySyncVarBehaviours, false);
            IterateCollection(_dirtySyncObjectBehaviours, true);

            void IterateCollection(List<NetworkBehaviour> collection, bool isSyncObject)
            {
                for (int i = 0; i < collection.Count; i++)
                {
                    bool dirtyCleared = collection[i].WriteDirtySyncTypes(isSyncObject);
                    if (dirtyCleared)
                    {
                        collection.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
        /// <summary>
        /// Sets that a NetworkBehaviour has a dirty syncVars.
        /// </summary>
        /// <param name="nb"></param>
        internal void SetDirtySyncType(NetworkBehaviour nb, bool isSyncObject)
        {
            if (isSyncObject)
                _dirtySyncObjectBehaviours.Add(nb);
            else
                _dirtySyncVarBehaviours.Add(nb);
        }
        #endregion

        #region Connection Handling.
        /// <summary>
        /// Called when the connection state changes for the local server.
        /// </summary>
        /// <param name="args"></param>
        internal void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            //If server just connected.
            if (args.ConnectionState == LocalConnectionStates.Started)
            {
                SetupSceneObjects();
            }
            //Server in anything but started state.
            else
            {
                base.DespawnSpawnedWithoutSynchronization(true);
                base.SceneObjects.Clear();
                _nextNetworkObjectId = 0;
            }
        }

        /// <summary>
        /// Called when a client disconnects.
        /// </summary>
        /// <param name="connection"></param>
        internal void ClientDisconnected(NetworkConnection connection)
        {
            RemoveFromObserversWithoutSynchronization(connection);

            OnPreDestroyClientObjects?.Invoke(connection);

            _disconnectedClientObjectsCache.Reset();
            foreach (NetworkObject nob in connection.Objects)
                _disconnectedClientObjectsCache.AddValue(nob);

            for (int i = 0; i < _disconnectedClientObjectsCache.Written; i++)
            {
                NetworkObject nob = _disconnectedClientObjectsCache.Collection[i];
                if (nob.SceneObject)
                    nob.RemoveOwnership();
                else
                    nob.Despawn();
            }
        }
        #endregion

        #region ObjectIds.
        /// <summary>
        /// Caches a NetworkObject ObjectId.
        /// </summary>
        /// <param name="nob"></param>
        private void CacheObjectId(NetworkObject nob)
        {
            if (nob.ObjectId >= 0)
                _objectIdCache.Enqueue(nob.ObjectId);
        }

        /// <summary>
        /// Gets the next ObjectId to use for NetworkObjects.
        /// </summary>
        /// <returns></returns>
        protected override int GetNextNetworkObjectId()
        {
            //Favor the cache first.
            if (_objectIdCache.Count > 0)
            {
                return _objectIdCache.Dequeue();
            }
            //None cached.
            else
            {
                //Either something went wrong or user actually managed to spawn ~32K networked objects.
                if (_nextNetworkObjectId > short.MaxValue)
                {
                    if (base.NetworkManager.CanLog(Logging.LoggingType.Error))
                        Debug.LogError($"No more available ObjectIds. How the heck did you manage to have {short.MaxValue} objects spawned at once?");
                    return -1;
                }
                else
                {
                    int value = _nextNetworkObjectId;
                    _nextNetworkObjectId++;
                    return value;
                }
            }
        }
        #endregion

        #region Initializing Objects In Scenes.
        /// <summary>
        /// Called when a scene loads on the server.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="arg1"></param>
        protected override void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1)
        {
            base.SceneManager_sceneLoaded(s, arg1);

            if (!NetworkManager.ServerManager.Started)
                return;
            SetupSceneObjects(s);
        }

        /// <summary>
        /// Setup all NetworkObjects in scenes. Should only be called when server is active.
        /// </summary>
        protected void SetupSceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                SetupSceneObjects(SceneManager.GetSceneAt(i));
        }

        /// <summary>
        /// Setup NetworkObjects in a scene. Should only be called when server is active.
        /// </summary>
        /// <param name="s"></param>
        private void SetupSceneObjects(Scene s)
        {
            if (!NetworkManager.ServerManager.Started)
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"Cannot setup scene objects while server is not active.");
                return;
            }

            //Iterate root objects, and down their hierarchy.
            foreach (GameObject go in s.GetRootGameObjects())
            {
                //Root object first.
                CheckSetupObject(go);
                //Objects children.
                foreach (Transform t in go.transform)
                    CheckSetupObject(t.gameObject);
            }

            void CheckSetupObject(GameObject go)
            {
                NetworkObject nob;
                //If has a NetworkObject component.
                if (go.TryGetComponent(out nob))
                {
                    //Only setup if a scene object and not initialzied.
                    if (nob.SceneObject && nob.Deinitializing)
                    {
                        base.AddToSceneObjects(nob);
                        /* If was active in the editor (before hitting play), or currently active
                         * then PreInitialize without synchronizing to clients. There is no reason
                         * to synchronize to clients because the scene just loaded on server,
                         * which means clients are not yet in the scene. */
                        if (nob.ActiveDuringEdit || nob.gameObject.activeInHierarchy)
                            SetupWithoutSynchronization(nob);
                    }
                }
            }
        }

        /// <summary>
        /// Performs setup on a NetworkObject without synchronizing the actions to clients.
        /// </summary>
        /// <param name="nob"></param>
        private void SetupWithoutSynchronization(NetworkObject nob, NetworkConnection ownerConnection = null)
        {
            int objectId = GetNextNetworkObjectId();
            nob.PreInitialize(NetworkManager, objectId, ownerConnection, true);
            base.AddToSpawned(nob);
            nob.gameObject.SetActive(true);
            nob.Initialize(true);
        }
        #endregion    

        #region Spawning.
        /// <summary>
        /// Spawns an object over the network.
        /// </summary>
        /// <param name="networkObject"></param>
        internal void Spawn(NetworkObject networkObject, NetworkConnection ownerConnection = null)
        {
            if (!NetworkManager.ServerManager.Started)
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning("Cannot spawn object because the server is not active.");
                return;
            }
            if (networkObject == null)
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Error))
                    Debug.LogError($"Specified networkObject is null.");
                return;
            }
            if (!networkObject.gameObject.scene.IsValid())
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Error))
                    Debug.LogError($"{networkObject.name} is a prefab. You must instantiate the prefab first, then use Spawn on the instantiated copy.");
                return;
            }
            if (ownerConnection != null && ownerConnection.IsValid && !ownerConnection.LoadedStartScenes)
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"{networkObject.name} was spawned but it's recommended to not spawn objects for connections until they have loaded start scenes. You can be notified when a connection loads start scenes by using connection.OnLoadedStartScenes on the connection, or SceneManager.OnClientLoadStartScenes.");
            }
            if (networkObject.IsSpawned)
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"{networkObject.name} is already spawned.");
                return;
            }

            /* Setup locally without sending to clients.
             * When observers are built for the network object
             * during initialization spawn messages will
             * be sent. */
            SetupWithoutSynchronization(networkObject, ownerConnection);

            //If there is an owner then try to add them to the networkObjects scene.
            if (ownerConnection != null && ownerConnection.IsValid)
                base.NetworkManager.SceneManager.AddConnectionToScene(ownerConnection, networkObject.gameObject.scene);
            //Also rebuild observers for the object so it spawns for others.
            RebuildObservers(networkObject);
        }

        /// <summary>
        /// Writes a spawn into writers.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="connection">Connection spawn is being written for.</param>
        /// <param name="everyoneWriter"></param>
        /// <param name="ownerWriter"></param>
        private void WriteSpawn(NetworkObject nob, NetworkConnection connection, ref PooledWriter everyoneWriter, ref PooledWriter ownerWriter)
        {
            /* Using a number of writers to prevent rebuilding the
             * packets excessively for values that are owner only
             * vs values that are everyone. To save performance the
             * owner writer is only written to if owner is valid.
             * This makes the code a little uglier but will scale
             * significantly better with more connections.
             * 
             * EG:
             * with this technique networkBehaviours are iterated
             * twice if there is an owner; once for data to send to everyone
             * and again for data only going to owner. 
             *
             * The alternative would be to iterate the networkbehaviours
             * for every connection it's going to and filling a single
             * writer with values based on if owner or not. This would
             * result in significantly more iterations. */
            PooledWriter commonWriter = WriterPool.GetWriter();
            commonWriter.WriteByte((byte)PacketId.ObjectSpawn);
            commonWriter.WriteNetworkObject(nob);
            if (base.NetworkManager.ServerManager.ShareOwners || connection == nob.Owner)
                commonWriter.WriteInt16((short)nob.OwnerId);
            else
                commonWriter.WriteInt16(-1);

            /* Write if a scene object or not, and also
             * store sceneObjectId if is a scene object. */
            bool sceneObject = nob.SceneObject;
            commonWriter.WriteBoolean(sceneObject);
            /* Writing a scene object. */
            if (sceneObject)
            {
                //Write Guid.
                commonWriter.WriteUInt64(nob.SceneId, AutoPackType.Unpacked);
                //Write changed properties.
                ChangedTransformProperties ctp = nob.GetChangedSceneTransformProperties();
                commonWriter.WriteByte((byte)ctp);
                //If properties have changed.
                if (ctp != ChangedTransformProperties.Unset)
                {
                    //Write any changed properties.
                    if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.Position))
                        commonWriter.WriteVector3(nob.transform.position);
                    if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.Rotation))
                        commonWriter.WriteQuaternion(nob.transform.rotation);
                    if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.LocalScale))
                        commonWriter.WriteVector3(nob.transform.localScale);
                }
            }
            /* Writing a spawned object. */
            else
            {
                commonWriter.WriteInt16(nob.PrefabId);
                /* //muchlater Write only properties that are different
                 * from the prefab. Odds are position will be changed,
                 * and possibly rotation, but not too likely scale. */
                commonWriter.WriteVector3(nob.transform.position);
                commonWriter.WriteQuaternion(nob.transform.rotation);
                commonWriter.WriteVector3(nob.transform.localScale);
            }

            /* Used to write latest data which must be sent to
             * clients, such as syncVars. */
            PooledWriter syncWriter = WriterPool.GetWriter();

            //Populate everyone first.
            everyoneWriter.WriteBytes(commonWriter.GetBuffer(), 0, commonWriter.Length);
            foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                nb.WriteSyncTypesForSpawn(syncWriter, false);
            everyoneWriter.WriteBytesAndSize(syncWriter.GetBuffer(), 0, syncWriter.Length);

            //If owner is valid then populate owner writer as well.
            if (nob.OwnerIsValid)
            {
                syncWriter.Reset();
                ownerWriter.WriteBytes(commonWriter.GetBuffer(), 0, commonWriter.Length);
                foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                    nb.WriteSyncTypesForSpawn(syncWriter, true);
                ownerWriter.WriteBytesAndSize(syncWriter.GetBuffer(), 0, syncWriter.Length);
            }

            //Dispose of writers created in this method.
            commonWriter.Dispose();
            syncWriter.Dispose();
        }
        #endregion

        #region Despawning.
        /// <summary>
        /// Despawns an object over the network.
        /// </summary>
        /// <param name="nob"></param>
        internal override void Despawn(NetworkObject nob, bool asServer)
        {
            if (nob.CanSpawnOrDespawn(true))
            {
                FinalizeDespawn(nob);
                base.Despawn(nob, true);
            }
        }

        /// <summary>
        /// Called when a NetworkObject is destroyed without being deactivated first.
        /// </summary>
        /// <param name="nob"></param>
        internal override void NetworkObjectUnexpectedlyDestroyed(NetworkObject nob)
        {
            FinalizeDespawn(nob);
            base.NetworkObjectUnexpectedlyDestroyed(nob);
        }

        /// <summary>
        /// Finalizes the despawn process. By the time this is called the object is considered unaccessible.
        /// </summary>
        /// <param name="nob"></param>
        private void FinalizeDespawn(NetworkObject nob)
        {
            if (nob != null && nob.ObjectId != -1)
            {
                nob.WriteDirtySyncTypes();
                WriteDespawnAndSend(nob);
                CacheObjectId(nob);
            }
        }

        /// <summary>
        /// Writes a despawn and sends it to clients.
        /// </summary>
        /// <param name="nob"></param>
        private void WriteDespawnAndSend(NetworkObject nob)
        {
            PooledWriter everyoneWriter = WriterPool.GetWriter();
            WriteDespawn(nob, ref everyoneWriter);

            ArraySegment<byte> despawnSegment = everyoneWriter.GetArraySegment();
            foreach (NetworkConnection conn in nob.Observers)
            {
                nob.InvokeOnServerDespawn(conn);
                NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, despawnSegment, conn);
            }

            everyoneWriter.Dispose();
        }
        /// <summary>
        /// Writes a despawn.
        /// </summary>
        /// <param name="nob"></param>
        private void WriteDespawn(NetworkObject nob, ref PooledWriter everyoneWriter)
        {
            everyoneWriter.WriteByte((byte)PacketId.ObjectDespawn);
            everyoneWriter.WriteNetworkObject(nob);
        }
    }
    #endregion



}
