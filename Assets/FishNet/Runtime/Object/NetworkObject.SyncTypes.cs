using UnityEngine;

namespace FishNet.Object
{
    public sealed partial class NetworkObject : MonoBehaviour
    {
        /// <summary>
        /// Writers dirty SyncTypes for all Networkbehaviours if their write tick has been met.
        /// </summary>
        internal void WriteDirtySyncTypes()
        {
            NetworkBehaviour[] nbs = NetworkBehaviours;
            int count = nbs.Length;
            for (int i = 0; i < count; i++)
            {
                //There was a null check here before, shouldn't be needed so it was removed.
                NetworkBehaviour nb = nbs[i];
                nb.WriteDirtySyncTypes(true, true);
                nb.WriteDirtySyncTypes(false, true);
            }
        }

        /// <summary>
        /// Resets SyncTypes for all NetworkBehaviours.
        /// </summary>
        internal void ResetSyncTypes(bool asServer)
        {
            NetworkBehaviour[] nbs = NetworkBehaviours;
            int count = nbs.Length;
            for (int i = 0; i < count; i++)
                nbs[i].ResetSyncTypes(asServer);
        }
    }

}

