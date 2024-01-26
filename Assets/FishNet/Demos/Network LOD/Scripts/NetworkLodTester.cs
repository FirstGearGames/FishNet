using FishNet.Managing.Observing;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;


namespace FishNet.Demo.NetworkLod
{

    public class NetworkLodTester : NetworkBehaviour
    {
        [Header("General")]
        [SerializeField]
        private NetworkObject _prefab;
        [SerializeField]
        private ObserverManager _observerManager;
        [Range(1, 8)]
        [SerializeField]
        private byte _lodLevel = 8;

        [Header("Spawning")]
        [SerializeField]
        private int _count = 500;
        [SerializeField]
        private float _xyRange = 15f;
        [SerializeField]
        private float _zRange = 100f;

        private void Awake()
        {
            //Check for pro...this will stay false if not on a pro package.
            bool isPro = false;
            
            if (!isPro)
            {
                Debug.LogError($"Network Level of Detail demo requires Fish-Networking Pro to work.");
                DestroyImmediate(this);
                return;
            }

            List<float> distances = _observerManager.GetLevelOfDetailDistances();
            while (distances.Count > _lodLevel)
                distances.RemoveAt(distances.Count - 1);
        }

        public override void OnStartServer()
        {
            //Spawn objects going down the range to make it easier to debug.
            float xySpacing = (_xyRange / _count);
            float zSpacing = (_zRange / _count);
            float x = 0f;
            float y = 0f;
            float z = 0f;

            for (int i = 0; i < _count; i++)
            {
                //Z cannot be flipped.
                float nextZ = z;

                x += xySpacing;
                y += xySpacing;
                z += zSpacing;
                float nextX = 0;
                float nextY = 0;
                Vector3 position = new Vector3(nextX, nextY, nextZ);
                NetworkObject obj = Instantiate(_prefab, position, Quaternion.identity);
                base.Spawn(obj);
            }
        }


    }

}