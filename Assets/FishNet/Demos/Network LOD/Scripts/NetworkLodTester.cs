using FishNet.Managing.Observing;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;


namespace FishNet.Demo.NetworkLod
{

    public class NetworkLodTester : NetworkBehaviour
    {
        [SerializeField]
        private NetworkObject _prefab;
        [SerializeField]
        private ObserverManager _observerManager;
        [Range(1, 8)]
        [SerializeField]
        private byte _lodLevel = 8;

        private const int _count = 500;
        private const float _xyRange = 15f;
        private const float _zRange = 100f;

        private void Awake()
        {
            List<float> distances = _observerManager.GetLevelOfDetailDistances();
            while (distances.Count > _lodLevel)
                distances.RemoveAt(distances.Count - 1);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            for (int i = 0; i < _count; i++)
            {
                float x = Random.Range(-_xyRange, _xyRange);
                float y = Random.Range(-_xyRange, _xyRange);
                float z = Random.Range(0f, _zRange);

                Vector3 position = new Vector3(x, y, z);
                NetworkObject obj = Instantiate(_prefab, position, Quaternion.identity);
                obj.name = $"Obj {i.ToString("0000")}";
                base.Spawn(obj);
            }

        }

    }

}