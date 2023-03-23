using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Serializing;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Cached expected level of detail value.
        /// </summary>
        private uint _cachedLevelOfDetailInterval;
        /// <summary>
        /// Cached value of UseLod.
        /// </summary>
        private bool _cachedUseLod;
        #endregion

        /// <summary>
        /// Parses a received network LOD update.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseNetworkLODUpdate(PooledReader reader, NetworkConnection conn)
        {
            if (!conn.Authenticated)
                return;
            if (!NetworkManager.ObserverManager.GetUseNetworkLod())
            {
                conn.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection [{conn.ClientId}] sent a level of detail update when the feature is not enabled.");
                return;
            }

            /* If local client then read out entries but do nothing.
             * Local client doesn't technically have to send LOD because
             * it's set on the client side but this code is kept in
             * to simulate actual bandwidth. */
            if (conn.IsLocalClient)
            {
                int w = reader.ReadInt32();
                for (int i = 0; i < w; i++)
                    ReadLod(out _, out _);
                return;
            }

            uint packetTick = conn.LastPacketTick;
            //Check if conn can send LOD.
            uint lastLod = conn.LastLevelOfDetailUpdate;
            //If previously set see if client is potentially exploiting.
            if (lastLod != 0)
            {
                if ((packetTick - lastLod) < _cachedLevelOfDetailInterval)
                {
                    conn.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection [{conn.ClientId}] sent excessive level of detail updates.");
                    return;
                }
            }
            //Set last recv lod.
            conn.LastLevelOfDetailUpdate = packetTick;

            //Get server objects to save calls.
            Dictionary<int, NetworkObject> serverObjects = Objects.Spawned;
            //Get level of details for this connection and reset them.
            Dictionary<NetworkObject, byte> levelOfDetails = conn.LevelOfDetails;

            int written = reader.ReadInt32();

            /* //TODO There is still an instance where client could simply say no LODs need
             * updating and never update for their objects in the first place. This can be resolved
             * by adding an observed object count to each connection and compare that to
             * the size of the LOD collection. */

            //Only process if some are written.
            if (written > 0)
            {
                //Maximum infractions before a kick.
                const int maximumInfractions = 15;
                int currentInfractions = conn.LevelOfDetailInfractions;
                int infractionsCounted = 0;

                /* If the connection has no objects then LOD isn't capable
                 * of being calculated. It's possible the players object was destroyed after
                 * the LOD sent but we don't know for sure without adding extra checks.
                 * Rather than add recently destroyed player object checks if there are
                 * no player objects then just add an infraction. The odds of this happening regularly
                 * are pretty slim. */
                if (conn.Objects.Count == 0)
                {
                    if (AddInfraction(3))
                    {
                        conn.Kick(reader, KickReason.UnusualActivity, LoggingType.Common, $"Connection [{conn.ClientId}] has sent an excessive number of level of detail updates without having any player objects spawned.");
                        return;
                    }
                }

                /* If written is more than spawned + recently despawned then
                 * the client is likely trying to exploit. */
                if (written > (Objects.Spawned.Count + Objects.RecentlyDespawnedIds.Count))
                {
                    conn.Kick(reader, KickReason.UnusualActivity, LoggingType.Common, $"Connection [{conn.ClientId}] sent a level of detail update for {written} items which exceeds spawned and recently despawned count.");
                    return;
                }

                Vector3 connObjectPosition = Vector3.zero;
                //Pick a random object from the player to sample.
                int objectIndex = UnityEngine.Random.Range(0, conn.Objects.Count);
                int connObjectIteration = 0;
                foreach (NetworkObject n in conn.Objects)
                {
                    if (connObjectIteration == objectIndex)
                    {
                        connObjectPosition = n.transform.position;
                        //Flag to indicate found.
                        objectIndex = -1;
                        break;
                    }
                }
                //Server somehow messed up. Should not be possible.
                if (objectIndex != -1)
                {
                    NetworkManager.LogError($"An object index of {objectIndex} could not be populated. Connection [{conn.ClientId}] object count is {conn.Objects.Count}.");
                    return;
                }

                //Sample at most x entries per update.
                int samplesRemaining = 10;
                //Chance to sample an update.
                const float sampleChance = 0.05f;

                List<float> lodDistances = NetworkManager.ObserverManager.GetLevelOfDetailDistances();
                int lodDistancesCount = lodDistances.Count;
                for (int i = 0; i < written; i++)
                {
                    int objectId;
                    byte lod;
                    ReadLod(out objectId, out lod);

                    //Lod is not possible.
                    if (lod >= lodDistancesCount)
                    {
                        conn.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection [{conn.ClientId}] provided a level of detail index which is out of bounds.");
                        return;
                    }

                    //Found in spawned, update lod.
                    if (serverObjects.TryGetValue(objectId, out NetworkObject nob))
                    {
                        //Value is unchanged.
                        if (levelOfDetails.TryGetValue(nob, out byte oldLod))
                        {
                            bool oldMatches = (oldLod == lod);
                            if (oldMatches && AddInfraction())
                            {
                                conn.Kick(reader, KickReason.UnusualActivity, LoggingType.Common, $"Connection [{conn.ClientId}] has excessively sent unchanged LOD information.");
                                return;
                            }
                        }
                        //If to sample.
                        if (samplesRemaining > 0 && UnityEngine.Random.Range(0f, 1f) <= sampleChance)
                        {
                            samplesRemaining--;
                            /* Only check if lod is less than maximum.
                             * If the client is hacking lods to specify maximum
                             * they are only doing the server a favor and hurting
                             * themselves with slower updates. */
                            if (lod < (lodDistancesCount - 1))
                            {
                                float specifiedLodDistance = lodDistances[lod];
                                float sqrMag = Vector3.SqrMagnitude(connObjectPosition - nob.transform.position);
                                /* If the found distance is actually larger than what client specified
                                 * then it's possible client may be sending fake LODs. */
                                if (sqrMag > specifiedLodDistance)
                                {
                                    if (AddInfraction())
                                    {
                                        conn.Kick(reader, KickReason.UnusualActivity, LoggingType.Common, $"Connection [{conn.ClientId}] provided an excessive number of incorrect LOD values.");
                                        return;
                                    }
                                }
                            }
                        }

                        levelOfDetails[nob] = lod;
                    }
                    //Not found in spawn; validate that client isn't trying to exploit.
                    else
                    {
                        //Too many infractions.
                        if (AddInfraction())
                        {
                            conn.Kick(reader, KickReason.UnusualActivity, LoggingType.Common, $"Connection [{conn.ClientId}] has accumulated excessive level of detail infractions.");
                            return;
                        }
                    }
                }
 
                //Adds an infraction returning if maximum infractions have been exceeded.
                bool AddInfraction(int count = 1)
                {
                    /* Only increase infractions at most 3 per iteration.
                    * This is to prevent a kick if the client perhaps had
                    * a massive lag spike. */
                    if (infractionsCounted < 3)
                        infractionsCounted += count;

                    bool overLimit = ((currentInfractions + infractionsCounted) >= maximumInfractions);
                    return overLimit;
                }
            }

            //Reads a LOD.
            void ReadLod(out int lObjectId, out byte lLod)
            {
                lObjectId = reader.ReadNetworkObjectId();
                lLod = reader.ReadByte();
            }

            //Remove an infraction. This will steadily remove infractions over time.
            if (conn.LevelOfDetailInfractions > 0)
                conn.LevelOfDetailInfractions--;
        }


    }


}
