#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using GameKit.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace FishNet.Managing.Object
{
    public abstract partial class ManagedObjects
    {
        /// <summary>
        /// Reads and outputs a transforms values.
        /// </summary>
        protected void ReadTransformProperties(Reader reader, out Vector3? localPosition, out Quaternion? localRotation, out Vector3? localScale)
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
                localRotation = reader.ReadQuaternion(NetworkManager.ServerManager.SpawnPacking.Rotation);
            else
                localRotation = null;
            //Scale.
            if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalScale))
                localScale = reader.ReadVector3();
            else
                localScale = null;
        }

        /// <summary>
        /// Writes a spawn to clients.
        /// </summary>
        internal void WriteSpawn_Server(NetworkObject nob, NetworkConnection connection, Writer writer)
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
            PooledWriter headerWriter = WriterPool.Retrieve();
            headerWriter.WritePacketId(PacketId.ObjectSpawn);
            headerWriter.WriteNetworkObjectForSpawn(nob);
            if (NetworkManager.ServerManager.ShareIds || connection == nob.Owner)
                headerWriter.WriteNetworkConnection(nob.Owner);
            else
                headerWriter.WriteInt16(-1);

            bool nested = (nob.CurrentParentNetworkObject != null);

            bool sceneObject = nob.IsSceneObject;
            //Write type of spawn.
            SpawnType st = SpawnType.Unset;
            if (sceneObject)
                st |= SpawnType.Scene;
            else
                st |= (nob.IsGlobal) ? SpawnType.InstantiatedGlobal : SpawnType.Instantiated;
            //Add on nested if needed.
            if (nested)
                st |= SpawnType.Nested;

            headerWriter.WriteByte((byte)st);
            //ComponentIndex for the nob. 0 is root but more appropriately there's a IsNested boolean as shown above.
            headerWriter.WriteByte(nob.ComponentIndex);
            //Properties on the transform which diff from serialized value.
            WriteChangedTransformProperties(nob, sceneObject, nested, headerWriter);

            /* When nested the parent nob needs to be written. */
            if (nested)
                headerWriter.WriteNetworkObjectId(nob.CurrentParentNetworkObject);

            /* Writing a scene object. */
            if (sceneObject)
            {
                headerWriter.WriteUInt64(nob.SceneId, AutoPackType.Unpacked);
#if DEVELOPMENT
                //Check to write additional information if a scene object.
                if (NetworkManager.DebugManager.WriteSceneObjectDetails)
                {
                    headerWriter.WriteString(nob.gameObject.scene.name);
                    headerWriter.WriteString(nob.gameObject.name);
                }
#endif
            }
            /* Writing a spawned object. */
            else
            {
                //Check to write parent behaviour or nob.
                NetworkBehaviour parentNb;
                Transform t = nob.transform.parent;
                if (t != null)
                {
                    parentNb = t.GetComponent<NetworkBehaviour>();
                    /* Check for a NetworkObject if there is no NetworkBehaviour.
                     * There is a small chance the parent object will only contain
                     * a NetworkObject. */
                    if (parentNb == null)
                    {
                        //If null check if there is a nob.
                        NetworkObject parentNob = t.GetComponent<NetworkObject>();
                        //ParentNob is null or not spawned.
                        if (!ParentIsSpawned(parentNob))
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.Unset);
                        }
                        else
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.NetworkObject);
                            headerWriter.WriteNetworkObjectId(parentNob);
                        }
                    }
                    //NetworkBehaviour found on parent.
                    else
                    {
                        //ParentNb is null or not spawned.
                        if (!ParentIsSpawned(parentNb.NetworkObject))
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.Unset);
                        }
                        else
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.NetworkBehaviour);
                            headerWriter.WriteNetworkBehaviour(parentNb);
                        }
                    }

                    //True if pNob is not null, and is spawned.
                    bool ParentIsSpawned(NetworkObject pNob)
                    {
                        bool isNull = (pNob == null);
                        if (isNull || !pNob.IsSpawned)
                        {
                            /* Only log if pNob exist. Otherwise this would print if the user 
                             * was parenting any object, which may not be desirable as they could be
                             * simply doing it for organization reasons. */
                            if (!isNull)
                                NetworkManager.LogWarning($"Parent {t.name} is not spawned. {nob.name} will not have it's parent sent in the spawn message.");
                            return false;
                        }

                        return true;
                    }

                }
                //No parent.
                else
                {
                    headerWriter.WriteByte((byte)SpawnParentType.Unset);
                }

                headerWriter.WriteNetworkObjectId(nob.PrefabId);
            }

            //Write headers first.
            writer.WriteBytes(headerWriter.GetBuffer(), 0, headerWriter.Length);

            /* Used to write latest data which must be sent to
             * clients, such as SyncTypes and RpcLinks. */
            PooledWriter tempWriter = WriterPool.Retrieve();
            //Send RpcLinks first.
            foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                nb.WriteRpcLinks(tempWriter);
            //Send links to everyone.
            writer.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);

            tempWriter.Reset();
            foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                nb.WriteSyncTypesForSpawn(tempWriter, connection);
            writer.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);

            //Dispose of writers created in this method.
            headerWriter.Store();
            tempWriter.Store();
        }

        /// <summary>
        /// Writes changed transform proeprties to writer.
        /// </summary>
        protected void WriteChangedTransformProperties(NetworkObject nob, bool sceneObject, bool nested, Writer headerWriter)
        {
            /* Write changed transform properties. */
            ChangedTransformProperties ctp;
            //If a scene object then get it from scene properties.
            if (sceneObject || nested)
            {
                ctp = nob.GetTransformChanges(nob.SerializedTransformProperties);
            }
            else
            {
                PrefabObjects po = NetworkManager.GetPrefabObjects<PrefabObjects>(nob.SpawnableCollectionId, false);
                ctp = nob.GetTransformChanges(po.GetObject(true, nob.PrefabId).gameObject);
            }

            headerWriter.WriteByte((byte)ctp);
            //If properties have changed.
            if (ctp != ChangedTransformProperties.Unset)
            {
                //Write any changed properties.
                if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalPosition))
                    headerWriter.WriteVector3(nob.transform.localPosition);
                if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalRotation))
                    headerWriter.WriteQuaternion(nob.transform.localRotation, NetworkManager.ServerManager.SpawnPacking.Rotation);
                if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalScale))
                    headerWriter.WriteVector3(nob.transform.localScale);
            }

        }

        /// <summary>
        /// Writes a despawn.
        /// </summary>
        /// <param name="nob"></param>
        protected void WriteDespawn(NetworkObject nob, DespawnType despawnType, Writer everyoneWriter)
        {
            everyoneWriter.WritePacketId(PacketId.ObjectDespawn);
            everyoneWriter.WriteNetworkObjectForDespawn(nob, despawnType);
        }

        /// <summary>
        /// Gets transform properties by applying passed in values if they are not null, otherwise using transforms defaults.
        /// </summary>
        internal void GetTransformProperties(Vector3? readPos, Quaternion? readRot, Vector3? readScale, Transform defaultTransform, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            pos = (readPos == null) ? defaultTransform.localPosition : readPos.Value;
            rot = (readRot == null) ? defaultTransform.localRotation : readRot.Value;
            scale = (readScale == null) ? defaultTransform.localScale : readScale.Value;
        }

        /// <summary>
        /// Finds a scene NetworkObject and sets transform values.
        /// </summary>
