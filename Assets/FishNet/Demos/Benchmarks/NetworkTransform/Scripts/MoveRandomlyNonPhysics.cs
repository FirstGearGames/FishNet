using System;
using System.Collections.Generic;
using FishNet.Utility.Template;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FishNet.Demo.Benchmarks.NetworkTransforms
{
    public class MoveRandomlyNonPhysics : TickNetworkBehaviour
    {
        [Serializable]
        private struct GoalData
        {
            public Vector3 Position;
            public Vector3 Eulers;
        }

        [SerializeField]
        private bool _isActive = true;
        [SerializeField]
        private bool _is2d;
        [Header("Changes")]
        [SerializeField]
        [Range(0, 3)]
        private byte _axes = 3;
        [Range(0.1f, 1f)]
        [SerializeField]
        private float _chancePerAxes = 0.8f;
        [SerializeField]
        [Range(0f, 1f)]
        private float _rotationChance = 0.33f;
        [Header("Movement")]
        [Range(0f, 5f)]
        [SerializeField]
        private float _delayBetweenMovements = 1.5f;
        [SerializeField]
        private float _yOffsetPerInstance = 0f;
        [SerializeField]
        private bool _randomMovement = true;
        [SerializeField]
        private List<GoalData> _goalDatas = new();
        [SerializeField]
        private bool _moveInUpdate = false;
        [SerializeField]
        [Range(0.1f, 30f)]
        private float _moveRate = 3f;
        [Range(1f, 30f)]
        [SerializeField]
        private float _rotateRate = 30f;
        [Range(0.1f, 40f)]
        [SerializeField]
        private float _range = 6f;

        // Position to move towards.
        private Vector3 _goalPosition;
        // Rotation to move towards.
        private Quaternion _goalRotation;
        private Quaternion _lastRot;
        private int _nextGoalDataIndex = 0;
        private float _nextMoveTime = float.MinValue;

        public override void OnStartNetwork()
        {
            if (!IsServerStarted && !Owner.IsLocalClient)
            {
                SetTickCallbacks(TickCallback.None);
                DestroyImmediate(this);
            }
            else
            {
                if (_moveInUpdate)
                    SetTickCallbacks(TickCallback.Update);
                else
                    SetTickCallbacks(TickCallback.Tick);
            }
        }

        protected override void TimeManager_OnUpdate()
        {
            if (_moveInUpdate)
                Move(Time.deltaTime);
        }

        protected override void TimeManager_OnTick()
        {
            if (!_moveInUpdate)
            {
                float delta = (float)TimeManager.TickDelta;
                Move(delta);
            }
        }

        private void Move(float delta)
        {
            if (!_isActive)
                return;
            if (!IsController)
                return;
            if (Time.time < _nextMoveTime)
                return;

            transform.position = Vector3.MoveTowards(transform.position, _goalPosition, _moveRate * delta);
            if (!_is2d)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _goalRotation, _rotateRate * delta);
            else
                transform.rotation = _goalRotation;

            if (transform.position == _goalPosition)
                SetNextGoal();
        }

        public override void OnStartServer()
        {
            TrySetFirstGoal();
        }

        public override void OnStartClient()
        {
            TrySetFirstGoal();
        }

        private void TrySetFirstGoal()
        {
            if (!IsController)
                return;

            SetNextGoal();

            transform.position = _goalPosition;
            transform.rotation = _goalRotation;

            _nextMoveTime = float.MinValue;
        }

        private void SetNextGoal()
        {
            if (_randomMovement)
                SetRandomGoal();
            else
                SetSpecifiedGoal();

            _nextMoveTime = Time.time + _delayBetweenMovements;

            void SetSpecifiedGoal()
            {
                if (_goalDatas.Count == 0)
                    return;

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
                    Vector3 rnd = Random.insideUnitSphere * _range;
                    Vector3 next = transform.position;

                    if (_axes >= 1 && RandomizeAxes())
                        next.x = rnd.x;
                    if (_axes >= 2 && RandomizeAxes())
                        next.y = rnd.y;
                    if (_axes >= 3 && RandomizeAxes())
                        next.z = rnd.z;

                    // Make sure at least one axes is set.
                    if (next == transform.position)
                        next.x = rnd.x;

                    bool RandomizeAxes() => Random.Range(0f, 1f) <= _chancePerAxes;

                    _goalPosition = next;
                }

                if (_rotationChance > 0f)
                {
                    if (Random.Range(0f, 1f) <= _rotationChance)
                    {
                        Vector3 euler;
                        if (!_is2d)
                        {
                            euler = Random.insideUnitSphere * 180f;
                        }
                        else
                        {
                            float nextY = transform.eulerAngles.y == 0f ? 180f : 0f;
                            euler = new(0f, nextY, 0f);
                        }

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