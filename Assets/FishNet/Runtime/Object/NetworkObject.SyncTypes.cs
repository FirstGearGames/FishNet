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
            foreach (NetworkBehaviour nb in NetworkBehaviours)
            {
                if (nb != null)
                {
                    nb.WriteDirtySyncTypes(true, true);
                    nb.WriteDirtySyncTypes(false, true);
                }
            }
        }
    }

}

