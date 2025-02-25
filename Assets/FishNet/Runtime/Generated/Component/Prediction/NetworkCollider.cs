using FishNet.Object;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using TimeManagerCls = FishNet.Managing.Timing.TimeManager;

namespace FishNet.Component.Prediction
{
    public abstract class NetworkCollider : NetworkBehaviour
    {
        #region Types.
        private struct CollisionData
        {
            /// <summary>
            /// Tick when entering collision.
            /// </summary>
            public uint EnterTick;
            /// <summary>
            /// Tick when exiting collision.
            /// </summary>
            public uint ExitTick;

            public CollisionData(uint enterTick) : this()
            {
                EnterTick = enterTick;
                ExitTick = FishNet.Managing.Timing.TimeManager.UNSET_TICK;
            }

            public CollisionData(uint enterTick, uint exitTick) : this()
            {
                EnterTick = enterTick;
                ExitTick = exitTick;
            }
        }
        #endregion

        /// <summary>
        /// Called when another collider enters this collider.
        /// </summary>
        public event Action<Collider> OnEnter;
        /// <summary>
        /// Called when another collider stays in this collider.
        /// </summary>
        public event Action<Collider> OnStay;
        /// <summary>
        /// Called when another collider exits this collider.
        /// </summary>
        public event Action<Collider> OnExit;
        /// <summary>
        /// True to run collisions for colliders which are triggers, false to run collisions for colliders which are not triggers.
        /// </summary>
        [HideInInspector]
        protected bool IsTrigger;
        /// <summary>
        /// Maximum number of simultaneous hits to check for. Larger values decrease performance but allow detection to work for more overlapping colliders. Typically the default value of 16 is more than sufficient.
        /// </summary>
        [Tooltip("Maximum number of simultaneous hits to check for. Larger values decrease performance but allow detection to work for more overlapping colliders. Typically the default value of 16 is more than sufficient.")]
        [SerializeField]
        private ushort _maximumSimultaneousHits = 16;
        /// <summary>
        /// Units to extend collision traces by. This is used to prevent missed overlaps when colliders do not intersect enough.
        /// </summary>
        [Tooltip("Units to extend collision traces by. This is used to prevent missed overlaps when colliders do not intersect enough.")]
        [Range(0f, 100f)]
        [SerializeField]
        private float _additionalSize = 0.1f;
        /// <summary>
        /// Layers to trace on. This is used when value is not nothing.
        /// </summary>
        [Tooltip("Layers to trace on. This is used when value is not nothing.")]
        [SerializeField]
        private LayerMask _layers = (LayerMask)0;

        /// <summary>
        /// The colliders on this object.
        /// </summary>
        private Collider[] _colliders;
        /// <summary>
        /// The hits from the last check.
        /// </summary>
        private Collider[] _hits;
        // /// <summary>
        // /// The history of collider data.
        // /// </summary>
        // private ResettableRingBuffer<ColliderData> _colliderDataHistory;
        private Dictionary<Collider, CollisionData> _enteredColliders;

        /// <summary>
        /// True if colliders have been searched for at least once.
        /// We cannot check the null state on _colliders because Unity has a habit of initializing collections on it's own.
        /// </summary>
        private bool _collidersFound;
        /// <summary>
        /// Last layer of the gameObject.
        /// </summary>
        private int _lastGameObjectLayer = -1;
        /// <summary>
        /// Interactable layers for the layer of this gameObject.
        /// </summary>
        private int _interactableLayers;

        protected virtual void Awake()
        {
            //_colliderDataHistory = ResettableCollectionCaches<ColliderData>.RetrieveRingBuffer();
            //_colliderDataHistory = new();
            _enteredColliders = CollectionCaches<Collider, CollisionData>.RetrieveDictionary();
            _hits = CollectionCaches<Collider>.RetrieveArray();
            if (_hits.Length < _maximumSimultaneousHits)
                _hits = new Collider[_maximumSimultaneousHits];
        }

        private void OnDestroy()
        {
            CollectionCaches<Collider, CollisionData>.StoreAndDefault(ref _enteredColliders);
            CollectionCaches<Collider>.StoreAndDefault(ref _hits, _hits.Length);
        }

