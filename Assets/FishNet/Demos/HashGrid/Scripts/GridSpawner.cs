using FishNet.Object;
using UnityEngine;


namespace FishNet.Demo.HashGrid
{

    public class GridSpawner : NetworkBehaviour
    {
        [SerializeField]
        private NetworkObject _staticPrefab;
        [SerializeField]
        private NetworkObject _movingPrefab;
        [SerializeField]
        private int _movingCount = 100;
        [SerializeField]
        private byte _spacing = 2;

        private float _range => MoveRandomly.Range;

        public override void OnStartServer()
        {
            

            for (int x = (int)(_range * -1); x < _range; x+= _spacing)
            {
                for (int y = (int)(_range * -1); y < _range; y++)
                {
                    NetworkObject n = Instantiate(_staticPrefab, new Vector3(x, y, transform.position.z), Quaternion.identity);
                    base.Spawn(n);
                }
            }

            for (int i = 0; i < _movingCount; i++)
            {
                NetworkObject n = Instantiate(_movingPrefab, transform.position, transform.rotation);
                base.Spawn(n);
            }
        }

    }

}