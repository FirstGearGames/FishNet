using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {


        /// <summary>
        /// Parses a received network LOD update.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseNetworkLODUpdate(PooledReader reader, NetworkConnection conn)
        {
            if (!conn.Authenticated)
                return;

            Debug.ClearDeveloperConsole();

            //Get server objects to save calls.
            Dictionary<int, NetworkObject> serverObjects = Objects.Spawned;
            //Get level of details for this connection and reset them.
            Dictionary<NetworkObject, byte> levelOfDetails = conn.LevelOfDetails;
            levelOfDetails.Clear();

            //Number of entries.
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int objectId = reader.ReadNetworkObjectId();
                byte lod = reader.ReadByte();
                if (serverObjects.TryGetValue(objectId, out NetworkObject nob))
                {
                    levelOfDetails[nob] = lod;
                   // Debug.Log($"Level {lod}, Object {nob.name}");
                }
            }
        }


    }


}
