using FishNet.Object;
using UnityEngine;

namespace FishNet.Demo.NetworkLod
{

    public class MoveRandomly : NetworkBehaviour
    {
        //Colors green for client.
        [SerializeField]
        private Renderer _renderer;

        //Time to move to new position.
        private const float _moveRate = 3f;
        //Maximum range for new position.
        private const float _range = 10f;
        //Position to move towards.
        private Vector3 _goal;
        //Position at spawn.
        private Vector3 _start;

        private void Update()
        {
            //Client should not move these.
            if (base.IsClientOnly)
                return;
            //Server shouldn't move client one.
            if (base.Owner.IsValid)
                return;

            transform.position = Vector3.MoveTowards(transform.position, _goal, _moveRate * Time.deltaTime);
            if (transform.position == _goal)
                RandomizeGoal();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _start = transform.position;
            RandomizeGoal();

            if (_renderer != null && base.Owner.IsActive)
                _renderer.material.color = Color.green;
        }

        private void RandomizeGoal()
        {
            _goal = _start + (Random.insideUnitSphere * _range);
        }

    }

}