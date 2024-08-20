using FishNet.Object;
using UnityEngine;


namespace FishNet.Demo.Benchmarks.NetworkTransforms
{
    public class PrefabSpawner : NetworkBehaviour
    {
        [Header("General")]
        [SerializeField]
        private NetworkObject _prefab;

        [Header("Spawning")]
        [SerializeField]
        private int _count = 500;

        // [SerializeField]
        // private float _xyRange = 15f;
        // [SerializeField]
        // private float _zRange = 100f;
        public override void OnStartServer()
        {
            if (_prefab == null)
            {
                Debug.LogError($"Prefab is null.");
                return;
            }

            NetworkObject prefab = _prefab;
            Vector3 currentPosition = transform.position;
            
            for (int i = 0; i < _count; i++)
            {
                NetworkObject nob = Instantiate(prefab, currentPosition, Quaternion.identity);
                base.Spawn(nob);
            }
        }
    }
}