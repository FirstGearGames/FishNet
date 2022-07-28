using FishNet.Managing.Timing;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public partial class OfflineRigidbody : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// Type of prediction movement being used.
        /// </summary>
        private enum RigidbodyType : byte
        {
            Rigidbody = 0,
            Rigidbody2D = 1
        }
        /// <summary>
        /// Data for a rigidbody before being set kinematic.
        /// </summary>
        private struct RigidbodyData
        {
            /// <summary>
            /// Rigidbody for data.
            /// </summary>
            private Rigidbody _rigidbody;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            private Vector3 _velocity;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            private Vector3 _angularVelocity;

            public RigidbodyData(Rigidbody rigidbody)
            {
                _rigidbody = rigidbody;
                _velocity = Vector3.zero;
                _angularVelocity = Vector3.zero;
            }

            /// <summary>
            /// Sets isKinematic status and returns if successful.
            /// </summary>
            public bool SetIsKinematic(bool isKinematic)
            {
                if (_rigidbody == null)
                    return false;

                if (!isKinematic)
                {
                    _velocity = _rigidbody.velocity;
                    _angularVelocity = _rigidbody.angularVelocity;
                }
                else
                {
                    _rigidbody.velocity = _velocity;
                    _rigidbody.angularVelocity = _angularVelocity;
                }

                return true;
            }
        }
        /// <summary>
        /// Data for a rigidbody2d before being set kinematic.
        /// </summary>
        private struct RigidbodyData2D
        {
            /// <summary>
            /// Rigidbody for data.
            /// </summary>
            private Rigidbody2D _rigidbody2d;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            private Vector2 _velocity;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            private float _angularVelocity;

            public RigidbodyData2D(Rigidbody2D rigidbody)
            {
                _rigidbody2d = rigidbody;
                _velocity = Vector2.zero;
                _angularVelocity = 0f;
            }

            /// <summary>
            /// Sets simulated status and returns if successful.
            /// </summary>
            public bool SetSimulated(bool simulated)
            {
                if (_rigidbody2d == null)
                    return false;

                if (!simulated)
                {
                    _velocity = _rigidbody2d.velocity;
                    _angularVelocity = _rigidbody2d.angularVelocity;
                }
                else
                {
                    _rigidbody2d.velocity = _velocity;
                    _rigidbody2d.angularVelocity = _angularVelocity;
                }

                return true;
            }
        }
        #endregion

        #region Serialized.
        [Header("This component is experimental! Please report any problems you may encounter.")]
        /// <summary>
        /// Type of prediction movement which is being used.
        /// </summary>
        [Tooltip("Type of prediction movement which is being used.")]
        [SerializeField]
        private RigidbodyType _rigidbodyType;
        /// <summary>
        /// True to also get rigidbody components within children.
        /// </summary>
        [Tooltip("True to also get rigidbody components within children.")]
        [SerializeField]
        private bool _getInChildren;
        #endregion

        #region Private.
        /// <summary>
        /// Rigidbody datas for found rigidbodies.
        /// </summary>
        private List<RigidbodyData> _rigidbodyDatas = new List<RigidbodyData>();
        /// <summary>
        /// Rigidbody2D datas for found rigidbodies;
        /// </summary>
        private List<RigidbodyData2D> _rigidbodyDatas2d = new List<RigidbodyData2D>();
        /// <summary>
        /// TimeManager subscribed to.
        /// </summary>
        private TimeManager _timeManager;
        #endregion


        private void Awake()
        {
            InitializeOnce();
        }


        private void OnDestroy()
        {
            ChangeSubscription(false);
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void InitializeOnce()
        {
            _timeManager = InstanceFinder.TimeManager;
            UpdateRigidbodies();
            ChangeSubscription(true);
        }

        /// <summary>
        /// Sets a new TimeManager to use.
        /// </summary>
        /// <param name="tm"></param>
        public void SetTimeManager(TimeManager tm)
        {
            if (tm == _timeManager)
                return;

            //Unsub from current.
            ChangeSubscription(false);
            //Sub to newest.
            _timeManager = tm;
            ChangeSubscription(true);
        }

        /// <summary>
        /// Finds and assigns rigidbodie using configured settings.
        /// </summary>
        public void UpdateRigidbodies()
        {
            //3D.
            if (_rigidbodyType == RigidbodyType.Rigidbody)
            {
                _rigidbodyDatas.Clear();
                if (_getInChildren)
                {
                    Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
                    for (int i = 0; i < rbs.Length; i++)
                        _rigidbodyDatas.Add(new RigidbodyData(rbs[i]));
                }
                else
                {
                    if (gameObject.TryGetComponent<Rigidbody>(out Rigidbody rb))
                        _rigidbodyDatas.Add(new RigidbodyData(rb));
                }
            }
            //2D.
            else
            {
                _rigidbodyDatas2d.Clear();
                if (_getInChildren)
                {
                    Rigidbody2D[] rbs = GetComponentsInChildren<Rigidbody2D>(true);
                    for (int i = 0; i < rbs.Length; i++)
                        _rigidbodyDatas2d.Add(new RigidbodyData2D(rbs[i]));
                }
                else
                {
                    if (gameObject.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                        _rigidbodyDatas2d.Add(new RigidbodyData2D(rb));
                }
            }
        }

        /// <summary>
        /// Changes the subscription to the TimeManager.
        /// </summary>
        private void ChangeSubscription(bool subscribe)
        {
            if (_timeManager == null)
                return;

            if (subscribe)
            {
                _timeManager.OnPreReplicateReplay += _timeManager_OnPreReplicateReplay;
                _timeManager.OnPostReplicateReplay += _timeManager_OnPostReplicateReplay;
            }
            else
            {
                _timeManager.OnPreReplicateReplay -= _timeManager_OnPreReplicateReplay;
                _timeManager.OnPostReplicateReplay -= _timeManager_OnPostReplicateReplay;
            }
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void _timeManager_OnPreReplicateReplay(PhysicsScene arg1, PhysicsScene2D arg2)
        {
            ChangeKinematic(true);
        }

        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void _timeManager_OnPostReplicateReplay(PhysicsScene arg1, PhysicsScene2D arg2)
        {
            ChangeKinematic(false);
        }

        /// <summary>
        /// Changes IsKinematic for rigidbodies.
        /// </summary>
        /// <param name="isKinematic"></param>
        private void ChangeKinematic(bool isKinematic)
        {
            if (!this.enabled)
                return;

            //3D.
            if (_rigidbodyType == RigidbodyType.Rigidbody)
            {
                for (int i = 0; i < _rigidbodyDatas.Count; i++)
                {
                    if (!_rigidbodyDatas[i].SetIsKinematic(isKinematic))
                    {
                        _rigidbodyDatas.RemoveAt(i);
                        i--;
                    }
                }
            }
            //2D.
            else
            {
                for (int i = 0; i < _rigidbodyDatas2d.Count; i++)
                {
                    if (!_rigidbodyDatas2d[i].SetSimulated(!isKinematic))
                    {
                        _rigidbodyDatas2d.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

    }


}