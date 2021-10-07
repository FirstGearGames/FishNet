using FishNet.Managing.Object;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Client.Object
{
    public class ClientObjects : ManagedObjects
    {

        #region Private.
        /// <summary>
        /// NetworkObjects which are cached to be spawned or despawned.
        /// </summary>
        private NetworkObjectCache _objectCache = null;
        #endregion

        internal ClientObjects(NetworkManager networkManager)
        {
            base.NetworkManager = networkManager;
            _objectCache = new NetworkObjectCache(networkManager);
        }

        /// <summary>
        /// Called when the connection state changes for the local client.
        /// </summary>
        /// <param name="args"></param>
        internal void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionStates.Started)
            {
                //Reset cache.
                _objectCache.Reset();

                base.DespawnSpawnedWithoutSynchronization(false);
                /* Clear spawned and scene objects as they will be rebuilt.
                 * Spawned would have already be cleared if DespawnSpawned
                 * was called but it won't hurt anything clearing an empty collection. */
                base.Spawned.Clear();
                base.SceneObjects.Clear();
            }
            else
            {
                DespawnSceneObjects();
            }
        }

        /// <summary>
        /// Despans all NetworkObjects in a scene if they are scene objects.
        /// </summary>
        private void DespawnSceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                foreach (GameObject go in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (go.TryGetComponent(out NetworkObject nob))
                    {
                        if (nob.SceneObject)
                        {
                            base.AddToSceneObjects(nob);
                            //Only run if not also server, as this already ran on server.
                            if (!base.NetworkManager.IsHost)
                                nob.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called when a scene is loaded.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="arg1"></param>
        protected override void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1)
        {
            base.SceneManager_sceneLoaded(s, arg1);

            if (!base.NetworkManager.IsClient)
                return;

            /* When a scene first loads for a client it should disable
             * all network objects in that scene. The server will send
             * spawn messages once it's aware client has loaded the scene. */
            foreach (GameObject go in s.GetRootGameObjects())
            {
                NetworkObject nob;
                if (go.TryGetComponent(out nob))
                {
                    if (nob.SceneObject)
                    {
                        base.AddToSceneObjects(nob);
                        //Only run if not also server, as this already ran on server.
                        if (!base.NetworkManager.IsHost)
                            nob.gameObject.SetActive(false);
                    }
                }
                foreach (Transform t in go.transform)
                {
                    if (go.TryGetComponent(out nob))
                    {
                        if (nob.SceneObject)
                        {
                            base.AddToSceneObjects(nob);
                            //Only run if not also server, as this already ran on server.
                            if (!base.NetworkManager.IsHost)
                                nob.gameObject.SetActive(false);
                        }
                    }
                }
            }
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
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"NetworkBehaviour could not be found when trying to parse OwnershipChange packet.");
            }
        }

        /// <summary>
        /// Parses a received syncVar.
        /// </summary>
        /// <param name="reader"></param>
        internal void ParseSyncType(PooledReader reader, bool isSyncObject, int dataLength)
        {
            int startPosition = reader.Position;
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            if (nb != null)
            {
                /* Length of data to be read.
                 * This must be known since multiple packet
                 * types can arrive in one message, and since synctype
                 * lengths may vary. */
                int length = reader.ReadInt32();
                if (length > 0)
                    nb.OnSyncType(reader, length, isSyncObject);
            }
            else
            {
                if (dataLength == -1)
                {
                    if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                        Debug.LogWarning($"NetworkBehaviour could not be found for SyncType.");
                }
                else
                {
                    reader.Position = startPosition;
                    reader.Skip(Math.Min(dataLength, reader.Remaining));
                }
            }
        }

        /// <summary>
        /// Parses an ObserversRpc.
        /// </summary>
        /// <param name="reader"></param>
        internal void ParseObserversRpc(PooledReader reader, int dataLength, Channel channel)
        {
            int startPosition = reader.Position;
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            if (nb != null)
                nb.OnObserversRpc(reader, channel);
            else
                SkipDataLength(PacketId.ObserversRpc, reader, startPosition, dataLength);
        }
        /// <summary>
        /// Parses a TargetRpc.
        /// </summary>
        /// <param name="reader"></param>
        internal void ParseTargetRpc(PooledReader reader, int dataLength, Channel channel)
        {
            int startPosition = reader.Position;
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            if (nb != null)
                nb.OnTargetRpc(reader, channel);
            else
                SkipDataLength(PacketId.TargetRpc, reader, startPosition, dataLength);
        }


        /// <summary>
        /// Caches a received spawn to be processed after all spawns and despawns are received for the tick.
        /// </summary>
        /// <param name="reader"></param>
        internal void CacheSpawn(PooledReader reader)
        {
            int objectId = reader.ReadInt16();
            int ownerId = reader.ReadInt16();
            bool sceneObject = reader.ReadBoolean();

            NetworkObject nob;
            if (sceneObject)
                nob = ReadSceneObject(reader, true);
            else
                nob = ReadSpawnedObject(reader, objectId);

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
                    if (NetworkManager.CanLog(Logging.LoggingType.Error))
                        Debug.LogError($"Spawn object could not be found or created for Id {objectId}; scene object: {sceneObject}.");
                }

                return;
            }
            /* If not host then pre-initialize. Pre-initializing applies
             * values needed to run such as owner, network manager, and completes
             * other reference creating functions. */
            if (!base.NetworkManager.IsHost)
            {
                NetworkConnection owner = new NetworkConnection(NetworkManager, ownerId);
                nob.PreInitialize(NetworkManager, objectId, owner, false);
            }

            _objectCache.AddSpawn(nob, syncValues, NetworkManager);
            base.AddToSpawned(nob);
        }

        /// <summary>
        /// Caches a received despawn to be processed after all spawns and despawns are received for the tick.
        /// </summary>
        /// <param name="reader"></param>
        internal void CacheDespawn(PooledReader reader)
        {
            int objectId = reader.ReadInt16();
            if (base.Spawned.TryGetValue(objectId, out NetworkObject nob))
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
            base.SceneObjects.TryGetValue(sceneId, out nob);
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
                            nob.transform.rotation = reader.ReadQuaternion();
                        if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.LocalScale))
                            nob.transform.localScale = reader.ReadVector3();
                    }
                }

                return nob;
            }
            //Not found in despawned. Shouldn't ever happen.
            else
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Error))
                    Debug.LogError($"SceneId of {sceneId} not found in SceneObjects.");
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
            short prefabId = reader.ReadInt16();
            Vector3 position = reader.ReadVector3();
            Quaternion rotation = reader.ReadQuaternion();
            Vector3 localScale = reader.ReadVector3();

            NetworkObject result = null;

            if (prefabId == -1)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Error))
                    Debug.LogError($"Spawned object has an invalid prefabId. Make sure all objects which are being spawned over the network are within SpawnableObjects on the NetworkManager.");
            }
            else
            {
                //Only instantiate if not host.
                if (!base.NetworkManager.IsHost)
                {
                    NetworkObject prefab = NetworkManager.SpawnablePrefabs.GetObject(false, prefabId);
                    result = MonoBehaviour.Instantiate<NetworkObject>(prefab, position, rotation);
                    result.transform.position = position;
                    result.transform.rotation = rotation;
                    result.transform.localScale = localScale;
                }
                //If host then find server instantiated object.
                else
                {
                    NetworkManager.ServerManager.Objects.Spawned.TryGetValue(objectId, out result);
                }
            }

            return result;
        }

    }

}