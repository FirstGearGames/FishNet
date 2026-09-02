using FishNet.Managing;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    /// <summary>
    /// Pauses and unpauses rigidbodies. While paused rigidbodies cannot be interacted with or simulated.
    /// </summary>
    public class RigidbodyPauser : IResettable
    {
        #region Types.
        /// <summary>
        /// Data for a rigidbody, including its transform and simulation state.
        /// </summary>
        public struct RigidbodyData
        {
            /// <summary>
            /// Rigidbody for data.
            /// </summary>
            public Rigidbody Rigidbody;
            /// <summary>
            /// Cached velocity.
            /// </summary>
            public Vector3 Velocity;
            /// <summary>
            /// Cached angular velocity.
            /// </summary>
            public Vector3 AngularVelocity;
            /// <summary>
            /// True if the rigidbody was kinematic.
            /// </summary>
            public bool IsKinematic;
            /// <summary>
            /// True if the rigidbody was detecting collisions.
            /// </summary>
            public bool DetectCollisions;
            /// <summary>
            /// Detection mode of the Rigidbody.
            /// </summary>
            public CollisionDetectionMode CollisionDetectionMode;
            /// <summary>
            /// World position of the rigidbody's transform.
            /// </summary>
            public Vector3 Position;
            /// <summary>
            /// World rotation of the rigidbody's transform.
            /// </summary>
            public Quaternion Rotation;

            public RigidbodyData(Rigidbody rb)
            {
                Rigidbody = rb;
                Velocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                IsKinematic = rb.isKinematic;
                DetectCollisions = rb.detectCollisions;
                CollisionDetectionMode = rb.collisionDetectionMode;
                Position = rb.transform.position;
                Rotation = rb.transform.rotation;
            }

            public void Update(Rigidbody rb)
            {
                #if UNITY_6000_1_OR_NEWER
                Velocity = rb.linearVelocity;
                #else
                Velocity = rb.velocity;
                #endif
                AngularVelocity = rb.angularVelocity;
                IsKinematic = rb.isKinematic;
                DetectCollisions = rb.detectCollisions;
                CollisionDetectionMode = rb.collisionDetectionMode;
                Position = rb.transform.position;
                Rotation = rb.transform.rotation;
            }
        }

        /// <summary>
        /// Data for a rigidbody2d, including its transform and simulation state.
        /// </summary>
        public struct Rigidbody2DData
        {
            /// <summary>
            /// Rigidbody for data.
            /// </summary>
            public Rigidbody2D Rigidbody2d;
            /// <summary>
            /// Cached velocity.
            /// </summary>
            public Vector2 Velocity;
            /// <summary>
            /// Cached angular velocity.
            /// </summary>
            public float AngularVelocity;
            /// <summary>
            /// True if the rigidbody was kinematic.
            /// </summary>
            public bool IsKinematic;
            /// <summary>
            /// True if the rigidbody was simulated.
            /// </summary>
            public bool Simulated;
            /// <summary>
            /// Detection mode of the rigidbody.
            /// </summary>
            public CollisionDetectionMode2D CollisionDetectionMode;
            /// <summary>
            /// World position of the rigidbody's transform.
            /// </summary>
            public Vector2 Position;
            /// <summary>
            /// World rotation of the rigidbody's transform, in degrees.
            /// </summary>
            public float Rotation;

            public Rigidbody2DData(Rigidbody2D rb)
            {
                Rigidbody2d = rb;
                Velocity = Vector2.zero;
                AngularVelocity = 0f;
                Simulated = rb.simulated;
                #if UNITY_6000_1_OR_NEWER
                IsKinematic = rb.bodyType == RigidbodyType2D.Kinematic;
                #else
                IsKinematic = rb.isKinematic;
                #endif
                CollisionDetectionMode = rb.collisionDetectionMode;
                Position = rb.position;
                Rotation = rb.rotation;
            }

            public void Update(Rigidbody2D rb)
            {
                #if UNITY_6000_1_OR_NEWER
                Velocity = rb.linearVelocity;
                #else
                Velocity = rb.velocity;
                #endif

                AngularVelocity = rb.angularVelocity;
                Simulated = rb.simulated;
                #if UNITY_6000_1_OR_NEWER
                IsKinematic = rb.bodyType == RigidbodyType2D.Kinematic;
                #else
                IsKinematic = rb.isKinematic;
                #endif
                CollisionDetectionMode = rb.collisionDetectionMode;
                Position = rb.position;
                Rotation = rb.rotation;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// True if the rigidbodies are considered paused.
        /// </summary>
        public bool Paused { get; private set; }
        #endregion

        #region Private.
        /// <summary>
        /// Rigidbody datas for found rigidbodies.
        /// </summary>
        private List<RigidbodyData> _rigidbodyDatas = new();
        /// <summary>
        /// Rigidbody2D datas for found rigidbodies;
        /// </summary>
        private List<Rigidbody2DData> _rigidbody2dDatas = new();
        /// <summary>
        /// Snapshot taken when pausing, applied when unpausing.
        /// </summary>
        private List<RigidbodyData> _pauseSnapshot = new();
        /// <summary>
        /// Snapshot taken when pausing 2D rigidbodies, applied when unpausing.
        /// </summary>
        private List<Rigidbody2DData> _pauseSnapshot2d = new();
        /// <summary>
        /// True to get rigidbodies in children of transform.
        /// </summary>
        private bool _getInChildren;
        /// <summary>
        /// Transform to get rigidbodies on.
        /// </summary>
        private Transform _transform;
        /// <summary>
        /// Type of prediction movement which is being used.
        /// </summary>
        private RigidbodyType _rigidbodyType;
        /// <summary>
        /// True if initialized at least once.
        /// </summary>
        private bool _initialized;
        #endregion

        /// <summary>
        /// Assigns rigidbodies using initialized settings.
        /// </summary>
        public void UpdateRigidbodies()
        {
            if (!_initialized)
            {
                InstanceFinder.NetworkManager.LogError($"T{GetType().Name} has not been initialized yet. This method cannot be used.");
                return;
            }

            UpdateRigidbodies(_transform, _rigidbodyType, _getInChildren);
        }

        /// <summary>
        /// Assigns rigidbodies manually and initializes component.
        /// </summary>
        public void UpdateRigidbodies(Rigidbody[] rbs)
        {
            List<Rigidbody> rigidbodies = CollectionCaches<Rigidbody>.RetrieveList();
            foreach (Rigidbody rb in rbs)
                rigidbodies.Add(rb);

            UpdateRigidbodies(rigidbodies);

            CollectionCaches<Rigidbody>.Store(rigidbodies);
        }

        /// <summary>
        /// Assigns rigidbodies manually and initializes component.
        /// </summary>
        private void UpdateRigidbodies(List<Rigidbody> rbs)
        {
            _rigidbodyDatas.Clear();

            foreach (Rigidbody rb in rbs)
                _rigidbodyDatas.Add(new(rb));

            _initialized = true;
        }

        /// <summary>
        /// Assigns rigidbodies manually and initializes component.
        /// </summary>
        public void UpdateRigidbodies2D(Rigidbody2D[] rbs)
        {
            List<Rigidbody2D> rigidbodies = CollectionCaches<Rigidbody2D>.RetrieveList();
            foreach (Rigidbody2D rb in rbs)
                rigidbodies.Add(rb);

            UpdateRigidbodies2D(rigidbodies);

            CollectionCaches<Rigidbody2D>.Store(rigidbodies);
        }

        /// <summary>
        /// Assigns rigidbodies manually and initializes component.
        /// </summary>
        private void UpdateRigidbodies2D(List<Rigidbody2D> rbs)
        {
            _rigidbody2dDatas.Clear();

            foreach (Rigidbody2D rb in rbs)
                _rigidbody2dDatas.Add(new(rb));

            _initialized = true;
        }

        /// <summary>
        /// Assigns rigidbodies.
        /// </summary>
        /// <param name = "rbs">Rigidbodies2D to use.</param>
        public void UpdateRigidbodies(Transform t, RigidbodyType rbType, bool getInChildren)
        {
            _rigidbodyType = rbType;
            _getInChildren = getInChildren;

            // 3D.
            if (rbType == RigidbodyType.Rigidbody)
            {
                List<Rigidbody> rigidbodies = CollectionCaches<Rigidbody>.RetrieveList();

                if (getInChildren)
                {
                    Rigidbody[] rbs = t.GetComponentsInChildren<Rigidbody>();
                    for (int i = 0; i < rbs.Length; i++)
                        rigidbodies.Add(rbs[i]);
                }
                else
                {
                    Rigidbody rb = t.GetComponent<Rigidbody>();
                    if (rb != null)
                        rigidbodies.Add(rb);
                }

                UpdateRigidbodies(rigidbodies);
                CollectionCaches<Rigidbody>.Store(rigidbodies);
            }
            // 2D.
            else
            {
                List<Rigidbody2D> rigidbodies = CollectionCaches<Rigidbody2D>.RetrieveList();

                if (getInChildren)
                {
                    Rigidbody2D[] rbs = t.GetComponentsInChildren<Rigidbody2D>();
                    for (int i = 0; i < rbs.Length; i++)
                        rigidbodies.Add(rbs[i]);
                }
                else
                {
                    Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
                    if (rb != null)
                        rigidbodies.Add(rb);
                }

                UpdateRigidbodies2D(rigidbodies);
                CollectionCaches<Rigidbody2D>.Store(rigidbodies);
            }
        }

        /// <summary>
        /// Fills a list with a snapshot of the current transform and simulation state of every tracked rigidbody.
        /// </summary>
        /// <param name = "snapshot">List to populate; it is cleared first.</param>
        public void GetSnapshot(List<RigidbodyData> snapshot)
        {
            snapshot.Clear();
            for (int i = 0; i < _rigidbodyDatas.Count; i++)
            {
                Rigidbody rb = _rigidbodyDatas[i].Rigidbody;
                if (rb == null)
                    continue;

                RigidbodyData data = new(rb);
                data.Update(rb);
                snapshot.Add(data);
            }
        }

        /// <summary>
        /// Fills a list with a snapshot of the current transform and simulation state of every tracked 2D rigidbody.
        /// </summary>
        /// <param name = "snapshot">List to populate; it is cleared first.</param>
        public void GetSnapshot(List<Rigidbody2DData> snapshot)
        {
            snapshot.Clear();
            for (int i = 0; i < _rigidbody2dDatas.Count; i++)
            {
                Rigidbody2D rb = _rigidbody2dDatas[i].Rigidbody2d;
                if (rb == null)
                    continue;

                Rigidbody2DData data = new(rb);
                data.Update(rb);
                snapshot.Add(data);
            }
        }

        /// <summary>
        /// Applies a snapshot, setting each rigidbody's transform (and optionally its simulation state) back to the stored values.
        /// </summary>
        /// <param name = "snapshot">Snapshot to apply. Null is ignored.</param>
        /// <param name = "restoreSimulationState">True to restore isKinematic, detectCollisions and collisionDetectionMode (used when unpausing).
        /// False to leave those as they are and only restore transform and velocity — the kinematic state is owned by other systems and must not be forced by a reconcile correction.</param>
        public void ApplySnapshot(List<RigidbodyData> snapshot, bool restoreSimulationState = true)
        {
            if (snapshot == null)
                return;

            for (int i = 0; i < snapshot.Count; i++)
            {
                RigidbodyData data = snapshot[i];
                Rigidbody rb = data.Rigidbody;
                if (rb == null)
                    continue;

                rb.position = data.Position;
                rb.rotation = data.Rotation;

                if (restoreSimulationState)
                {
                    rb.isKinematic = data.IsKinematic;
                    rb.detectCollisions = data.DetectCollisions;
                    rb.collisionDetectionMode = data.CollisionDetectionMode;
                }

                //Velocities cannot be set on a kinematic rigidbody; check the actual current state.
                if (!rb.isKinematic)
                {
                    #if UNITY_6000_1_OR_NEWER
                    rb.linearVelocity = data.Velocity;
                    #else
                    rb.velocity = data.Velocity;
                    #endif
                    rb.angularVelocity = data.AngularVelocity;
                }
            }
        }

        /// <summary>
        /// Applies a snapshot, setting each 2D rigidbody's transform (and optionally its simulation state) back to the stored values.
        /// </summary>
        /// <param name = "snapshot">Snapshot to apply. Null is ignored.</param>
        /// <param name = "restoreSimulationState">True to restore bodyType, simulated and collisionDetectionMode (used when unpausing).
        /// False to leave those as they are and only restore transform and velocity — the kinematic state is owned by other systems and must not be forced by a reconcile correction.</param>
        public void ApplySnapshot(List<Rigidbody2DData> snapshot, bool restoreSimulationState = true)
        {
            if (snapshot == null)
                return;

            for (int i = 0; i < snapshot.Count; i++)
            {
                Rigidbody2DData data = snapshot[i];
                Rigidbody2D rb = data.Rigidbody2d;
                if (rb == null)
                    continue;

                rb.position = data.Position;
                rb.rotation = data.Rotation;

                if (restoreSimulationState)
                {
                    #if UNITY_6000_1_OR_NEWER
                    rb.bodyType = data.IsKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
                    #else
                    rb.isKinematic = data.IsKinematic;
                    #endif
                    rb.simulated = data.Simulated;
                    rb.collisionDetectionMode = data.CollisionDetectionMode;
                }

                //Velocities can only be set on a dynamic body; check the actual current state.
                #if UNITY_6000_1_OR_NEWER
                bool canSetVelocity = rb.bodyType == RigidbodyType2D.Dynamic;
                #else
                bool canSetVelocity = !rb.isKinematic;
                #endif
                if (canSetVelocity)
                {
                    #if UNITY_6000_1_OR_NEWER
                    rb.linearVelocity = data.Velocity;
                    #else
                    rb.velocity = data.Velocity;
                    #endif
                    rb.angularVelocity = data.AngularVelocity;
                }
            }
        }

        /// <summary>
        /// Pauses rigidbodies preventing them from interacting. The pre-pause state is snapshotted so Unpause can restore it.
        /// </summary>
        public void Pause()
        {
            if (Paused)
                return;
            Paused = true;

            // 3D.
            if (_rigidbodyType == RigidbodyType.Rigidbody)
            {
                //Snapshot the current state, then freeze every rigidbody.
                GetSnapshot(_pauseSnapshot);

                for (int i = 0; i < _rigidbodyDatas.Count; i++)
                {
                    Rigidbody rb = _rigidbodyDatas[i].Rigidbody;
                    if (rb == null)
                    {
                        _rigidbodyDatas.RemoveAt(i);
                        i--;
                        continue;
                    }

                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                }
            }
            // 2D.
            else
            {
                GetSnapshot(_pauseSnapshot2d);

                for (int i = 0; i < _rigidbody2dDatas.Count; i++)
                {
                    Rigidbody2D rb = _rigidbody2dDatas[i].Rigidbody2d;
                    if (rb == null)
                    {
                        _rigidbody2dDatas.RemoveAt(i);
                        i--;
                        continue;
                    }

                    rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                    #if UNITY_6000_1_OR_NEWER
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    #else
                    rb.isKinematic = true;
                    #endif
                    rb.simulated = false;
                }
            }
        }

        /// <summary>
        /// Unpauses rigidbodies allowing them to interact normally, restoring the state snapshotted during Pause.
        /// </summary>
        public void Unpause()
        {
            if (!Paused)
                return;
            Paused = false;

            if (_rigidbodyType == RigidbodyType.Rigidbody)
                ApplySnapshot(_pauseSnapshot);
            else
                ApplySnapshot(_pauseSnapshot2d);
        }

        public void ResetState()
        {
            _rigidbodyDatas.Clear();
            _rigidbody2dDatas.Clear();
            _pauseSnapshot.Clear();
            _pauseSnapshot2d.Clear();
            _getInChildren = default;
            _transform = default;
            _rigidbodyType = default;
            _initialized = default;
            Paused = default;
        }

        public void InitializeState() { }
    }
}