using FishNet.Managing;
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
            /// <summary>
            /// Scene of this rigidbody when being set kinematic.
            /// </summary>
            public Scene SimulatedScene;
            /// <summary>
            /// True if the rigidbody was kinematic prior to being paused.
            /// </summary>
            public bool IsKinematic;

            public RigidbodyData(Rigidbody rb)
            {
                Rigidbody = rb;
                Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                Velocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                SimulatedScene = rb.gameObject.scene;
                IsKinematic = rb.isKinematic;
            }

            public void Update(Rigidbody rb)
            {
                Velocity = rb.velocity;
                AngularVelocity = rb.angularVelocity;
                SimulatedScene = rb.gameObject.scene;
                IsKinematic = rb.isKinematic;
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
            /// <summary>
            /// Scene of this rigidbody when being set kinematic.
            /// </summary>
            public Scene SimulatedScene;
            /// <summary>
            /// True if the rigidbody was simulated prior to being paused.
            /// </summary>
            public bool Simulated;

            public Rigidbody2DData(Rigidbody2D rb)
            {
                Rigidbody2d = rb;
                Rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                Velocity = Vector2.zero;
                AngularVelocity = 0f;
                SimulatedScene = rb.gameObject.scene;
                Simulated = rb.simulated;
            }

            public void Update(Rigidbody2D rb)
            {
                Velocity = rb.velocity;
                AngularVelocity = rb.angularVelocity;
                SimulatedScene = rb.gameObject.scene;
                Simulated = rb.simulated;
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
        private List<RigidbodyData> _rigidbodyDatas = new List<RigidbodyData>();
        /// <summary>
        /// Rigidbody2D datas for found rigidbodies;
        /// </summary>
        private List<Rigidbody2DData> _rigidbody2dDatas = new List<Rigidbody2DData>();
        /// <summary>
        /// Type of prediction movement which is being used.
        /// </summary>
        private RigidbodyType _rigidbodyType;
        /// <summary>
        /// 
        /// </summary>
        private static Scene _kinematicSceneCache;
        /// <summary>
        /// Scene used to simulate kinematic rigidbodies.
        /// </summary>
        private static Scene _kinematicScene
        {
            get
            {
                if (!_kinematicSceneCache.IsValid())
                    _kinematicSceneCache = SceneManager.CreateScene("RigidbodyPauser_Kinematic", new CreateSceneParameters(LocalPhysicsMode.Physics2D | LocalPhysicsMode.Physics3D));
                return _kinematicSceneCache;
            }
        }
        /// <summary>
        /// Parent of GraphicalObject prior to unparenting.
        /// </summary>
        private Transform _graphicalParent;
        /// <summary>
        /// GraphicalObject to unparent when pausing.
        /// </summary>
        private Transform _graphicalObject;
        #endregion

        /// <summary>
        /// Assigns rigidbodies.
        /// </summary>
        /// <param name="rbs">Rigidbodies2D to use.</param>
        public void UpdateRigidbodies(Transform t, RigidbodyType rbType, bool getInChildren, Transform graphicalObject)
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

                //Make sure all added datas are not the graphical object.
                for (int i = 0; i < _rigidbodyDatas.Count; i++)
                {
                    if (_rigidbodyDatas[i].Rigidbody.transform == graphicalObject)
                    {
                        NetworkManager.StaticLogError($"GameObject {t.name} has it's GraphicalObject as a child or on the same object as a Rigidbody object. The GraphicalObject must be a child of root, and not sit beneath or on any rigidbodies.");
                        graphicalObject = null;
                    }
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

                //Make sure all added datas are not the graphical object.
                for (int i = 0; i < _rigidbody2dDatas.Count; i++)
                {
                    if (_rigidbody2dDatas[i].Rigidbody2d.transform == graphicalObject)
                    {
                        NetworkManager.StaticLogError($"GameObject {t.name} has it's GraphicalObject as a child or on the same object as a Rigidbody object. The GraphicalObject must be a child of root, and not sit beneath or on any rigidbodies.");
                        graphicalObject = null;
                    }
                }
            }

            if (graphicalObject != null)
            {
                _graphicalObject = graphicalObject;
                _graphicalParent = graphicalObject.parent;
            }
        }

        /// <summary>
        /// Unpauses rigidbodies allowing them to interact normally.
        /// </summary>
        public void Unpause()
        {
            if (!Paused)
                return;
            Paused = false;

            //3D.
            if (_rigidbodyType == RigidbodyType.Rigidbody)
            {
                for (int i = 0; i < _rigidbodyDatas.Count; i++)
                {
                    if (!UnpauseRigidbody(i))
                    {
                        _rigidbodyDatas.RemoveAt(i);
                        i--;
                    }
                }

                //Sets isKinematic status and returns if successful.
                bool UnpauseRigidbody(int index)
                {
                    RigidbodyData rbData = _rigidbodyDatas[index];
                    Rigidbody rb = rbData.Rigidbody;
                    if (rb == null)
                        return false;

                    rb.velocity = rbData.Velocity;
                    rb.angularVelocity = rbData.AngularVelocity;
                    rb.isKinematic = rbData.IsKinematic;
                    SceneManager.MoveGameObjectToScene(rb.transform.root.gameObject, rbData.SimulatedScene);
                    return true;
                }
            }
            //2D.
            else
            {
                for (int i = 0; i < _rigidbody2dDatas.Count; i++)
                {
                    if (!UnpauseRigidbody(i))
                    {
                        _rigidbody2dDatas.RemoveAt(i);
                        i--;
                    }
                }

                //Sets isKinematic status and returns if successful.
                bool UnpauseRigidbody(int index)
                {
                    Rigidbody2DData rbData = _rigidbody2dDatas[index];
                    Rigidbody2D rb = rbData.Rigidbody2d;
                    if (rb == null)
                        return false;

                    rb.velocity = rbData.Velocity;
                    rb.angularVelocity = rbData.AngularVelocity;
                    rb.simulated = rbData.Simulated;
                    rb.isKinematic = !rbData.Simulated;
                    SceneManager.MoveGameObjectToScene(rb.transform.root.gameObject, rbData.SimulatedScene);
                    return true;
                }
            }

            //Parent went null, then graphicalObject needs to be destroyed.
            if (_graphicalParent == null && _graphicalObject != null)
                MonoBehaviour.Destroy(_graphicalObject.gameObject);
            else
                _graphicalObject?.SetParent(_graphicalParent);

        }

        /// <summary>
        /// Pauses rigidbodies preventing them from interacting.
        /// </summary>
        public void Pause()
        {
            if (Paused)
                return;
            Paused = true;

            _graphicalObject?.SetParent(null);
            Scene kinematicScene = _kinematicScene;

            //3D.
            if (_rigidbodyType == RigidbodyType.Rigidbody)
            {
                for (int i = 0; i < _rigidbodyDatas.Count; i++)
                {
                    if (!PauseRigidbody(i))
                    {
                        _rigidbodyDatas.RemoveAt(i);
                        i--;
                    }
                }

                //Sets isKinematic status and returns if successful.
                bool PauseRigidbody(int index)
                {
                    RigidbodyData rbData = _rigidbodyDatas[index];
                    Rigidbody rb = rbData.Rigidbody;
                    if (rb == null)
                        return false;

                    rbData.Update(rb);
                    _rigidbodyDatas[index] = rbData;
                    SceneManager.MoveGameObjectToScene(rb.transform.root.gameObject, kinematicScene);
                    return true;
                }
            }
            //2D.
            else
            {
                for (int i = 0; i < _rigidbody2dDatas.Count; i++)
                {
                    if (!PauseRigidbody(i))
                    {
                        _rigidbody2dDatas.RemoveAt(i);
                        i--;
                    }
                }

                //Sets isKinematic status and returns if successful.
                bool PauseRigidbody(int index)
                {
                    Rigidbody2DData rbData = _rigidbody2dDatas[index];
                    Rigidbody2D rb = rbData.Rigidbody2d;
                    if (rb == null)
                        return false;

                    rbData.Update(rb);
                    _rigidbody2dDatas[index] = rbData;
                    SceneManager.MoveGameObjectToScene(rb.transform.root.gameObject, kinematicScene);
                    return true;
                }
            }
        }
    }


}