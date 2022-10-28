using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Component.Prediction
{
    /// <summary>
    /// Pauses and unpauses rigidbodies. While paused rigidbodies cannot be interacted with or simulated.
    /// </summary>
    public class RigidbodyPauser
    {
        #region Types.
        /// <summary>
        /// Data for a rigidbody before being set kinematic.
        /// </summary>
        private struct RigidbodyData
        {
            /// <summary>
            /// Rigidbody for data.
            /// </summary>
            public Rigidbody Rigidbody;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            public Vector3 Velocity;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            public Vector3 AngularVelocity;

            public RigidbodyData(Rigidbody rigidbody)
            {
                Rigidbody = rigidbody;
                Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                Velocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
            }
        }
        /// <summary>
        /// Data for a rigidbody2d before being set kinematic.
        /// </summary>
        private struct Rigidbody2DData
        {
            /// <summary>
            /// Rigidbody for data.
            /// </summary>
            public Rigidbody2D Rigidbody2d;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            public Vector2 Velocity;
            /// <summary>
            /// Cached velocity when being set kinematic.
            /// </summary>
            public float AngularVelocity;

            public Rigidbody2DData(Rigidbody2D rigidbody)
            {
                Rigidbody2d = rigidbody;
                Rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                Velocity = Vector2.zero;
                AngularVelocity = 0f;
            }

            public void Update(Rigidbody2D rb)
            {
                Velocity = rb.velocity;
                AngularVelocity = rb.angularVelocity;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Last value set while calling ChangeKinematic.
        /// </summary>
        public bool LastChangeKinematicValue { get; private set; }
        #endregion

        #region Private.
        /// <summary>
        /// Rigidbody datas for found rigidbodies.
        /// </summary>
        private List<RigidbodyData> _rigidbodyDatas = new List<RigidbodyData>();
        /// <summary>
        /// Rigidbody2D datas for found rigidbodies;
        /// </summary>
        private List<Rigidbody2DData> _rigidbody2dDatas = new List<Rigidbody2DData>();
        /// <summary>
        /// Colliders to disable and enable.
        /// </summary>
        private List<Collider> _colliders = new List<Collider>();
        /// <summary>
        /// Colliders2D to eable and disable.
        /// </summary>
        private List<Collider2D> _colliders2d = new List<Collider2D>();
        /// <summary>
        /// Type of prediction movement which is being used.
        /// </summary>
        private RigidbodyType _rigidbodyType;
        #endregion

        /// <summary>
        /// Assigns rigidbodies.
        /// </summary>
        /// <param name="rbs">Rigidbodies2D to use.</param>
        public void UpdateRigidbodies(Transform t, RigidbodyType rbType, bool getInChildren)
        {
            _rigidbodyType = rbType;
            _rigidbodyDatas.Clear();
            _rigidbody2dDatas.Clear();

            //3D.
            if (rbType == RigidbodyType.Rigidbody)
            {
                if (getInChildren)
                {
                    Rigidbody[] rbs = t.GetComponentsInChildren<Rigidbody>();
                    for (int i = 0; i < rbs.Length; i++)
                        _rigidbodyDatas.Add(new RigidbodyData(rbs[i]));
                }
                else
                {
                    Rigidbody rb = t.GetComponent<Rigidbody>();
                    if (rb != null)
                        _rigidbodyDatas.Add(new RigidbodyData(rb));
                }
            }
            //2D.
            else
            {
                if (getInChildren)
                {
                    Rigidbody2D[] rbs = t.GetComponentsInChildren<Rigidbody2D>();
                    for (int i = 0; i < rbs.Length; i++)
                        _rigidbody2dDatas.Add(new Rigidbody2DData(rbs[i]));
                }
                else
                {
                    Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
                    if (rb != null)
                        _rigidbody2dDatas.Add(new Rigidbody2DData(rb));
                }
            }

            UpdateColliders(t, rbType, getInChildren);
        }

        /// <summary>
        /// Updates colliders on root and children depending upon settings.
        /// </summary>
        private void UpdateColliders(Transform t, RigidbodyType rbType, bool getInChildren)
        {
            _colliders.Clear();
            _colliders2d.Clear();

            //3D.
            if (rbType == RigidbodyType.Rigidbody)
            {
                Collider[] cs;
                if (getInChildren)
                    cs = t.GetComponentsInChildren<Collider>();
                else
                    cs = t.GetComponents<Collider>();

                foreach (Collider item in cs)
                {
                    //Only add if enabled. We do not want to toggle intentionally disabled colliders on.
                    if (item.enabled)
                        _colliders.Add(item);
                }
            }
            //2D.
            else
            {
                Collider2D[] cs;
                if (getInChildren)
                    cs = t.GetComponentsInChildren<Collider2D>();
                else
                    cs = t.GetComponents<Collider2D>();

                foreach (Collider2D item in cs)
                {
                    //Only add if enabled. We do not want to toggle intentionally disabled colliders on.
                    if (item.enabled)
                        _colliders2d.Add(item);
                }
            }
        }

        /// <summary>
        /// Changes IsKinematic for rigidbodies.
        /// </summary>
        /// <param name="isKinematic"></param>
        public void ChangeKinematic(bool isKinematic)
        {
            if (isKinematic == LastChangeKinematicValue)
                return;
            LastChangeKinematicValue = isKinematic;

            //3D.
            if (_rigidbodyType == RigidbodyType.Rigidbody)
            {
                //Enable or disable colliders.
                for (int i = 0; i < _colliders.Count; i++)
                    _colliders[i].enabled = !isKinematic;

                for (int i = 0; i < _rigidbodyDatas.Count; i++)
                {
                    if (!SetIsKinematic(i))
                    {
                        _rigidbodyDatas.RemoveAt(i);
                        i--;
                    }
                }

                //Sets isKinematic status and returns if successful.
                bool SetIsKinematic(int index)
                {
                    RigidbodyData rbData = _rigidbodyDatas[index];
                    Rigidbody rb = rbData.Rigidbody;
                    if (rb == null)
                        return false;

                    //IsKinematic.
                    if (isKinematic)
                    {
                        rbData.Velocity = rb.velocity;
                        rbData.AngularVelocity = rb.angularVelocity;
                        rb.isKinematic = true;
                        //Update data.
                        _rigidbodyDatas[index] = rbData;
                    }
                    else
                    {
                        rb.isKinematic = false;
                        rb.velocity = rbData.Velocity;
                        rb.angularVelocity = rbData.AngularVelocity;
                    }

                    return true;
                }
            }
            //2D.
            else
            {
                bool simulated = !isKinematic;
                //Enable or disable colliders.
                for (int i = 0; i < _colliders2d.Count; i++)
                    _colliders2d[i].enabled = simulated;

                for (int i = 0; i < _rigidbody2dDatas.Count; i++)
                {
                    if (!SetSimulated(i))
                    {
                        _rigidbody2dDatas.RemoveAt(i);
                        i--;
                    }
                }

                //Sets isKinematic status and returns if successful.
                bool SetSimulated(int index)
                {
                    Rigidbody2DData rbData = _rigidbody2dDatas[index];
                    Rigidbody2D rb = rbData.Rigidbody2d;
                    if (rb == null)
                        return false;

                    if (!simulated)
                    {
                        rbData.Update(rb);
                        rb.velocity = Vector2.zero;
                        rb.angularVelocity = 0f;
                        rb.isKinematic = true;
                        rb.simulated = false;
                        _original = rb.gameObject.scene;
                        //Update data.
                        _rigidbody2dDatas[index] = rbData;
                    }
                    else
                    {
                        rb.isKinematic = false;
                        rb.simulated = true;
                        rb.velocity = rbData.Velocity;
                        rb.angularVelocity = rbData.AngularVelocity;
                    }

                    return true;
                }
            }
        }

        private Scene SSS
        {
            get
            {
                if (!_sdfkj43fkjsd.IsValid())
                    _sdfkj43fkjsd = SceneManager.CreateScene("sdfsdfs", new CreateSceneParameters() { localPhysicsMode = LocalPhysicsMode.Physics2D });

                return _sdfkj43fkjsd;
            }
        }

        private Scene _sdfkj43fkjsd;
        private Scene _original;
    }


}