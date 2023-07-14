using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Example.Scened
{


    public class PlayerController : NetworkBehaviour
    {
        [SerializeField]
        private GameObject _camera;
        [SerializeField]
        private float _moveRate = 4f;
        [SerializeField]
        private bool _clientAuth = true;

        public override void OnStartClient()
        {
            if (base.IsOwner)
                _camera.SetActive(true);
        }

        private void Update()
        {
            if (!base.IsOwner)
                return;

            float hor = Input.GetAxisRaw("Horizontal");
            float ver = Input.GetAxisRaw("Vertical");

            /* If ground cannot be found for 20 units then bump up 3 units. 
             * This is just to keep player on ground if they fall through
             * when changing scenes.             */
            if (_clientAuth || (!_clientAuth && base.IsServer))
            {
                if (!Physics.Linecast(transform.position + new Vector3(0f, 0.3f, 0f), transform.position - (Vector3.one * 20f)))
                    transform.position += new Vector3(0f, 3f, 0f);
            }

            if (_clientAuth)
                Move(hor, ver);
            else
                ServerMove(hor, ver);
        }

        [ServerRpc]
        private void ServerMove(float hor, float ver)
        {
            Move(hor, ver);
        }

        private void Move(float hor, float ver)
        {
            float gravity = -10f * Time.deltaTime;
            //If ray hits floor then cancel gravity.
            Ray ray = new Ray(transform.position + new Vector3(0f, 0.05f, 0f), -Vector3.up);
            if (Physics.Raycast(ray, 0.1f + -gravity))
                gravity = 0f;

            /* Moving. */
            Vector3 direction = new Vector3(
                0f,
                gravity,
                ver * _moveRate * Time.deltaTime);

            transform.position += transform.TransformDirection(direction);
            transform.Rotate(new Vector3(0f, hor * 100f * Time.deltaTime, 0f));
        }

    }


}