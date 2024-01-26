using UnityEngine;

namespace FishNet.Example.ColliderRollbacks
{
    public class DestroyAfterDelay : MonoBehaviour
    {
        [SerializeField]
        private float _delay = 1f;

        private void Awake()
        {
            Destroy(gameObject, _delay);
        }

    }

}