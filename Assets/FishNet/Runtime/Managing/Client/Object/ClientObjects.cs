#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Server;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using GameKit.Utilities;
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
        #region Internal.
        /// <summary>
        /// NetworkObjects which are currently active on the local client.
        /// </summary>
        internal List<NetworkObject> LocalClientSpawned = new List<NetworkObject>();
        #endregion

        #region Private.
        /// <summary>
        /// NetworkObjects which are cached to be spawned or despawned.
        /// </summary>
        private ClientObjectCache _objectCache;
        #endregion

        internal ClientObjects(NetworkManager networkManager)
        {
            base.Initialize(networkManager);
            _objectCache = new ClientObjectCache(this, networkManager);
        }

        /// <summary>
        /// Called when a connection state changes for the local server.
        /// </summary>
        internal void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            //Nothing needs to be done if started.
            if (args.ConnectionState == LocalConnectionState.Started)
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

            /* Only perform this step if the transport being stopped
             * is the one which client is connected to. */
            if (NetworkManager.IsClient && args.TransportIndex == base.NetworkManager.ClientManager.GetTransportIndex())
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
            if (args.ConnectionState != LocalConnectionState.Started)
            {
                _objectCache.Reset();

                //If not server then deinitialize normally.
                if (!base.NetworkManager.IsServer)
                {
                    base.DespawnWithoutSynchronization(false);
                }
                //Otherwise invoke stop callbacks only for client side.
                else
                {
                    foreach (NetworkObject n in Spawned.Values)
                    { 
                        n.InvokeStopCallbacks(false);
                        n.SetInitializedStatus(false, false);
                    }
                }
                /* Clear spawned and scene objects as they will be rebuilt.
                 * Spawned would have already be cleared if DespawnSpawned
                 * was called but it won't hurt anything clearing an empty collection. */
                base.Spawned.Clear();
                base.SceneObjects_Internal.Clear();
                LocalClientSpawned.Clear();
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
        /// Adds a NetworkObject to Spawned.
        /// </summary>
        internal override void AddToSpawned(NetworkObject nob, bool asServer)
        {
            LocalClientSpawned.Add(nob);
            base.AddToSpawned(nob, asServer);
            //If being added as client and is also server.
            if (NetworkManager.IsServer)
                nob.SetRenderersVisible(true);
        }

        /// <summary>
        /// Removes a NetworkedObject from spawned.
        /// </summary>
        protected override void RemoveFromSpawned(NetworkObject nob, bool unexpectedlyDestroyed, bool asServer)
        {
            //No need to check if !asServer.
            LocalClientSpawned.Remove(nob);
            base.RemoveFromSpawned(nob, unexpectedlyDestroyed, asServer);
        }

        /// <summary>
        /// Sends a predicted spawn to the server.
        /// </summary>
        internal void PredictedSpawn(NetworkObject networkObject, NetworkConnection ownerConnection)
        {
            //No more Ids to use.
            Queue<int> predictedObjectIds = NetworkManager.ClientManager.Connection.PredictedObjectIds;
            if (predictedObjectIds.Count == 0)
            {
                NetworkManager.LogError($"Predicted spawn for object {networkObject.name} failed because no more predicted ObjectIds remain. This usually occurs when the client is spawning excessively before the server can respond. Increasing ReservedObjectIds within the ServerManager component or reducing spawn rate could prevent this problem.");
                return;
            }

            networkObject.PreinitializePredictedObject_Client(base.NetworkManager, predictedObjectIds.Dequeue(), ownerConnection, base.NetworkManager.ClientManager.Connection);
            NetworkManager.ClientManager.Objects.AddToSpawned(networkObject, false);
            networkObject.Initialize(false, true);

            PooledWriter writer = WriterPool.Retrieve();
            WriteSpawn(networkObject, writer);
            base.NetworkManager.TransportManager.SendToServer((byte)Channel.Reliable, writer.GetArraySegment());
            writer.Store();
        }

        /// <summary>
        /// Writes a predicted spawn.
        /// </summary>
        /// <param name="nob"></param>
        public void WriteSpawn(NetworkObject nob, Writer writer)
        {
            PooledWriter headerWriter = WriterPool.Retrieve();
            headerWriter.WritePacketId(PacketId.ObjectSpawn);
            headerWriter.WriteNetworkObjectForSpawn(nob);
            headerWriter.WriteNetworkConnection(nob.Owner);

            bool sceneObject = nob.IsSceneObject;
            //Write type of spawn.
            SpawnType st = SpawnType.Unset;
            if (sceneObject)
                st |= SpawnType.Scene;
            else
                st |= (nob.IsGlobal) ? SpawnType.InstantiatedGlobal : SpawnType.Instantiated;
            headerWriter.WriteByte((byte)st);

            //ComponentIndex for the nob. 0 is root but more appropriately there's a IsNested boolean as shown above.
            headerWriter.WriteByte(nob.ComponentIndex);
            //Properties on the transform which diff from serialized value.
            base.WriteChangedTransformProperties(nob, sceneObject, false, headerWriter);
            /* Writing a scene object. */
            if (sceneObject)
            {
                headerWriter.WriteUInt64(nob.SceneId, AutoPackType.Unpacked);
#if DEVELOPMENT
                base.CheckWriteSceneObjectDetails(nob, headerWriter);
#endif
            }
            /* Writing a spawned object. */
            else
            {
                //Nested predicted spawning will be added later.
                headerWriter.WriteByte((byte)SpawnParentType.Unset);
                headerWriter.WriteNetworkObjectId(nob.PrefabId);
            }

            writer.WriteBytes(headerWriter.GetBuffer(), 0, headerWriter.Length);

            //If allowed to write synctypes.
            if (nob.AllowPredictedSyncTypes)
            {
                PooledWriter tempWriter = WriterPool.Retrieve();
                foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                    nb.WriteSyncTypesForSpawn(tempWriter, null);
                writer.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);
                tempWriter.Store();
            }

            //Dispose of writers created in this method.
            headerWriter.Store();
        }


        /// <summary>
        /// Sends a predicted despawn to the server.
        /// </summary>
        internal void PredictedDespawn(NetworkObject networkObject)
        {
            PooledWriter writer = WriterPool.Retrieve();
            WriteDepawn(networkObject, writer);
            base.NetworkManager.TransportManager.SendToServer((byte)Channel.Reliable, writer.GetArraySegment());
            writer.Store();

            //Deinitialize after writing despawn so all the right data is sent.
            networkObject.DeinitializePredictedObject_Client();
        }

        /// <summary>
        /// Writes a predicted despawn.
        /// </summary>
        public void WriteDepawn(NetworkObject nob, Writer writer)
        {
            writer.WritePacketId(PacketId.ObjectDespawn);
            writer.WriteNetworkObject(nob);
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
            List<NetworkObject> nobs = CollectionCaches<NetworkObject>.RetrieveList();
            Scenes.GetSceneNetworkObjects(s, false,true, ref nobs);

            int nobsCount = nobs.Count;
            for (int i = 0; i < nobsCount; i++)
            {
                NetworkObject nob = nobs[i];
                if (!nob.IsSceneObject)
                    continue;

                base.UpdateNetworkBehavioursForSceneObject(nob, false);
                if (nob.IsNetworked && nob.IsNetworked)
                {
                    base.AddToSceneObjects(nob);
                    //Only run if not also server, as this already ran on server.
                    if (!base.NetworkManager.IsServer)
                        nob.gameObject.SetActive(false);
                }
            }

            CollectionCaches<NetworkObject>.Store(nobs);
        }

        /// <summary>
        /// Called when a NetworkObject runs Deactivate.
        /// </summary>
        /// <param name="nob"></param>
        internal override void NetworkObjectUnexpectedlyDestroyed(NetworkObject nob, bool asServer)
        {
            nob.RemoveClientRpcLinkIndexes();
            base.NetworkObjectUnexpectedlyDestroyed(nob, asServer);
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
                nob.GiveOwnership(newOwner, false);
            else
                NetworkManager.LogWarning($"NetworkBehaviour could not be found when trying to parse OwnershipChange packet.");
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
                if (dataLength > 0)
                    nb.OnSyncType(reader, dataLength, isSyncObject);
            }
            else
            {
                SkipDataLength(packetId, reader, dataLength);
            }
        }

        /// <summary>
        /// Parses a 
        /// </summary>
        /// <param name="reader"></param>
        internal void ParsePredictedSpawnResult(Reader reader)
        {
            int usedObjectId = reader.ReadNetworkObjectId();
            bool success = reader.ReadBoolean();
            if (success)
            {
                int nextObjectId = reader.ReadNetworkObjectId();
                if (nextObjectId != NetworkObject.UNSET_OBJECTID_VALUE)
                    NetworkManager.ClientManager.Connection.PredictedObjectIds.Enqueue(nextObjectId);
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
            sbyte initializeOrder;
            ushort collectionId;
            int objectId = reader.ReadNetworkObjectForSpawn(out initializeOrder, out collectionId, out _);
            int ownerId = reader.ReadNetworkConnectionId();
            SpawnType st = (SpawnType)reader.ReadByte();
            byte componentIndex = reader.ReadByte();

            //Read transform values which differ from serialized values.
            Vector3? localPosition;
            Quaternion? localRotation;
            Vector3? localScale;
            base.ReadTransformProperties(reader, out localPosition, out localRotation, out localScale);

            bool nested = SpawnTypeEnum.Contains(st, SpawnType.Nested);
            int rootObjectId = (nested) ? reader.ReadNetworkObjectId() : 0;
            bool sceneObject = SpawnTypeEnum.Contains(st, SpawnType.Scene);

            int? parentObjectId = null;
            byte? parentComponentIndex = null;
            int? prefabId = null;
            ulong sceneId = 0;
            string sceneName = string.Empty;
            string objectName = string.Empty;

            if (sceneObject)
            {
                ReadSceneObject(reader, out sceneId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                base.CheckReadSceneObjectDetails(reader, ref sceneName, ref objectName);
#endif
            }
            else
            {
                ReadSpawnedObject(reader, out parentObjectId, out parentComponentIndex, out prefabId);
            }
            ArraySegment<byte> rpcLinks = reader.ReadArraySegmentAndSize();
            ArraySegment<byte> syncValues = reader.ReadArraySegmentAndSize();

            /* If the objectId can be found as already spawned then check if it's predicted.
             * Should the spawn be predicted then no need to continue. Later on however
             * we may want to apply synctypes.
             * 
             * Only check if not server, since if server the client doesnt need
             * to predicted spawn. */
            if (!base.NetworkManager.IsServerOnly && base.Spawned.TryGetValue(objectId, out NetworkObject nob))
            {
                //If not predicted the nob should not be in spawned.
                if (!nob.PredictedSpawner.IsValid)
                {
                    NetworkManager.LogWarning($"Received a spawn objectId of {objectId} which was already found in spawned, and was not predicted. This sometimes may occur on clientHost when the server destroys an object unexpectedly before the clientHost gets the spawn message.");
                }
                //Everything is proper, apply RPC links.
                else
                {
                    PooledReader linkReader = ReaderPool.Retrieve(rpcLinks, NetworkManager);
                    ApplyRpcLinks(nob, linkReader);
                    linkReader.Store();
                }
                //No further initialization needed when predicting.
                return;
            }

            _objectCache.AddSpawn(base.NetworkManager, collectionId, objectId, initializeOrder, ownerId, st, componentIndex, rootObjectId, parentObjectId, parentComponentIndex, prefabId, localPosition, localRotation, localScale, sceneId, sceneName, objectName, rpcLinks, syncValues);
        }
        /// <summary>
        /// Caches a received despawn to be processed after all spawns and despawns are received for the tick.
        /// </summary>
        /// <param name="reader"></param>
        internal void CacheDespawn(PooledReader reader)
        {
            DespawnType despawnType;
            int objectId = reader.ReadNetworkObjectForDepawn(out despawnType);
            _objectCache.AddDespawn(objectId, despawnType);
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
        /// Gets a nested NetworkObject within it's root.
        /// </summary>
        /// <param name="cnob"></param>
        /// <returns></returns>
        internal NetworkObject GetNestedNetworkObject(CachedNetworkObject cnob)
        {
            NetworkObject rootNob;
            int rootObjectId = cnob.RootObjectId;
            byte componentIndex = cnob.ComponentIndex;

            /* Spawns are processed after all spawns come in,
             * this ensures no reference race conditions. Turns out because of this
             * the parentNob may be in cache and not actually spawned, if it was spawned the same packet
             * as this one. So when not found in the spawned collection try to
             * find it in Spawning before throwing. */
            rootNob = _objectCache.GetSpawnedObject(rootObjectId);
            //If still null, that's not good.
            if (rootNob == null)
            {
                NetworkManager.LogError($"Nested spawned object with componentIndex of {componentIndex} and a parentId of {rootObjectId} could not be spawned because parent was not found.");
                return null;
            }

            NetworkObject nob = null;
            List<NetworkObject> childNobs = rootNob.ChildNetworkObjects;
            //Find nob with component index.
            for (int i = 0; i < childNobs.Count; i++)
            {
                if (childNobs[i].ComponentIndex == componentIndex)
                {
                    nob = childNobs[i];
                    break;
                }
            }
            //If child nob was not found.
            if (nob == null)
            {
                NetworkManager.LogError($"Nested spawned object with componentIndex of {componentIndex} could not be found as a child NetworkObject of {rootNob.name}.");
                return null;
            }

            return nob;
        }

        /// <summary>
        /// Applies RPCLinks to a NetworkObject.
        /// </summary>
        internal void ApplyRpcLinks(NetworkObject nob, Reader reader)
        {
            List<ushort> rpcLinkIndexes = new List<ushort>();
            //Apply rpcLinks.
            foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
            {
                int length = reader.ReadInt32();

                int readerStart = reader.Position;
                while (reader.Position - readerStart < length)
                {
                    //Index of RpcLink.
                    ushort linkIndex = reader.ReadUInt16();
                    RpcLink link = new RpcLink(nob.ObjectId, nb.ComponentIndex,
                        //RpcHash.
                        reader.ReadUInt16(),
                        //ObserverRpc.
                        (RpcType)reader.ReadByte());
                    //Add to links.
                    SetRpcLink(linkIndex, link);
                    rpcLinkIndexes.Add(linkIndex);
                }
            }
            nob.SetRpcLinkIndexes(rpcLinkIndexes);
        }

        /// <summary>
        /// Instantiates a NetworkObject if required and sets transform values.
        /// </summary>
        internal NetworkObject GetInstantiatedNetworkObject(CachedNetworkObject cnob)
        {
            if (cnob.PrefabId == null)
            {
                NetworkManager.LogError($"PrefabId for {cnob.ObjectId} is null. Object will not spawn.");
                return null;
            }

            NetworkManager networkManager = base.NetworkManager;
            int prefabId = cnob.PrefabId.Value;
            NetworkObject result;

            if (prefabId == NetworkObject.UNSET_OBJECTID_VALUE)
            {
                NetworkManager.LogError($"Spawned object has an invalid prefabId. Make sure all objects which are being spawned over the network are within SpawnableObjects on the NetworkManager.");
                return null;
            }

            ushort collectionId = cnob.CollectionId;
            //PrefabObjects to get the prefab from.
            PrefabObjects prefabObjects = networkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
            //Not found for collectionId > 0. This means the user likely did not setup the collection on client.
            if (prefabObjects == null && collectionId > 0)
            {
                networkManager.LogError($"PrefabObjects collection is not found for CollectionId {collectionId}. Be sure to add your addressables NetworkObject prefabs to the collection on server and client before attempting to spawn them over the network.");
                return null;
            }

            //Only instantiate if not host.
            if (!networkManager.IsHost)
            {
                Transform parentTransform = null;
                bool hasParent = (cnob.ParentObjectId != null);
                //Set parentTransform if there's a parent object.
                if (hasParent)
                {
                    int objectId = cnob.ParentObjectId.Value;
                    NetworkObject nob = _objectCache.GetSpawnedObject(objectId);

                    if (nob == null)
                    {
                        NetworkObject prefab = prefabObjects.GetObject(false, prefabId);
                        networkManager.LogError($"NetworkObject not found for ObjectId {objectId}. Prefab {prefab.name} will be instantiated without parent synchronization.");
                    }
                    else
                    {
                        //If parent object is a network behaviour then find the component.
                        if (cnob.ParentIsNetworkBehaviour)
                        {
                            byte componentIndex = cnob.ComponentIndex;
                            NetworkBehaviour nb = nob.GetNetworkBehaviour(componentIndex, false);
                            if (nb != null)
                            {
                                parentTransform = nb.transform;
                            }
                            else
                            {
                                NetworkObject prefab = prefabObjects.GetObject(false, prefabId);
                                networkManager.LogError($"NetworkBehaviour on index {componentIndex} could nto be found within NetworkObject {nob.name} with ObjectId {objectId}. Prefab {prefab.name} will be instantiated without parent synchronization.");
                            }
                        }
                        //The networkObject is the parent.
                        else
                        {
                            parentTransform = nob.transform;
                        }
                    }
                }

                result = networkManager.GetPooledInstantiated(prefabId, collectionId, false);
                Transform t = result.transform;
                t.SetParent(parentTransform, true);
                //Only need to set IsGlobal also if not host.
                bool isGlobal = SpawnTypeEnum.Contains(cnob.SpawnType, SpawnType.InstantiatedGlobal);
                result.SetIsGlobal(isGlobal);
            }
            //If host then find server instantiated object.
            else
            {
                ServerObjects so = networkManager.ServerManager.Objects;
                if (!so.Spawned.TryGetValueIL2CPP(cnob.ObjectId, out result))
                    result = so.GetFromPending(cnob.ObjectId);

                if (result == null)
                    networkManager.LogWarning($"ObjectId {cnob.ObjectId} could not be found in Server spawned, nor Server pending despawn. This may occur as clientHost when objects are destroyed before the client receives a despawn packet. In most cases this may be ignored.");
            }

            return result;
        }

        /// <summary>
        /// Gets a NetworkObject from Spawned, or object cache.
        /// </summary>
        /// <param name="cnob"></param>
        /// <returns></returns>
        internal NetworkObject GetSpawnedNetworkObject(CachedNetworkObject cnob)
        {
            NetworkObject nob;
            //Try checking already spawned objects first.
            if (base.Spawned.TryGetValueIL2CPP(cnob.ObjectId, out nob))
            {
                return nob;
            }
            /* If not found in already spawned objects see if
             * the networkObject is in the objectCache. It's possible the despawn
             * came immediately or shortly after the spawn message, before
             * the object has been initialized. */
            else
            {
                nob = _objectCache.GetInCached(cnob.ObjectId, ClientObjectCache.CacheSearchType.Any);
                /* Nob may be null if it's a child object being despawned, and the
                 * parent despawn already occurred. */
                return nob;
            }
        }

        /// <summary>
        /// Finishes reading a scene object.
        /// </summary>
        private void ReadSceneObject(PooledReader reader, out ulong sceneId)
        {
            sceneId = reader.ReadUInt64(AutoPackType.Unpacked);
        }

        /// <summary>
        /// Finishes reading a spawned object, and instantiates the object.
        /// </summary>
        private void ReadSpawnedObject(PooledReader reader, out int? parentObjectId, out byte? parentComponentIndex, out int? prefabId)
        {
            //Parent.
            SpawnParentType spt = (SpawnParentType)reader.ReadByte();
            //Defaults.
            parentObjectId = null;
            parentComponentIndex = null;

            if (spt == SpawnParentType.NetworkObject)
            {
                int objectId = reader.ReadNetworkObjectId();
                if (objectId != NetworkObject.UNSET_OBJECTID_VALUE)
                    parentObjectId = objectId;
            }
            else if (spt == SpawnParentType.NetworkBehaviour)
            {
                reader.ReadNetworkBehaviour(out int objectId, out byte componentIndex, _objectCache.ReadSpawningObjects);
                if (objectId != NetworkObject.UNSET_OBJECTID_VALUE)
                {
                    parentObjectId = objectId;
                    parentComponentIndex = componentIndex;
                }
            }

            prefabId = (ushort)reader.ReadNetworkObjectId();
        }

    }

}