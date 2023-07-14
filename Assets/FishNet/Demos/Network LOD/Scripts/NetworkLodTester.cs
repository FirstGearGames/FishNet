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

                float nextX = RandomlyFlipFloat(x);
                float nextY = RandomlyFlipFloat(y);
                //Z cannot be flipped.
                float nextZ = z;

                x += xySpacing;
                y += xySpacing;
                z += zSpacing;
                nextX = 0;
                nextY = 0;
                Vector3 position = new Vector3(nextX, nextY, nextZ);
                NetworkObject obj = Instantiate(_prefab, position, Quaternion.identity);
                base.Spawn(obj);
            }

            float RandomlyFlipFloat(float a)
            {
                if (Random.Range(0f, 1f) <= 0.5f)
                    return -a;
                else
                    return a;
            }

        }


    }

}