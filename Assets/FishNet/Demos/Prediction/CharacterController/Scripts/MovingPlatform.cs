using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace FishNet.Demo.Prediction.CharacterControllers
{
    public class MovingPlatform : TickNetworkBehaviour
    {
        #region Types.
        public struct ReplicateData : IReplicateData
        {
            public ReplicateData(uint unused = 0)
            {
                _tick = 0;
            }

            /// <summary>
            /// Tick is set at runtime. There is no need to manually assign this value.
            /// </summary>
            private uint _tick;

            public void Dispose() { }

            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public ReconcileData(Vector3 position, byte goalIndex)
            {
                Position = position;
                GoalIndex = goalIndex;
                _tick = 0;
            }

            /// <summary>
            /// Position of the character.
            /// </summary>
            public Vector3 Position;
            /// <summary>
            /// Current vertical velocity.
            /// </summary>
            /// <remarks>Used to simulate jumps and falls.</remarks>
            public byte GoalIndex;

            /// <summary>
            /// Tick is set at runtime. There is no need to manually assign this value.
            /// </summary>
            private uint _tick;

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        [SerializeField]
        private Transform _top;
        public Transform Top => _top;

        [SerializeField]
        private float _moveRate = 4f;

        /// <summary>
        /// Goal to move towards.
        /// </summary>
        private byte _goalIndex;
        /// <summary>
        /// Goals to move towards.
        /// </summary>
        private List<Vector3> _goals = new();

        private void Awake()
        {
            const float offset = 5f;
            Vector3 position = transform.position;
            _goals.Add(position + new Vector3(0f, 0f, offset));
            _goals.Add(position + new Vector3(0f, 0f, -offset));
        }

        public override void OnStartNetwork()
        {
            base.SetTickCallbacks(TickCallback.Tick);
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(default);
            CreateReconcile();
        }

        /// <summary>
        /// Creates a reconcile that is sent to clients.
        /// </summary>
        public override void CreateReconcile()
        {
            ReconcileData rd = new(transform.position, _goalIndex);
            PerformReconcile(rd);
        }

        [Replicate]
        private void PerformReplicate(ReplicateData rd, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            /* Move logic is always called regardless of state.
             *
             * This means that move will be called when replaying after a reconcile,
             * as well during OnTick, as well even if there is no data being received
             * from the server (IsFuture).
             *
             * By doing this we allow the platform to be ahead of what the client has
             * received by the server.
             *
             * When observed the client will actually see the platform
             * ahead of the server depending on their ping, being more ahead with higher pings.
             * This is the desired outcome as that's where the platform will be by the time
             * the client sends input to the server. If the platform was not ahead of the server
             * then when the client perhaps jumped onto it, the platform would actually be further
             * on the server then where the client observed it. In result, the client would snap to
             * a correct position when landing on the platform.
             *
             * If you want to visually see this simple uncomment the line below. Be sure to add
             * latency to make the correction more noticeable. */
            //if (state.IsFuture() && state.IsReplayed()) return;
            
            //Always use the tickDelta as your delta when performing actions inside replicate.
            float delta = (float)base.TimeManager.TickDelta;

            Vector3 goal = _goals[_goalIndex];
            Vector3 next = Vector3.MoveTowards(transform.position, goal, delta * _moveRate);

            transform.position = next;

            if (next == goal)
            {
                _goalIndex++;
                if (_goalIndex >= _goals.Count)
                    _goalIndex = 0;
            }
        }

        [Reconcile]
        private void PerformReconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
            _goalIndex = rd.GoalIndex;
        }

    }
}