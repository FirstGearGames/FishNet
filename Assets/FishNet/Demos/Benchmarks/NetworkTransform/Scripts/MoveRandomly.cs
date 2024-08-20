using FishNet.Utility.Template;
using UnityEngine;

namespace FishNet.Demo.Benchmarks.NetworkTransforms
{
    public class MoveRandomly : TickNetworkBehaviour
    {
        [SerializeField]
        private bool _isActive = true;
        
        [Header("Changes")]
        [SerializeField]
        [Range(0, 3)]
        private byte _axes = 3;
        [SerializeField]
        [Range(0f, 1f)]
        private float _rotationChance = 0.33f;

        [Header("Movement")]
        [SerializeField]
        [Range(0.1f, 30f)]
        private float _moveRate = 3f;
        [Range(1f, 1000f)]
        [SerializeField]
        private float _rotateRate = 30f;

        //Maximum range for new position.
        private const float _range = 10f;

        //Position to move towards.
        private Vector3 _goalPosition;
        //Rotation to move towards.
        private Quaternion _goalRotation;
        //Position at spawn.
        private Vector3 _startPosition;

        private Quaternion _lastRot;

        protected override void TimeManager_OnTick()
        {
            if (!_isActive)
                return;
            if (!base.IsServerInitialized)
                return;

            float delta = (float)base.TimeManager.TickDelta;
            
            transform.position = Vector3.MoveTowards(transform.position, _goalPosition, _moveRate * delta);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _goalRotation, _rotateRate * delta);
            
            if (transform.position == _goalPosition)
                RandomizeGoal();
        }
        
        public override void OnStartServer()
        {
            _startPosition = transform.position;
            
            RandomizeGoal();
        }

        private void RandomizeGoal()
        {
            if (_axes > 0)
            {
                Vector3 currentPosition = transform.position;
                Vector3 goal = (Random.insideUnitSphere * _range);
                switch (_axes)
                {
                    case 1:
                        goal.y = currentPosition.y;
                        goal.z = currentPosition.z;
                        break;
                    case 2:
                        goal.z = currentPosition.z;
                        break;
                }

                _goalPosition = goal;
            }

            if (_rotationChance > 0f) 
            {
                if (Random.Range(0f, 1f) <= _rotationChance)
                {
                    Vector3 euler = Random.insideUnitSphere * 180f;
                    _goalRotation = Quaternion.Euler(euler);
                }
                else
                {
                    _goalRotation = transform.rotation;
                }
            }
        }
    }
}