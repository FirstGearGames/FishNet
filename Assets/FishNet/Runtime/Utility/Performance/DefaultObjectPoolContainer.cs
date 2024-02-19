using UnityEngine;

namespace FishNet.Utility.Performance
{


    internal class DefaultObjectPoolContainer : MonoBehaviour
    {  
        /// <summary>
        /// Pool associated with this object.
        /// </summary>
        private DefaultObjectPool _pool;

        private void OnDestroy()
        {
            if (_pool != null)
                _pool.ObjectsDestroyed(this);
        }

        public void Initialize(DefaultObjectPool dop)
        {
            _pool = dop;
        }

    }


}