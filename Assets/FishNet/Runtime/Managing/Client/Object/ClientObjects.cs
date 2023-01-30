using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Server;
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
                        n.InvokeStopCallbacks(false);
                }
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
            SceneFN.GetSceneNetworkObjects(s, false, out nobs);

            for (int i = 0; i < nobs.Written; i++)
            {
                NetworkObject nob = nobs.Collection[i];
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
            int objectId = reader.ReadNetworkObjectForSpawn(out initializeOrder, out collectionId);
            int ownerId = reader.ReadNetworkConnectionId();
            SpawnType st = (SpawnType)reader.ReadByte();
            byte componentIndex = reader.ReadByte();

            //Read transform values which differ from serialized values.
            Vector3? localPosition;
            Quaternion? localRotation;
            Vector3? localScale;
            ReadTransformProperties();

            bool nested = SpawnTypeEnum.Contains(st, SpawnType.Nested);
            int rootObjectId = (nested) ? reader.ReadNetworkObjectId() : -1;
            bool sceneObject = SpawnTypeEnum.Contains(st, SpawnType.Scene);

            int? parentObjectId = null;
            byte? parentComponentIndex = null;
            short? prefabId = null;
            ulong sceneId = 0;

            if (sceneObject)
                ReadSceneObject(reader, out sceneId);
            else
                ReadSpawnedObject(reader, out parentObjectId, out parentComponentIndex, out prefabId);
            //
            ArraySegment<byte> rpcLinks = reader.ReadArraySegmentAndSize();
            ArraySegment<byte> syncValues = reader.ReadArraySegmentAndSize();

            _objectCache.AddSpawn(base.NetworkManager, collectionId, objectId, initializeOrder, ownerId, st, componentIndex, rootObjectId, parentObjectId, parentComponentIndex, prefabId, localPosition, localRotation, localScale, sceneId, rpcLinks, syncValues);

            void ReadTransformProperties()
            {
                //Read changed.
                ChangedTransformProperties ctp = (ChangedTransformProperties)reader.ReadByte();
                //Position.
                if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalPosition))
                    localPosition = reader.ReadVector3();
                else
                    localPosition = null;
                //Rotation.
                if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalRotation))
                    localRotation = reader.ReadQuaternion(base.NetworkManager.ServerManager.SpawnPacking.Rotation);
                else
                    localRotation = null;
                //Scale.
                if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalScale))
                    localScale = reader.ReadVector3();
                else
                    localScale = null;
            }

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

            GetTransformProperties(cnob, nob.transform, out Vector3 pos, out Quaternion rot, out Vector3 scale);
            nob.transform.SetLocalPositionRotationAndScale(pos, rot, scale);

            return nob;
        }

        /// <summary>
        /// Finds a scene NetworkObject and sets transform values.
        /// </summary>
        internal NetworkObject GetSceneNetworkObject(CachedNetworkObject cnob)
        {
            ulong sceneId = cnob.SceneId;
            NetworkObject nob;
            base.SceneObjects.TryGetValueIL2CPP(sceneId, out nob);
            //If found in scene objects.
            if (nob != null)
            {
                Transform t = nob.transform;
                GetTransformProperties(cnob, t, out Vector3 pos, out Quaternion rot, out Vector3 scale);
                t.SetLocalPositionRotationAndScale(pos, rot, scale);
                return nob;
            }
            //Not found in scene objects. Shouldn't ever happen.
            else
            {
                NetworkManager.LogError($"SceneId of {sceneId} not found in SceneObjects. This may occur if your scene differs between client and server, if client does not have the scene loaded, or if networked scene objects do not have a SceneCondition. See ObserverManager in the documentation for more on conditions.");
                return null;
            }
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
            short prefabId = cnob.PrefabId.Value;
            NetworkObject result = null;

            if (prefabId == -1)
            {
                NetworkManager.LogError($"Spawned object has an invalid prefabId. Make sure all objects which are being spawned over the network are within SpawnableObjects on the NetworkManager.");
                return null;
            }

            //PrefabObjects to get the prefab from.
            PrefabObjects prefabObjects;
            if (cnob.CollectionId == 0)
            {
                prefabObjects = networkManager.SpawnablePrefabs;
            }
            else
            {
                prefabObjects = networkManager.GetPrefabObjects<PrefabObjects>(cnob.CollectionId, false);
                if (prefabObjects == null)
                {
                    networkManager.LogError($"PrefabObjects collection is not found for CollectionId {cnob.CollectionId}. Be sure to add your addressables NetworkObject prefabs to the collection on server and client before attempting to spawn them over the network.");
                    return null;
                }
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

                result = networkManager.GetPooledInstantiated(prefabId, false);
                Transform t = result.transform;
                t.SetParent(parentTransform, true);
                GetTransformProperties(cnob, t, out Vector3 pos, out Quaternion rot, out Vector3 scale);
                t.SetLocalPositionRotationAndScale(pos, rot, scale);
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
                    networkManager.LogError($"ObjectId {cnob.ObjectId} could not be found in Server spawned, nor Server pending despawn.");
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
        /// Gets transform properties from a CachedNetworkObject, and applying defaultTransform values if properties are not found within the cached objet.
        /// </summary>
        private void GetTransformProperties(CachedNetworkObject cnob, Transform defaultTransform, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            pos = (cnob.LocalPosition == null) ? defaultTransform.localPosition : cnob.LocalPosition.Value;
            rot = (cnob.LocalRotation == null) ? defaultTransform.localRotation : cnob.LocalRotation.Value;
            scale = (cnob.LocalScale == null) ? defaultTransform.localScale : cnob.LocalScale.Value;
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
        private void ReadSpawnedObject(PooledReader reader, out int? parentObjectId, out byte? parentComponentIndex, out short? prefabId)
        {
            //Parent.
            SpawnParentType spt = (SpawnParentType)reader.ReadByte();
            //Defaults.
            parentObjectId = null;
            parentComponentIndex = null;

            if (spt == SpawnParentType.NetworkObject)
            {
                int objectId = reader.ReadNetworkObjectId();
                if (objectId != -1)
                    parentObjectId = objectId;
            }
            else if (spt == SpawnParentType.NetworkBehaviour)
            {
                reader.ReadNetworkBehaviour(out int objectId, out byte componentIndex);
                if (objectId != -1)
                {
                    parentObjectId = objectId;
                    parentComponentIndex = componentIndex;
                }
            }

            prefabId = reader.ReadInt16();
        }

    }

}