        public override void OnStartNetwork()
        {
            FindColliders();

            //Initialize the ringbuffer. Server only needs 1 tick worth of history.
            // uint historyTicks = (base.IsServerStarted) ? 1 : TimeManager.TimeToTicks(_historyDuration);
            //_colliderDataHistory.Initialize((int)historyTicks);

            //Events needed by server and client.
            TimeManager.OnPrePhysicsSimulation += TimeManager_OnPostPhysicsSimulation;
        }

        public override void OnStartClient()
        {
            //Events only needed by the client.
            PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
            PredictionManager.OnPostReconcileSyncTransforms += PredictionManagerOnOnPreReconcile;
        }

        private void PredictionManagerOnOnPreReconcile(uint clienttick, uint servertick)
        {
            if (_enteredColliders.Count > 0)
            {
                List<Collider> entriesToRemove = CollectionCaches<Collider>.RetrieveList();

                foreach (KeyValuePair<Collider, CollisionData> kvp in _enteredColliders)
                {
                    if (kvp.Value.ExitTick < clienttick)
                        entriesToRemove.Add(kvp.Key);
                }
                foreach (Collider entry in entriesToRemove)
                    _enteredColliders.Remove(entry);

                CollectionCaches<Collider>.Store(entriesToRemove);
            }
            CheckColliders(clienttick);
        }

        public override void OnStopClient()
        {
            //Events only needed by the client.
            PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
            PredictionManager.OnPostReconcileSyncTransforms -= PredictionManagerOnOnPreReconcile;
        }

        public override void OnStopNetwork()
        {
            TimeManager.OnPrePhysicsSimulation -= TimeManager_OnPostPhysicsSimulation;
        }

        /// <summary>
        /// When using TimeManager for physics timing, this is called immediately after the physics simulation has occured for the tick.
        /// While using Unity for physics timing, this is called during Update, only if a physics frame.
        /// This may be useful if you wish to run physics differently for stacked scenes.
        private void TimeManager_OnPostPhysicsSimulation(float delta)
        {
            CheckColliders(TimeManager.LocalTick);
        }

        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// </summary>
        private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
        {
            CheckColliders(clientTick);
        }

        /// <summary>
        /// Units to extend collision traces by. This is used to prevent missed overlaps when colliders do not intersect enough.
        /// </summary>
        public virtual float GetAdditionalSize() => _additionalSize;

