using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        #region Internal.
        /// <summary>
        /// How many ticks between each LOD update.
        /// </summary>
        public uint LevelOfDetailInterval => NetworkManager.TimeManager.TimeToTicks(0.5d, TickRounding.RoundUp);
        #endregion

        #region Private.
        /// <summary>
        /// Positions of the player objects.
        /// </summary>
        private List<Vector3> _objectsPositionsCache = new List<Vector3>();
        /// <summary>
        /// Next index within Spawned to update the LOD on.
        /// </summary>
        private int _nextLodNobIndex;
        #endregion

        /// <summary>
        /// Sends a level of update if conditions are met.
        /// </summary>
        /// <param name="forceFullUpdate">True to force send a full update immediately.
        /// This may be useful when teleporting clients.
        /// This must be called by on the server by using ServerManager.ForceLodUpdate(NetworkConnection).
        /// </param>
        internal void SendLodUpdate(bool forceFullUpdate)
        {
            if (!Connection.Authenticated)
                return;
            NetworkManager nm = NetworkManager;
            if (forceFullUpdate)
            {
                nm.LogError($"ForceFullUpdate is not yet implemented. Setting this true should not be possible.");
                return;
            }
            if (!nm.ObserverManager.GetUseNetworkLod())
                return;

            //Interval check.
            uint localTick = nm.TimeManager.LocalTick;
            uint intervalRequirement = LevelOfDetailInterval;
            bool intervalMet = ((localTick - Connection.LastLevelOfDetailUpdate) >= intervalRequirement);
            if (!forceFullUpdate && !intervalMet)
                return;

            //Set next tick.
            Connection.LastLevelOfDetailUpdate = localTick;

            List<NetworkObject> localClientSpawned = nm.ClientManager.Objects.LocalClientSpawned;
            int spawnedCount = localClientSpawned.Count;
            if (spawnedCount == 0)
                return;

            //Rebuild position cache for players objects.
            _objectsPositionsCache.Clear();
            foreach (NetworkObject playerObjects in Connection.Objects)
                _objectsPositionsCache.Add(playerObjects.transform.position);

            /* Set the maximum number of entries per send.
             * Each send is going to be approximately 3 bytes
             * but sometimes can be 4. Calculate based off the maximum
             * possible bytes. */
            //int mtu = NetworkManager.TransportManager.GetMTU((byte)Channel.Reliable);
            const int estimatedMaximumIterations = ( 400 / 4);
            /* Aim to process all objects over at most 10 seconds.
             * To reduce the number of packets sent objects are
             * calculated ~twice a second. This means if the client had
             * 1000 objects visible to them they would need to process
            * 100 objects a second, so 50 objects every half a second.
            * This should be no problem even on slower mobile devices. */
            int iterations;
            //Normal update.
            if (!forceFullUpdate)
            {
                iterations = Mathf.Min(spawnedCount, estimatedMaximumIterations);
            }
            //Force does a full update.
            else
            {
                _nextLodNobIndex = 0;
                iterations = spawnedCount;
            }

            //Cache a few more things.
            Dictionary<NetworkObject, byte> currentLods = Connection.LevelOfDetails;
            List<float> lodDistances = NetworkManager.ObserverManager.GetLevelOfDetailDistances();

            //Index to use next is too high so reset it.
            if (_nextLodNobIndex >= spawnedCount)
                _nextLodNobIndex = 0;
            int nobIndex = _nextLodNobIndex;

            PooledWriter tmpWriter = WriterPool.GetWriter(1000);
            int written = 0;

            //Only check if player has objects.
            if (_objectsPositionsCache.Count > 0)
            {
                for (int i = 0; i < iterations; i++)
                {
                    NetworkObject nob = localClientSpawned[nobIndex];
                    //Somehow went null. Can occur perhaps if client destroys objects between ticks maybe.
                    if (nob == null)
                    {
                        IncreaseObjectIndex();
                        continue;
                    }
                    //Only check objects not owned by the local client.
                    if (!nob.IsOwner && !nob.IsDeinitializing)
                    {
                        Vector3 nobPosition = nob.transform.position;
                        float closestDistance = float.MaxValue;
                        foreach (Vector3 objPosition in _objectsPositionsCache)
                        {
                            float dist = Vector3.SqrMagnitude(nobPosition - objPosition);
                            if (dist < closestDistance)
                                closestDistance = dist;
                        }

                        //If not within any distances then max lod will be used, the value below.
                        byte lod = (byte)(lodDistances.Count - 1);
                        for (byte z = 0; z < lodDistances.Count; z++)
                        {
                            //Distance is within range of this lod.
                            if (closestDistance <= lodDistances[z])
                            {
                                lod = z;
                                break;
                            }
                        }

                        bool changed;
                        /* See if value changed. Value is changed
                         * if it's not the same of old or if
                         * the nob has not yet been added to the 
                         * level of details collection. 
                         * Even if a forced update only delta
                         * needs to send. */
                        if (currentLods.TryGetValue(nob, out byte oldLod))
                            changed = (oldLod != lod);
                        else
                            changed = true;

                        //If changed then set new value and write.
                        if (changed)
                        {
                            currentLods[nob] = lod;
                            tmpWriter.WriteNetworkObjectId(nob.ObjectId);
                            tmpWriter.WriteByte(lod);
                            written++;
                        }
                    }

                    IncreaseObjectIndex();
                    
                    void IncreaseObjectIndex()
                    {
                        nobIndex++;
                        if (nobIndex >= spawnedCount)
                            nobIndex = 0;
                    }
                }
            }

            //Set next lod index to current nob index.
            _nextLodNobIndex = nobIndex;
            /* Send using the reliable channel since
             * we are using deltas. This is also why
             * updates are sent larger chunked twice a second rather
             * than smaller chunks regularly. */
            PooledWriter writer = WriterPool.GetWriter(1000);
            writer.WritePacketId(PacketId.NetworkLODUpdate);
            writer.WriteInt32(written);
            writer.WriteArraySegment(tmpWriter.GetArraySegment());
            NetworkManager.TransportManager.SendToServer((byte)Channel.Reliable, writer.GetArraySegment(), true);

            //Dispose writers.
            writer.DisposeLength();
            tmpWriter.DisposeLength();
        }


    }


}
