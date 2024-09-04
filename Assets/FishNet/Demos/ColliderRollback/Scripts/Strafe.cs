using FishNet.Object;
using UnityEngine;


namespace FishNet.Example.ColliderRollbacks
{

    public class Strafe : NetworkBehaviour
    {
        public float MoveRate = 2f;
        public float MoveDistance = 3f;

        private bool _movingRight = true;
        private float _startX;
        public override void OnStartServer()
        {
            
            _startX = transform.position.x;
        }

        private void Update()
        {
            if (base.IsServerStarted)
            {
                float x = (_movingRight) ? _startX + MoveDistance : _startX - MoveDistance;
                Vector3 goal = new(x, transform.position.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, goal, MoveRate * Time.deltaTime);
                if (transform.position == goal)
                    _movingRight = !_movingRight;
            }
        }
    }


}