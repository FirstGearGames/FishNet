using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Client
{
    /// <summary>
    /// Handles objects and information about objects for the local client. See ManagedObjects for inherited options.
    /// </summary>
    public partial class ClientObjects : ManagedObjects
    {

        #region Private.
        /// <summary>
        /// NetworkObjects which are cached to be spawned or despawned.
        /// </summary>
        private ClientObjectCache _objectCache;
        #endregion

        internal ClientObjects(NetworkManager networkManager)
        {
            base.NetworkManager = networkManager;
            _objectCache = new ClientObjectCache(this);
        }

        /// <summary>
        /// Called when a connection state changes for the local server.
        /// </summary>
        internal void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            //Nothing needs to be done if started.
            if (args.ConnectionState == LocalConnectionStates.Started)
                return;

            /* If not started and client is active then deinitialize
             * client objects first. This will let the deinit calls
             * perform before the server destroys them. Ideally this
             * would be done when the user shows intent to shutdown
             * the server, but realistically planning for server socket
             * drops is a much more universal solution.
             *
             * Calling StopConnection on the client will set it's local state
             * to Stopping which will result in a deinit. */
            if (NetworkManager.IsClient)
                base.NetworkManager.ClientManager.StopConnection();
        }

        /// <summary>
        /// Called when the connection state changes for the local client.
        /// </summary>
        /// <param name="args"></param>
        internal void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            /* If new state is not started then reset
             * environment. */
            if (args.ConnectionState != LocalConnectionStates.Started)
            {
                _objectCache.Reset();
                base.DespawnSpawnedWithoutSynchronization(false);
                /* Clear spawned and scene objects as they will be rebuilt.
                 * Spawned would have already be cleared if DespawnSpawned
                 * was called but it won't hurt anything clearing an empty collection. */
                base.Spawned.Clear();
                base.SceneObjects.Clear();
            }
        }


        /// <summary>
        /// Called when a scene is loaded.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="arg1"></param>
        [APIExclude]
        protected internal override void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1)
        {
            base.SceneManager_sceneLoaded(s, arg1);

            if (!base.NetworkManager.IsClient)
                return;
            /* When a scene first loads for a client it should disable
             * all network objects in that scene. The server will send
             * spawn messages once it's aware client has loaded the scene. */
            RegisterAndDespawnSceneObjects(s);
        }

        /// <summary>
        /// Registers NetworkObjects in all scenes and despawns them.
        /// </summary>
        internal void RegisterAndDespawnSceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                RegisterAndDespawnSceneObjects(SceneManager.GetSceneAt(i));
        }

        /// <summary>
        /// Adds NetworkObjects within s to SceneObjects, and despawns them.
        /// </summary>
        /// <param name="s"></param>
        private void RegisterAndDespawnSceneObjects(Scene s)
        {
            ListCache<NetworkObject> nobs;
            SceneFN.GetSceneNetworkObjects(s, true, out nobs);

            for (int i = 0; i < nobs.Written; i++)
            {
                NetworkObject nob = nobs.Collection[i];
                base.UpdateNetworkBehaviours(nob, false);
                if (nob.IsNetworked && nob.IsSceneObject && nob.IsNetworked)
                {
                    base.AddToSceneObjects(nob);
                    //Only run if not also server, as this already ran on server.
                    if (!base.NetworkManager.IsServer)
                        nob.gameObject.SetActive(false);
                }
            }

            ListCaches.StoreCache(nobs);
        }

        /// <summary>
        /// Called when a NetworkObject runs Deactivate.
        /// </summary>
        /// <param name="nob"></param>
        internal override void NetworkObjectUnexpectedlyDestroyed(NetworkObject nob)
        {
            nob.RemoveClientRpcLinkIndexes();
            base.NetworkObjectUnexpectedlyDestroyed(nob);
        }

        /// <summary>
        /// Parses an OwnershipChange packet.
        /// </summary>
        /// <param name="reader"></param>
        internal void ParseOwnershipChange(PooledReader reader)
        {
            NetworkObject nob = reader.ReadNetworkObject();
            NetworkConnection newOwner = reader.ReadNetworkConnection();
            if (nob != null)
            {
                nob.GiveOwnership(newOwner, false);
            }
            else
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"NetworkBehaviour could not be found when trying to parse OwnershipChange packet.");
            }
        }

        /// <summary>
        /// Parses a received syncVar.
        /// </summary>
        /// <param name="reader"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseSyncType(PooledReader reader, bool isSyncObject, Channel channel)
        {
            //cleanup this is unique to synctypes where length comes first.
            //this will change once I tidy up synctypes.
            ushort packetId = (isSyncObject) ? (ushort)PacketId.SyncObject : (ushort)PacketId.SyncVar;
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            int dataLength = Packets.GetPacketLength(packetId, reader, channel);

            if (nb != null)
            {
                /* Length of data to be read for syncvars.
                 * This is important because syncvars are never
                 * a set length and data must be read through completion.
                 * The only way to know where completion of syncvar is, versus
                 * when another packet starts is by including the length. */
                int length = reader.ReadInt32();
                if (length > 0)
                    nb.OnSyncType(reader, length, isSyncObject);
            }
            else
            {
                SkipDataLength(packetId, reader, dataLength);
            }
        }

        /// <summary>
        /// Parses a ReconcileRpc.
        /// </summary>
        /// <param name="reader"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseReconcileRpc(PooledReader reader, Channel channel)
        {
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            int dataLength = Packets.GetPacketLength((ushort)PacketId.Reconcile, reader, channel);

            if (nb != null)
                nb.OnReconcileRpc(null, reader, channel);
            else
                SkipDataLength((ushort)PacketId.ObserversRpc, reader, dataLength);
        }

        /// <summary>
        /// Parses an ObserversRpc.
        /// </summary>
        /// <param name="reader"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseObserversRpc(PooledReader reader, Channel channel)
        {
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            int dataLength = Packets.GetPacketLength((ushort)PacketId.ObserversRpc, reader, channel);

            if (nb != null)
                nb.OnObserversRpc(null, reader, channel);
            else
                SkipDataLength((ushort)PacketId.ObserversRpc, reader, dataLength);
        }
        /// <summary>
        /// Parses a TargetRpc.
        /// </summary>
        /// <param name="reader"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseTargetRpc(PooledReader reader, Channel channel)
        {
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            int dataLength = Packets.GetPacketLength((ushort)PacketId.TargetRpc, reader, channel);

            if (nb != null)
                nb.OnTargetRpc(null, reader, channel);
            else
                SkipDataLength((ushort)PacketId.TargetRpc, reader, dataLength);
        }

        /// <summary>
        /// Caches a received spawn to be processed after all spawns and despawns are received for the tick.
        /// </summary>
        /// <param name="reader"></param>
        internal void CacheSpawn(PooledReader reader)
        {
            int objectId = reader.ReadNetworkObjectId();
            int ownerId = reader.ReadNetworkConnectionId();
            bool sceneObject = reader.ReadBoolean();

            NetworkObject nob;
            if (sceneObject)
                nob = ReadSceneObject(reader, true);
            else
                nob = ReadSpawnedObject(reader, objectId);

            ArraySegment<byte> rpcLinks = reader.ReadArraySegmentAndSize();
            ArraySegment<byte> syncValues = reader.ReadArraySegmentAndSize();

            /*If nob is null then exit method. Since ClientObjects gets nob from
             * server objects as host this can occur sometimes
             * when the object is destroyed on server before client gets
             * spawn packet. */
            if (nob == null)
            {
                //Only error if client only.
                if (!NetworkManager.IsHost)
                {
                    if (NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"Spawn object could not be found or created for Id {objectId}; scene object: {sceneObject}.");
                }

                return;
            }
            else
            {
                nob.SetIsNetworked(true);
            }
            /* If not host then pre-initialize. Pre-initializing applies
             * values needed to run such as owner, network manager, and completes
             * other reference creating functions. */
            if (!base.NetworkManager.IsHost)
            {
                //If local client is owner then use localconnection reference.
                NetworkConnection localConnection = base.NetworkManager.ClientManager.Connection;
                NetworkConnection owner;
                //If owner is self.
                if (ownerId == localConnection.ClientId)
                {
                    owner = localConnection;
                }
                else
                {
                    /* If owner cannot be found then share owners
                     * is disabled */
                    if (!base.NetworkManager.ClientManager.Clients.TryGetValueIL2CPP(ownerId, out owner))
                        owner = NetworkManager.EmptyConnection;
                }
                nob.PreinitializeInternal(NetworkManager, objectId, owner, false, false);
            }

            _objectCache.AddSpawn(nob, rpcLinks, syncValues, NetworkManager);
        }

        /// <summary>
        /// Caches a received despawn to be processed after all spawns and despawns are received for the tick.
        /// </summary>
        /// <param name="reader"></param>
        internal void CacheDespawn(PooledReader reader)
        {
            int objectId = reader.ReadNetworkObjectId();
            if (base.Spawned.TryGetValueIL2CPP(objectId, out NetworkObject nob))
                _objectCache.AddDespawn(nob);
        }


        /// <summary>
        /// Iterates object cache which contains spawn and despawn messages.
        /// Parses the packets within the cache and ensures objects are spawned and despawned before their sync values are applied.
        /// This ensures there is no chance a sync value is referencing a spawned object which does not exist yet due to it normally being spawned later in the cache.
        /// </summary>
        internal void IterateObjectCache()
        {
            _objectCache.Iterate();
        }

        /// <summary>
        /// Finishes reading a scene object.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="setProperties">True to also read properties and set them.</param>
        private NetworkObject ReadSceneObject(PooledReader reader, bool setProperties)
        {
            ulong sceneId = reader.ReadUInt64(AutoPackType.Unpacked);
            NetworkObject nob;
            base.SceneObjects.TryGetValueIL2CPP(sceneId, out nob);
            //If found in scene objects.
            if (nob != null)
            {
                if (setProperties)
                {
                    //Read changed.
                    ChangedTransformProperties ctp = (ChangedTransformProperties)reader.ReadByte();
                    //If scene object has changed.
                    if (ctp != ChangedTransformProperties.Unset)
                    {
                        //Apply any changed values.
                        if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.Position))
                            nob.transform.position = reader.ReadVector3();
                        if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.Rotation))
                            nob.transform.rotation = reader.ReadQuaternion(base.NetworkManager.ServerManager.SpawnPacking.Rotation);
                        if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.LocalScale))
                            nob.transform.localScale = reader.ReadVector3();
                    }
                }

                return nob;
            }
            //Not found in despawned. Shouldn't ever happen.
            else
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"SceneId of {sceneId} not found in SceneObjects. This may occur if your scene differs between client and server, if client does not have the scene loaded, or if networked scene objects do not have a SceneCondition. See ObserverManager in the documentation for more on conditions.");
                return null;
            }
        }

        /// <summary>
        /// Finishes reading a spawned object, and instantiates the object.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectId"></param>
        /// <param name="owner"></param>
        private NetworkObject ReadSpawnedObject(PooledReader reader, int objectId)
        {
            //Parent.
            SpawnParentType spt = (SpawnParentType)reader.ReadByte();
            Transform parentTransform = null;
            if (spt == SpawnParentType.NetworkObject)
            {
                NetworkObject n = reader.ReadNetworkObject();
                if (n != null)
                    parentTransform = n.transform;
            }
            else if (spt == SpawnParentType.NetworkBehaviour)
            {
                NetworkBehaviour n = reader.ReadNetworkBehaviour();
                if (n != null)
                    parentTransform = n.transform;
            }

            short prefabId = reader.ReadInt16();
            Vector3 position = reader.ReadVector3();
            Quaternion rotation = reader.ReadQuaternion(base.NetworkManager.ServerManager.SpawnPacking.Rotation);
            Vector3 localScale = reader.ReadVector3();

            NetworkObject result = null;

            if (prefabId == -1)
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Spawned object has an invalid prefabId. Make sure all objects which are being spawned over the network are within SpawnableObjects on the NetworkManager.");
            }
            else
            {
                //Only instantiate if not host.
                if (!base.NetworkManager.IsHost)
                {
                    NetworkObject prefab = NetworkManager.SpawnablePrefabs.GetObject(false, prefabId);
                    result = MonoBehaviour.Instantiate<NetworkObject>(prefab, position, rotation);
                    result.transform.SetParent(parentTransform, true);
                    //result.transform.position = position;
                    //result.transform.rotation = rotation;
                    result.transform.localScale = localScale;
                }
                //If host then find server instantiated object.
                else
                {
                    NetworkManager.ServerManager.Objects.Spawned.TryGetValueIL2CPP(objectId, out result);
                }
            }

            return result;
        }

    }

}