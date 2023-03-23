using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

/*
* 
* See TransformPrediction.cs for more detailed notes.
* 
*/

namespace FishNet.Example.Prediction.Rigidbodies
{

    public class RigidbodyPrediction : NetworkBehaviour
    {
        #region Types.
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
        #endregion

        #region Serialized.
        [SerializeField]
        private float _jumpForce = 15f;
        [SerializeField]
        private float _moveRate = 15f;
        #endregion

        #region Private.
        /// <summary>
        /// Rigidbody on this object.
        /// </summary>
        private Rigidbody _rigidbody;
        /// <summary>
        /// Next time a jump is allowed.
        /// </summary>
        private float _nextJumpTime;
        /// <summary>
        /// True to jump next frame.
        /// </summary>
        private bool _jump;
        #endregion

        #region Predicted spawning.
        /// <summary>
        /// Prefab to spawn for predicted spawning.
        /// </summary>
        public NetworkObject BulletPrefab;
        /// <summary>
        /// True if a spawn is queued from input.
        /// </summary>
        private bool _spawnBullet;
        /// <summary>
        /// True if a despawn is queued from input.
        /// </summary>
        private bool _despawnBullet;
        /// <summary>
        /// Last spawned bullet. Used to test predicted despawn.
        /// </summary>
        private NetworkObject _lastSpawnedBullet;
        #endregion

        private void Awake()
        {

            _rigidbody = GetComponent<Rigidbody>();
            InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        private void OnDestroy()
        {
            if (InstanceFinder.TimeManager != null)
            {
                InstanceFinder.TimeManager.OnTick -= TimeManager_OnTick;
                InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            base.PredictionManager.OnPreReplicateReplay += PredictionManager_OnPreReplicateReplay;
        }
        public override void OnStopClient()
        {
            base.OnStopClient();
            base.PredictionManager.OnPreReplicateReplay -= PredictionManager_OnPreReplicateReplay;
        }

        private void Update()
        {
            if (base.IsOwner)
            {
                if (Input.GetKeyDown(KeyCode.RightAlt))
                {
                    _rigidbody.velocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                if (Input.GetKeyDown(KeyCode.Space) && Time.time > _nextJumpTime)
                {
                    _nextJumpTime = Time.time + 1f;
                    _jump = true;
                }
                else if (Input.GetKeyDown(KeyCode.LeftShift))
                {
                    _spawnBullet = true;
                }
                //else if (Input.GetKeyDown(KeyCode.LeftAlt))
                //{
                //    _despawnBullet = true;
                //}
            }
        }

        /// <summary>
        /// Called every time any predicted object is replaying. Replays only occur for owner.
        /// Currently owners may only predict one object at a time.
        /// </summary>
        private void PredictionManager_OnPreReplicateReplay(uint arg1, PhysicsScene arg2, PhysicsScene2D arg3)
        {
            /* Server does not replay so it does
             * not need to add gravity. */
            if (!base.IsServer)
                AddGravity();
        }


        private void TimeManager_OnTick()
        {
            if (base.IsOwner)
            {
                Reconciliation(default, false);
                BuildMoveData(out MoveData md);
                Move(md, false);
                //Predicted spawning example.
                TryDespawnBullet();
                TrySpawnBullet();
            }
            if (base.IsServer)
            {
                Move(default, true);
            }

            /* Server and all clients must add the additional gravity.
             * Adding gravity is not necessarily required in general but
             * to make jumps more snappy extra gravity is added per tick.
             * All clients and server need to simulate the gravity to keep
             * prediction equal across the network. */
            AddGravity();
        }

        private void TimeManager_OnPostTick()
        {
            /* Reconcile is sent during PostTick because we
             * want to send the rb data AFTER the simulation. */
            if (base.IsServer)
            {
                ReconcileData rd = new ReconcileData(transform.position, transform.rotation, _rigidbody.velocity, _rigidbody.angularVelocity);
                Reconciliation(rd, true);
            }
        }


        /// <summary>
        /// Builds a MoveData to use within replicate.
        /// </summary>
        /// <param name="md"></param>
        private void BuildMoveData(out MoveData md)
        {
            md = default;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            if (horizontal == 0f && vertical == 0f && !_jump)
                return;

            md = new MoveData(_jump, horizontal, vertical);
            _jump = false;
        }


        /// <summary>
        /// PredictedObject example (unpolished)
        /// </summary>
        private void TrySpawnBullet()
        {
            if (_spawnBullet)
            {
                _spawnBullet = false;

                NetworkObject nob = Instantiate(BulletPrefab, transform.position + (transform.forward * 1f), transform.rotation);
                //Set last spawned to test destroy with ALT key.
                _lastSpawnedBullet = nob;

                //Set force to 100f at current forward.
                PredictedBullet bt = nob.GetComponent<PredictedBullet>();
                bt.SetStartingForce(transform.forward * 20f);
                //Spawn client side, which will send the predicted spawn to server.
                base.Spawn(nob, base.Owner);
            }
        }

        /// <summary>
        /// PredictedObject example (unpolished)
        /// </summary>
        private void TryDespawnBullet()
        {
            if (_despawnBullet)
            {
                _despawnBullet = false;
                _lastSpawnedBullet?.Despawn();
            }
        }

        /// <summary>
        /// Adds gravity to the rigidbody.
        /// </summary>
        private void AddGravity()
        {
            _rigidbody.AddForce(Physics.gravity * 2f);
        }

        [Replicate]
        private void Move(MoveData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false)
        {
            Vector3 forces = new Vector3(md.Horizontal, 0f, md.Vertical) * _moveRate;
            _rigidbody.AddForce(forces);

            if (md.Jump)
                _rigidbody.AddForce(new Vector3(0f, _jumpForce, 0f), ForceMode.Impulse);
        }

        [Reconcile]
        private void Reconciliation(ReconcileData rd, bool asServer, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
            transform.rotation = rd.Rotation;
            _rigidbody.velocity = rd.Velocity;
            _rigidbody.angularVelocity = rd.AngularVelocity;
        }


    }


}