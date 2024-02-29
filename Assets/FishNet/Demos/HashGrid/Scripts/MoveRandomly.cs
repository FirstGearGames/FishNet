using FishNet.Object;
using UnityEngine;

namespace FishNet.Demo.HashGrid
{

    public class MoveRandomly : NetworkBehaviour
    {
        //Colors green for client.
        [SerializeField]
        private Renderer _renderer;

        //How quickly to move over 1s.
        private float _moveRate = 0.5f;
        //Maximum range for new position.
        public const float Range = 25f;
        //Position to move towards.
        private Vector3 _goal;
        //Position at spawn.
        private Vector3 _start;

        private void Update()
        {
            if (!base.IsOwner && !base.IsServer)
                return;

            transform.position = Vector3.MoveTowards(transform.position, _goal, (_moveRate * Time.deltaTime));
            if (transform.position == _goal)
                RandomizeGoal();
        }

        public override void OnStartNetwork()
        {
            _start = transform.position;

            if (base.Owner.IsLocalClient)
            {
                _renderer.material.color = Color.green;
                _moveRate *= 3f;
                transform.position -= new Vector3(0f, 0f, 1f);
                Camera c = Camera.main;
                c.transform.SetParent(transform);
                c.transform.localPosition = new Vector3(0f, 0f, -5f);
            }
            else
            {
                _renderer.material.color = Color.gray;
                transform.position = (_start + RandomInsideRange());
            }

            RandomizeGoal();
        }

        public override void OnStopNetwork()
        {
            Camera c = Camera.main;
            if (c != null && base.Owner.IsLocalClient)
                c.transform.SetParent(null);
        }

        private void RandomizeGoal()
        {
            _goal = _start + RandomInsideRange();
        }

        private Vector3 RandomInsideRange()
        {
            Vector3 goal = (Random.insideUnitSphere * Range);
            goal.z = transform.position.z;
            return goal;
        }

    }

}