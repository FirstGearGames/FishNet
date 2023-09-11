using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

/*
* 
* See TransformPrediction.cs for more detailed notes.
* 
*/

namespace FishNet.PredictionV2
{

    public class RigidbodyPredictionV2 : NetworkBehaviour
    {
#if PREDICTION_V2


        public struct MoveData : IReplicateData
        {
            public bool Jump;
            public float Horizontal;
            public float Vertical;
            public MoveData(bool jump, float horizontal, float vertical)
            {
                Jump = jump;
                Horizontal = horizontal;
                Vertical = vertical;
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
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            public ReconcileData(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
            {
                Position = position;
                Rotation = rotation;
                Velocity = velocity;
                AngularVelocity = angularVelocity;
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
        private float _moveRate = 15f;

        private Rigidbody _rigidbody;
        private bool _jump;

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

            _rigidbody = GetComponent<Rigidbody>();
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopNetwork()
        {

            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }


        private void TimeManager_OnTick()
        {
            Move(BuildMoveData());
        }

        private MoveData BuildMoveData()
        {

            if (!base.IsOwner)
                return default;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            MoveData md = new MoveData(_jump, horizontal, vertical);
            _jump = false;

            return md;
        }

        private MoveData _lastData;

        [ReplicateV2]
        private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {

            //if (!base.IsOwner)
            //{
            //    if (state == ReplicateState.ReplayedPredicted || state == ReplicateState.Predicted)
            //    {
            //        uint tick = md.GetTick();
            //        md = _lastData;
            //        md.SetTick(tick);
            //    }
            //    else
            //    {
            //        _lastData = md;
            //    }
            //}
            //if (base.IsOwner && state != ReplicateState.UserCreated && state != ReplicateState.ReplayedUserCreated)
            //  Debug.LogError($"SDFSD  " + md.GetTick());
            /* If predicted input via replay then slow down velocity.
             * This prevents potential overshooting if the object were to change
             * direction. If your rigidbody has a substantial amount of drag or is
             * likely to continue in the same direction this likely is not needed.
             * 
             * This is not a requirement by any means but rather a modification for
             * this demo scene/game type. */
            //if (state == ReplicateState.ReplayedPredicted || state == ReplicateState.Predicted)
            //    _rigidbody.velocity *= 0.75f;

            Vector3 forces = new Vector3(md.Horizontal, 0f, md.Vertical) * _moveRate;
            _rigidbody.AddForce(forces);

            if (md.Jump)
                _rigidbody.AddForce(new Vector3(0f, _jumpForce, 0f), ForceMode.Impulse);
            //Add gravity to make the object fall faster.
            _rigidbody.AddForce(Physics.gravity * 3f);
        }

        private void TimeManager_OnPostTick()
        {
            /* The base.IsServer check is not required but does save a little
            * performance by not building the reconcileData if not server. */
            if (IsServer)
            {
                ReconcileData rd = new ReconcileData(transform.position, transform.rotation, _rigidbody.velocity, _rigidbody.angularVelocity);
                Reconciliation(rd);
            }
        }

        [ReconcileV2]
        private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
            transform.rotation = rd.Rotation;
            _rigidbody.velocity = rd.Velocity;
            _rigidbody.angularVelocity = rd.AngularVelocity;
        }

#endif
    }

}