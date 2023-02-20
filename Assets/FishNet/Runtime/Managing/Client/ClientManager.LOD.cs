using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        /// <summary>
        /// Positions of the player objects.
        /// </summary>
        private List<Vector3> _objectsPositionsCache = new List<Vector3>();

        internal void SendNetworkLODUpdate()
        {
            if (!Connection.Authenticated)
                return;

            //Rebuild position cache for players objects.
            _objectsPositionsCache.Clear();
            foreach (NetworkObject playerObjects in Connection.Objects)
                _objectsPositionsCache.Add(playerObjects.transform.position);

            PooledWriter pw = WriterPool.GetWriter(5000);
            pw.WritePacketId(PacketId.NetworkLODUpdate);

            Dictionary<NetworkObject, byte> levelOfDetails = Connection.LevelOfDetails;
            levelOfDetails.Clear();

            Dictionary<int, NetworkObject> spawned = Objects.Spawned;
            pw.WriteInt32(spawned.Count);
            foreach (NetworkObject nob in spawned.Values)
            {
                if (nob.IsOwner)
                    continue;
                Vector3 nobPosition = nob.transform.position;
                float closestDistance = float.MaxValue;
                foreach (Vector3 objPosition in _objectsPositionsCache)
                {
                    float dist = Vector3.Magnitude(nobPosition - objPosition);
                    if (dist < closestDistance)
                        closestDistance = dist;
                }

                List<float> lodDistances = NetworkManager.ObserverManager.LevelOfDetailDistances;
                //If not within any distances then max lod will be used.
                byte lod = (byte)(lodDistances.Count - 1);
                for (byte i = 0; i < lodDistances.Count; i++)
                {
                    //Distance is within range of this lod.
                    if (closestDistance <= lodDistances[i])
                    {
                        lod = i;
                        break;
                    }

                }

                levelOfDetails[nob] = lod;
                pw.WriteNetworkObjectId(nob.ObjectId);
                pw.WriteByte(lod);
            }
             
            NetworkManager.TransportManager.SendToServer((byte)Channel.Unreliable, pw.GetArraySegment(), true);
        }

    }


}
