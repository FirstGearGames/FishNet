using System;
using System.Collections.Generic;
using FishNet.Utility.Template;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FishNet.Demo.Benchmarks.NetworkTransforms
{
    public class MoveRandomlyNonPhysics : TickNetworkBehaviour
    {
        [System.Serializable]
        private struct GoalData
        {
            public Vector3 Position;
            public Vector3 Eulers;
        }

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
        private bool _randomMovement = true;
        [SerializeField]
        private List<GoalData> _goalDatas = new();

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
        private int _nextGoalDataIndex = 0;

        public override void OnStartNetwork()
        {
            if (!base.IsServerStarted)
            {
                base.SetTickCallbacks(TickCallback.None);
                DestroyImmediate(this);
            }
            else
            {
                if (_moveInUpdate)
                    base.SetTickCallbacks(TickCallback.Update);
                else
                    base.SetTickCallbacks(TickCallback.Tick);
            }
        }

        protected override void TimeManager_OnUpdate()
        {
            Move(Time.deltaTime);
        }

        protected override void TimeManager_OnTick()
        {
            float delta = (float)base.TimeManager.TickDelta;
            Move(delta);
        }

        private void Move(float delta)
        {
            if (!_isActive)
                return;

            transform.position = Vector3.MoveTowards(transform.position, _goalPosition, _moveRate * delta);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _goalRotation, _rotateRate * delta);

            if (transform.position == _goalPosition)
                SetNextGoal();
        }

        public override void OnStartServer()
        {
            SetNextGoal();
        }

        private void SetNextGoal()
        {
            if (_randomMovement)
                SetRandomGoal();
            else
                SetSpecifiedGoal();

            void SetSpecifiedGoal()
            {
                if (_goalDatas.Count == 0) return;

                if (_nextGoalDataIndex >= _goalDatas.Count)
                    _nextGoalDataIndex = 0;

                int index = _nextGoalDataIndex;
                _nextGoalDataIndex++;
                
                _goalPosition = _goalDatas[index].Position;
                _goalRotation = Quaternion.Euler(_goalDatas[index].Eulers);
            }

            void SetRandomGoal()
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
}