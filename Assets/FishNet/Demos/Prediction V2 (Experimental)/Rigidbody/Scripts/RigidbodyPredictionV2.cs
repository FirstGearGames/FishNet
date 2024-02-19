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
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */
    /* THIS CLASS IS CURRENTLY USED FOR TESTING AND IS NOT CONSIDERED
     * AN EXAMPLE TO FOLLOW. */

    public class RigidbodyPredictionV2 : NetworkBehaviour
    {
#if PREDICTION_V2

        public struct MoveData : IReplicateData
        {
            public bool Jump;
            public float Horizontal;
            public float Vertical;
            public Vector3 OtherImpulseForces;
            public MoveData(bool jump, float horizontal, float vertical, Vector3 otherImpulseForces)
            { 
                Jump = jump;
                Horizontal = horizontal;
                Vertical = vertical;
                OtherImpulseForces = otherImpulseForces;
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

        //[SerializeField]
        //private float _jumpForce = 15f;
        [SerializeField]
        private float _moveRate = 15f;

        public Rigidbody Rigidbody { get; private set; }
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
            Rigidbody = GetComponent<Rigidbody>();
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
            if (!IsOwner && Owner.IsValid)
                return default;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            //MoveData md = new MoveData(_jump, horizontal, vertical, (SpringForces + RocketForces));
            MoveData md = new MoveData(_jump, horizontal, vertical, Vector3.zero);

            //SpringForces = Vector3.zero;
            //RocketForces = Vector3.zero;

            _jump = false;

            return md;
        }

        public uint LastMdTick;

        [Replicate]
        private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            LastMdTick = md.GetTick();
            //if (base.IsOwner)
            //    Debug.Log(PredictionManager.ClientReplayTick + " > " + md.GetTick());
            //if (state == ReplicateState.Future)
            //{
            //    /* Reduce velocity slightly. This will be slightly less accurate if
            //     * the object continues to move in the same direction but can drastically
            //     * reduce jarring visuals if the object changes path rather than predicted(future)
            //     * forward. */
            //    _rigidbody.velocity *= 0.65f;
            //    _rigidbody.angularVelocity *= 0.65f;
            //    return;
            //}            

            //Vector3 forces = new Vector3(md.Horizontal, 0f, md.Vertical) * _moveRate;
            //Rigidbody.AddForce(forces);

            //if (md.Jump)
            //    Rigidbody.AddForce(new Vector3(0f, _jumpForce, 0f), ForceMode.Impulse);
            ////Add gravity to make the object fall faster.
            //Rigidbody.AddForce(Physics.gravity * 3f);

            Vector3 forces = new Vector3(md.Horizontal, 0f, md.Vertical) * _moveRate;
            //PRB.AddForce(forces);
            forces += Physics.gravity * 3f;
            //if (md.Jump)
            //    PRB.AddForce(new Vector3(0f, _jumpForce, 0f), ForceMode.Impulse);
            ////Add gravity to make the object fall faster.
            //PRB.AddForce(forces);


            //if (IsOwner)
            //{
            //    if (state.IsReplayed())
            //        Debug.Log($"{md.GetTick()} -> {transform.position.x} -> {Rigidbody.velocity.x}");
            //    else
            //        Debug.LogWarning($"{md.GetTick()} -> {transform.position.x} -> {Rigidbody.velocity.x}");
            //}

            //if ((!base.IsServerStarted && base.IsOwner) || (base.IsServerStarted && !base.IsOwner))
            //    Debug.LogWarning($"Frame {Time.frameCount}. State {state}, Horizontal {md.Horizontal}. MdTick {md.GetTick()}, PosX {transform.position.x.ToString("0.##")}. VelX {Rigidbody.velocity.x.ToString("0.###")}.");
        }

        private void SendReconcile()
        {
            /* The base.IsServer check is not required but does save a little
            * performance by not building the reconcileData if not server. */
            if (IsServerStarted)
            {
                ReconcileData rd = new ReconcileData(transform.position, transform.rotation, Rigidbody.velocity, Rigidbody.angularVelocity);
                //if (!base.IsOwner)
                //    Debug.LogError($"Frame {Time.frameCount}. Reconcile, MdTick {LastMdTick}, PosX {transform.position.x.ToString("0.##")}. VelX {Rigidbody.velocity.x.ToString("0.###")}.");
                Reconciliation(rd);
            }
        }

        private void TimeManager_OnPostTick()
        {
            SendReconcile();
        }

        [Reconcile]
        private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
            transform.rotation = rd.Rotation;
            Rigidbody.velocity = rd.Velocity;
            Rigidbody.angularVelocity = rd.AngularVelocity;

            //if (PrintForClient())
            //{ 
            //    Debug.LogError($"Frame {Time.frameCount}. Reconcile, MdTick {rd.GetTick()}, PosX {transform.position.x.ToString("0.##")}. VelX {Rigidbody.velocity.x.ToString("0.###")}. RdPosX " +
            //        $"{rd.Position.x.ToString("0.##")}. RdVelX {Rigidbody.velocity.x.ToString("0.###")}");
            //}

        }

        private bool PrintForClient() => ((!base.IsServerStarted && base.IsOwner) || (base.IsServerStarted && !base.IsOwner));

#endif
    }

}