#if DEVELOPMENT
        internal NetworkObject GetSceneNetworkObject(ulong sceneId, string sceneName, string objectName)
#else
        internal NetworkObject GetSceneNetworkObject(ulong sceneId)
#endif
        {
            NetworkObject nob;
            SceneObjects_Internal.TryGetValueIL2CPP(sceneId, out nob);
            //If found in scene objects.
            if (nob == null)
            {
#if DEVELOPMENT
                string missingObjectDetails = (sceneName == string.Empty) ? "For more information on the missing object add DebugManager to your NetworkManager and enable WriteSceneObjectDetails"
                    : $"Scene containing the object is '{sceneName}', object name is '{objectName}";
                NetworkManager.LogError($"SceneId of {sceneId} not found in SceneObjects. {missingObjectDetails}. This may occur if your scene differs between client and server, if client does not have the scene loaded, or if networked scene objects do not have a SceneCondition. See ObserverManager in the documentation for more on conditions.");
#else
                NetworkManager.LogError($"SceneId of {sceneId} not found in SceneObjects. This may occur if your scene differs between client and server, if client does not have the scene loaded, or if networked scene objects do not have a SceneCondition. See ObserverManager in the documentation for more on conditions.");
#endif
            }
            return nob;
        }

        /// <summary>
        /// Returns if a NetworkObject meets basic criteria for being predicted spawned.
        /// </summary>
        /// <param name="reader">If not null reader will be cleared on error.</param>
        /// <returns></returns>
        protected bool CanPredictedSpawn(NetworkObject nob, NetworkConnection spawner, NetworkConnection owner, bool asServer, Reader reader = null)
        {
            //Does not allow predicted spawning.
            if (!nob.AllowPredictedSpawning)
            {
                if (asServer)
                    spawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {spawner.ClientId} tried to spawn an object {nob.name} which does not support predicted spawning.");
                else
                    NetworkManager.LogError($"Object {nob.name} does not support predicted spawning. Modify the NetworkObject component settings to allow predicted spawning.");

                reader?.Clear();
                return false;
            }
            //Parenting is not yet supported.
            if (nob.transform.parent != null)
            {
                if (asServer)
                    spawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {spawner.ClientId} tried to spawn an object that is not root.");
                else
                    NetworkManager.LogError($"Predicted spawning as a child is not supported.");

                reader?.Clear();
                return false;
            }
            //Nested nobs not yet supported.
            if (nob.ChildNetworkObjects.Count > 0)
            {
                if (asServer)
                    spawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {spawner.ClientId} tried to spawn an object {nob.name} which has nested NetworkObjects.");
                else
                    NetworkManager.LogError($"Predicted spawning prefabs which contain nested NetworkObjects is not yet supported but will be in a later release.");

                reader?.Clear();
                return false;
            }
            //Blocked by PredictedSpawn settings or user logic.
            if ((asServer && !nob.PredictedSpawn.OnTrySpawnServer(spawner, owner))
                || (!asServer && !nob.PredictedSpawn.OnTrySpawnClient()))
            {
                reader?.Clear();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns if a NetworkObject meets basic criteria for being predicted despawned.
        /// </summary>
        /// <param name="reader">If not null reader will be cleared on error.</param>
        /// <returns></returns>
        protected bool CanPredictedDespawn(NetworkObject nob, NetworkConnection despawner, bool asServer, Reader reader = null)
        {
            //Does not allow predicted spawning.
            if (!nob.AllowPredictedDespawning)
            {
                if (asServer)
                    despawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {despawner.ClientId} tried to despawn an object {nob.name} which does not support predicted despawning.");
                else
                    NetworkManager.LogError($"Object {nob.name} does not support predicted despawning. Modify the PredictedSpawn component settings to allow predicted despawning.");

                reader?.Clear();
                return false;
            }
            //Parenting is not yet supported.
            if (nob.transform.parent != null)
            {
                if (asServer)
                    despawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {despawner.ClientId} tried to despawn an object that is not root.");
                else
                    NetworkManager.LogError($"Predicted despawning as a child is not supported.");

                reader?.Clear();
                return false;
            }
            //Nested nobs not yet supported.
            if (nob.ChildNetworkObjects.Count > 0)
            {
                if (asServer)
                    despawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {despawner.ClientId} tried to despawn an object {nob.name} which has nested NetworkObjects.");
                else
                    NetworkManager.LogError($"Predicted despawning prefabs which contain nested NetworkObjects is not yet supported but will be in a later release.");

                reader?.Clear();
                return false;
            }
            //Blocked by PredictedSpawn settings or user logic.
            if (
                (asServer && !nob.PredictedSpawn.OnTryDepawnServer(despawner))
                || (!asServer && !nob.PredictedSpawn.OnTryDespawnClient())
                )
            {
                reader?.Clear();
                return false;
            }

            return true;
        }





    }
}

