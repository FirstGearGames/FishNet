using System;
using FishNet.Utility.Template;
using UnityEngine;
using Random = UnityEngine.Random;

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
        private bool _moveInUpdate = false;
        [SerializeField]
        [Range(0.1f, 30f)]
        private float _moveRate = 3f;
        [Range(1f, 1000f)]
        [SerializeField]
        private float _rotateRate = 30f;
        [Range(0.1f, 40f)]
        [SerializeField]
        private float _range = 6f;

        //Position to move towards.
        private Vector3 _goalPosition;
        //Rotation to move towards.
        private Quaternion _goalRotation;

        private Quaternion _lastRot;

        protected override void TimeManager_OnTick()
        {
            if (_moveInUpdate)
                return;
            
            float delta = (float)base.TimeManager.TickDelta;
            Move(delta);
        }

        private void Update()
        {
            if (!_moveInUpdate)
                return;

            Move(Time.deltaTime);
        }

        private void Move(float delta)
        {        
            if (!_isActive)
                return;
            if (!base.IsServerInitialized)
                return;
            
            transform.position = Vector3.MoveTowards(transform.position, _goalPosition, _moveRate * delta);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _goalRotation, _rotateRate * delta);
            
            if (transform.position == _goalPosition)
                RandomizeGoal();
        }

        public override void OnStartServer()
        {
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