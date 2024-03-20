using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;
using static FishNet.PredictionV2.CharacterControllerPredictionV2;

namespace FishNet.PredictionV2
{

    public class CharacterControllerPredictionV2 : NetworkBehaviour
    {
#if PREDICTION_V2


        public struct MoveData : IReplicateData
        {
            public uint SentTick;
            public bool Jump;
            public float Horizontal;
            public float Vertical;
            public MoveData(bool jump, float horizontal, float vertical, uint sentTick)
            {
                Jump = jump;
                Horizontal = horizontal;
                Vertical = vertical;
                SentTick = sentTick;
                _tick = 0;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public Vector3 Position;
            public float VerticalVelocity;
            public ReconcileData(Vector3 position, float verticalVelocity)
            {
                Position = position;
                VerticalVelocity = verticalVelocity;
                _tick = 0;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        [SerializeField]
        private float _jumpForce = 15f;
        [SerializeField]
        private float _moveRate = 4f;

        private CharacterController _characterController;
        private bool _jump;
        private float _verticalVelocity;

        private void Update()
        {
            if (base.IsOwner)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    _jump = true;
            }
        }

        public override void OnStartNetwork()
        {
            _characterController = GetComponent<CharacterController>();
            base.TimeManager.OnTick += TimeManager_OnTick;
        }

        public override void OnStopNetwork()
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
        }


        private void TimeManager_OnTick()
        {
            Move(BuildMoveData());
            /* The base.IsServer check is not required but does save a little
            * performance by not building the reconcileData if not server. */
            if (base.IsServerStarted)
            {
                ReconcileData rd = new ReconcileData(transform.position, _verticalVelocity);
                Reconciliation(rd);
            }
        }

        private MoveData BuildMoveData()
        {
            if (!base.IsOwner)
                return default;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            MoveData md;
            if (horizontal != 0 || vertical != 0)
                md = new MoveData(_jump, horizontal, vertical, base.TimeManager.LocalTick);
            else
                md = new MoveData(_jump, horizontal, vertical, 0);
            _jump = false;

            return md;
        }


        [Replicate]
        private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (state == ReplicateState.CurrentFuture)
                return;

            if (md.Jump)
                _verticalVelocity = _jumpForce;

            float delta = (float)base.TimeManager.TickDelta;
            _verticalVelocity += (Physics.gravity.y * delta);
            if (_verticalVelocity < -20f)
                _verticalVelocity = -20f;


            Vector3 forces = new Vector3(md.Horizontal, _verticalVelocity, md.Vertical) * _moveRate;
            _characterController.Move(forces * delta);


        }

        [Reconcile]
        private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
            _verticalVelocity = rd.VerticalVelocity;

        }

#endif
    }

}