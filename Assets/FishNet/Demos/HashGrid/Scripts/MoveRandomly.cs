using FishNet.Object;
using UnityEngine;

namespace FishNet.Demo.HashGrid
{
    public class MoveRandomly : NetworkBehaviour
    {
        // Colors green for client.
        [SerializeField]
        private Renderer _renderer;

        // How quickly to move over 1s.
        private float _moveRate = 0.5f;
        // Maximum range for new position.
        public const float Range = 25f;
        // Position to move towards.
        private Vector3 _goal;
        // Position at spawn.
        private Vector3 _start;

        private void Update()
        {
            if (!IsController)
                return;

            transform.position = Vector3.MoveTowards(transform.position, _goal, _moveRate * Time.deltaTime);
            if (transform.position == _goal)
                RandomizeGoal();
        }

        public override void OnStartNetwork()
        {
            _start = transform.position;
            RandomizeGoal();
        }

        public override void OnStartServer()
        {
            if (!Owner.IsValid)
                transform.position = _start + RandomInsideRange();
        }

        public override void OnStartClient()
        {
            if (Owner.IsLocalClient)
            {
                _renderer.material.color = Color.green;
                _moveRate *= 3f;
                transform.position -= new Vector3(0f, 0f, 1f);
                Camera c = Camera.main;
                c.transform.SetParent(transform);
                c.transform.localScale = Vector3.one;
                c.transform.localPosition = new(0f, 0f, -5f);
            }
            else
            {
                _renderer.material.color = Color.gray;
            }
        }

        public override void OnStopClient()
        {
            if (IsOwner)
            {
                Camera c = Camera.main;
                if (c != null)
                {
                    c.transform.SetParent(null);
                    c.transform.localScale = Vector3.one;
                }
            }
        }

        private void RandomizeGoal()
        {
            _goal = _start + RandomInsideRange();
        }

        private Vector3 RandomInsideRange()
        {
            Vector3 goal = Random.insideUnitSphere * Range;
            goal.z = transform.position.z;
            return goal;
        }
    }
}