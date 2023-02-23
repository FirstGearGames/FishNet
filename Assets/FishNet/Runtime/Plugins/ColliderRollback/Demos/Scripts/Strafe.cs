using FishNet.Object;
using UnityEngine;

namespace FirstGearGames.ColliderRollbacks.Demos
{


    public class Strafe : NetworkBehaviour
    {
        public float MoveRate = 2f;
        public float MoveDistance = 3f;

        private bool _movingRight = true;
        private float _startX;
        public override void OnStartServer()
        {
            base.OnStartServer();
            _startX = transform.position.x;
        }

        private void Update()
        {
            if (base.IsServer)
            {
                float x = (_movingRight) ? _startX + MoveDistance : _startX - MoveDistance;
                Vector3 goal = new Vector3(x, transform.position.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, goal, MoveRate * Time.deltaTime);
                if (transform.position == goal)
                    _movingRight = !_movingRight;
            }
        }
    }


}