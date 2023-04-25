using UnityEngine;

namespace FishNet.Object
{
    public sealed partial class NetworkObject : MonoBehaviour
    {
        public bool TryGetNetworkBehaviour(byte componentIndex, out NetworkBehaviour behaviour)
        {
            behaviour = null;
            if (componentIndex >= NetworkBehaviours.Length)
                return false;
            behaviour = NetworkBehaviours[componentIndex];
            return true;
        }
        public bool TryGetNetworkBehaviour<T>(out T behaviour) where T : NetworkBehaviour
        {
            behaviour = null;
            foreach (var b in NetworkBehaviours)
            {
                if (b is T target)
                {
                    behaviour = target;
                    return true;
                }
            }
            return false;
        }
    }
}

