using UnityEngine;

namespace GameKit.Utilities.ObjectPooling.Examples
{

    public class ProjectileSpawner : MonoBehaviour
    {
        public GameObject Prefab;
        public bool UsePool = true;

        public float _instantiateDelay = 0.075f;
        private float _nextInstantiate = 0f;

        // Update is called once per frame
        void Update()
        {
            if (Time.unscaledTime < _nextInstantiate)
                return;

            _nextInstantiate = Time.unscaledTime + _instantiateDelay;

            if (UsePool)
            {
                ObjectPool.Retrieve(Prefab, transform.position, Quaternion.identity);
            }
            else
            {
                Instantiate(Prefab, transform.position, Quaternion.identity);
            }
        }
    }


}