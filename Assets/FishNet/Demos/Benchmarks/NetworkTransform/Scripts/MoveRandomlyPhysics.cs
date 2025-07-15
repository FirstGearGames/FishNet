using System;
using FishNet.Component.Transforming;
using FishNet.Managing.Timing;
using FishNet.Utility.Template;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FishNet.Demo.Benchmarks.NetworkTransforms
{
    public class MoveRandomlyPhysics : TickNetworkBehaviour
    {
        [SerializeField]
        private bool _isActive = true;
        [Header("Movement")]
        [Tooltip("How much force to apply.")]
        [Range(0f, 1000f)]
        [SerializeField]
        private float _force = 10f;
        [Tooltip("How often to apply force.")]
        [SerializeField]
        private FloatRange _interval = new(3f, 10f);
        private uint _nextForceTick = TimeManager.UNSET_TICK;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnStartNetwork()
        {
            if (!IsServerStarted)
            {
                SetTickCallbacks(TickCallback.None);
                _rigidbody.isKinematic = true;
                DestroyImmediate(this);
            }
            else
            {
                SetTickCallbacks(TickCallback.Tick);
            }
        }

        protected override void TimeManager_OnTick()
        {
            Move();
        }

        private void Move()
        {
            if (!_isActive)
                return;

            uint tick = TimeManager.LocalTick;
            if (tick < _nextForceTick)
                return;

            _nextForceTick = tick + TimeManager.TimeToTicks(_interval.RandomInclusive(), TickRounding.RoundUp);

            Vector3 force = Random.insideUnitSphere * _force;

            // Always ensure vertical movement, and movement away from center.
            if (Math.Sign(force.x) == Math.Sign(transform.position.x))
                force.x *= -1f;
            force.y = _force;
            if (Math.Sign(force.z) == Math.Sign(transform.position.z))
                force.z *= -1f;

            _rigidbody.AddForce(force, ForceMode.Impulse);
        }
    }
}