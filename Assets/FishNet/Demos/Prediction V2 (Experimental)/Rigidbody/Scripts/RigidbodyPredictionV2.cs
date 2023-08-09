using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using System.Collections.Generic;
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

        private MoveData? _lastMoveData;

        private int _jumpedLast = 0;

        [ReplicateV2]
        private void Move(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            //if (state == ReplicateState.Predicted && _lastMoveData.HasValue)
            //{
            //    MoveData lastMd = _lastMoveData.Value;
            //    lastMd.Horizontal *= 0.9f;
            //    /* The tick will increase even if the data is unset.
            //     * Cache the tick, set md to last data, then reapply the tick. */
            //    uint tick = md.GetTick();
            //    md = lastMd;
            //    md.SetTick(tick);
            //    _lastMoveData = lastMd;
            //}
            //else if (state == ReplicateState.UserCreated && !base.IsOwner && !base.IsServer)
            //{
            //    _lastMoveData = md;
            //}
            ////if (state == ReplicateState.UserCreated && canShow)
            ////{
            ////    bool jump = (md.Jump);
            ////    if (jump)
            ////        Debug.LogWarning($"Replicating tick {md.GetTick()}");
            ////    else
            ////        Debug.Log($"Replicating tick {md.GetTick()}");
            ////}

            bool replayed = (state == ReplicateState.ReplayedUserCreated || state == ReplicateState.ReplayedPredicted);
            if (md.Jump)
            {
                //Debug.Log($"Replaying {replayed}. JUMPING on tick {theTick}");
                Debug.LogWarning($"Frame {Time.frameCount}. Replaying {replayed}. JUMPING on mdTick {md.GetTick()}. LocalTick {base.TimeManager.LocalTick}");
                if (replayed)
                    _jumpedLast++;
            }


            //If predicted input via replay then slow down velocity.
            if (state == ReplicateState.ReplayedPredicted || state == ReplicateState.Predicted)
                _rigidbody.velocity *= 0.5f;

            /* ReplicateState is set based on if the data is new, being replayed, ect.
            * Visit the ReplicationState enum for more information on what each value
            * indicates. At the end of this guide a more advanced use of state will
            * be demonstrated. */
            Vector3 forces = new Vector3(md.Horizontal, 0f, md.Vertical) * _moveRate;
            _rigidbody.AddForce(forces);

            if (md.Jump)
                _rigidbody.AddForce(new Vector3(0f, _jumpForce, 0f), ForceMode.Impulse);
            //Add gravity to make the object fall faster.
            _rigidbody.AddForce(Physics.gravity * 3f);

            lastReplicateTick = md.GetTick();
        }

        private uint lastReplicateTick;
        private Dictionary<uint, float> _yPositions = new Dictionary<uint, float>();

        private void TimeManager_OnPostTick()
        {
            if (!base.IsOwner)
                _yPositions[lastReplicateTick] = transform.position.y;

            /* The base.IsServer check is not required but does save a little
            * performance by not building the reconcileData if not server. */
            if (IsServer)
            {
                ReconcileData rd = new ReconcileData(transform.position, transform.rotation, _rigidbody.velocity, _rigidbody.angularVelocity);
                Reconciliation(rd);
                // if (base.IsOwner)
                // Debug.LogWarning($"Sending reconcile. MdTick {rd.GetTick()}. LocalTick {base.TimeManager.LocalTick}. PosY {transform.position.y}");
            }
        }

        [ReconcileV2]
        private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            if (!base.IsOwner)
            {
                if (!_yPositions.TryGetValue(rd.GetTick(), out float value))
                    value = 9999f;
                Debug.LogError($"Tick {rd.GetTick()}. VelocityY {rd.Velocity.y}. Reconcile Y {rd.Position.y}. Saved Y {value}");

                List<uint> removedEntries = new List<uint>();
                foreach (var item in _yPositions.Keys)
                {
                    if (item <= rd.GetTick())
                        removedEntries.Add(item);
                }
                foreach (var item in removedEntries)
                    _yPositions.Remove(item);
            }

            transform.position = rd.Position;
            transform.rotation = rd.Rotation;
            _rigidbody.velocity = rd.Velocity;
            _rigidbody.angularVelocity = rd.AngularVelocity;
        }

#endif
    }

}