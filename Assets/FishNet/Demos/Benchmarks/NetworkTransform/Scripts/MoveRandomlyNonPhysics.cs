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

        //Position to move towards.
        private Vector3 _goalPosition;
        //Rotation to move towards.
        private Quaternion _goalRotation;

        private Quaternion _lastRot;
        private int _nextGoalDataIndex = 0;

        public override void OnStartNetwork()
        {
            if (!base.IsServerStarted && !base.Owner.IsLocalClient)
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
            if (!base.IsController)
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
                    Vector3 rnd = (Random.insideUnitSphere * _range);
                    Vector3 next = transform.position;

                    if (_axes >= 1 && RandomizeAxes())
                        next.x = rnd.x;
                    if (_axes >= 2 && RandomizeAxes())
                        next.y = rnd.y;
                    if (_axes >= 3 && RandomizeAxes())
                        next.z = rnd.z;
                    
                    //Make sure at least one axes is set.
                    if (next == transform.position)
                        next.x = rnd.x;
                    
                    bool RandomizeAxes() => (Random.Range(0f, 1f) <= _chancePerAxes);

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
                            float nextY = (transform.eulerAngles.y == 0f) ? 180f : 0f;
                            euler = new Vector3(0f, nextY, 0f);
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