        /// <summary>
        /// Checks for any trigger changes;
        /// </summary>
        private void CheckColliders(uint tick)
        {
            //Should not be possible as tick always starts on 1.
            if (tick == TimeManagerCls.UNSET_TICK)
                return;

            HashSet<Collider> current = CollectionCaches<Collider>.RetrieveHashSet();
            Dictionary<Collider, CollisionData> entered = _enteredColliders;

            /* Previous may not be set here if there were
             * no collisions during the previous tick. */

            // The rotation of the object for box colliders.
            Quaternion rotation = transform.rotation;

            //If layers are specified then do not use GOs layers, use specified.
            if (_layers != (LayerMask)0)
            {
                _interactableLayers = _layers;
            }
            //Use GOs layers.
            else
            {
                int currentLayer = gameObject.layer;
                if (_lastGameObjectLayer != currentLayer)
                {
                    _lastGameObjectLayer = currentLayer;
                    _interactableLayers = Layers.GetInteractableLayersValue(currentLayer);
                }
            }

            //Check each collider for triggers.
            foreach (Collider col in _colliders)
            {
                if (!col.enabled)
                    continue;
                if (IsTrigger != col.isTrigger)
                    continue;

                //Number of hits from the checks.
                int hits;
                if (col is SphereCollider sphereCollider)
                    hits = GetSphereColliderHits(sphereCollider, _interactableLayers);
                else if (col is CapsuleCollider capsuleCollider)
                    hits = GetCapsuleColliderHits(capsuleCollider, _interactableLayers);
                else if (col is BoxCollider boxCollider)
                    hits = GetBoxColliderHits(boxCollider, rotation, _interactableLayers);
                else
                    hits = 0;

                /* Check hits for enter/exit callbacks. */
                for (int i = 0; i < hits; i++)
                {
                    Collider hit = _hits[i];
                    if (hit == null || hit == col)
                        continue;

                    current.Add(hit);

                    //Already entered.
                    if (entered.TryGetValueIL2CPP(hit, out CollisionData collisionData))
                    {
                        /* If entered tick is beyond the tick being checked then
                         * that means the collider entered at a later time, and something
                         * is not aligning. Invoke OnExit and OnEnter again. */
                        if (collisionData.EnterTick >= tick || collisionData.ExitTick != TimeManagerCls.UNSET_TICK)
                        {
                            OnExit?.Invoke(hit);
                            OnEnter?.Invoke(hit);
                            //Also update position in collection.
                            entered[hit] = new CollisionData(tick);
                        }
                    }
                    //Not yet in entered state.
                    else
                    {
                        OnEnter?.Invoke(hit);
                        //Also update position in collection.
                        entered[hit] = new CollisionData(tick);
                    }

                    //Always invoke OnStay when collider hits.
                    OnStay?.Invoke(hit);
                }

                List<Collider> collidersExited = CollectionCaches<Collider>.RetrieveList();
                /* Check to invoke exit on any colliders which are no longer
                 * in the entered state. */
                foreach (Collider c in entered.Keys)
                {
                    //Collider was still entered, no need to check exit.
                    if (current.Contains(c))
                        continue;
                    //Should not be possible to exit the same time as entering unless
                    if (entered[c].EnterTick == tick)
                        continue;

                    collidersExited.Add(c);
                }

                //Invoke for exited and remove from entered.
                foreach (Collider c in collidersExited)
                {
                    /* If here then the entered collider was not hit
                     * this trace. Invoke exit and remove from entered. */
                    OnExit?.Invoke(c);

                    

                    if (base.IsServerStarted)
                        entered.Remove(c);
                    else
                        entered[c] = new(entered[c].EnterTick, tick);
                    //entered.Remove(c);
                }
            }

            CollectionCaches<Collider>.Store(current);
        }

        /// <summary>
        /// Checks for Sphere collisions.
        /// </summary>
        /// <returns>Number of colliders hit.</returns>
        private int GetSphereColliderHits(SphereCollider sphereCollider, int layerMask)
        {
            sphereCollider.GetSphereOverlapParams(out Vector3 center, out float radius);
            radius += GetAdditionalSize();
            return gameObject.scene.GetPhysicsScene().OverlapSphere(center, radius, _hits, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        /// <summary>
        /// Checks for Capsule collisions.
        /// </summary>
        /// <returns>Number of colliders hit.</returns>
        private int GetCapsuleColliderHits(CapsuleCollider capsuleCollider, int layerMask)
        {
            capsuleCollider.GetCapsuleCastParams(out Vector3 start, out Vector3 end, out float radius);
            radius += GetAdditionalSize();
            return gameObject.scene.GetPhysicsScene().OverlapCapsule(start, end, radius, _hits, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        /// <summary>
        /// Checks for Box collisions.
        /// </summary>
        /// <returns>Number of colliders hit.</returns>
        private int GetBoxColliderHits(BoxCollider boxCollider, Quaternion rotation, int layerMask)
        {
            boxCollider.GetBoxOverlapParams(out Vector3 center, out Vector3 halfExtents);
            Vector3 additional = (Vector3.one * GetAdditionalSize());
            halfExtents += additional;
            return gameObject.scene.GetPhysicsScene().OverlapBox(center, halfExtents, _hits, rotation, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        /// <summary>
        /// Finds colliders to use.
        /// <paramref name="rebuild"/>True to rebuild the colliders even if they are already populated.
        /// </summary>
        public void FindColliders(bool rebuild = false)
        {
            if (_collidersFound && !rebuild)
                return;
            _collidersFound = true;

            _colliders = GetComponents<Collider>();
        }

        /// <summary>
        /// Resets this NetworkBehaviour so that it may be added to an object pool.
        /// </summary>
        public override void ResetState(bool asServer)
        {
            base.ResetState(asServer);
            ClearColliderDataHistory();
        }

        /// <summary>
        /// Resets datas in collider data history and clears collection.
        /// </summary>
        private void ClearColliderDataHistory()
        {
            _enteredColliders.Clear();
        }
    }
}