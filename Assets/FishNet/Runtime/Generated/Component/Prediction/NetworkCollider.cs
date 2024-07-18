using FishNet.Object;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using TimeManagerCls = FishNet.Managing.Timing.TimeManager;

namespace FishNet.Component.Prediction
{

    public abstract class NetworkCollider : NetworkBehaviour
    {
        #region Types.
        private struct ColliderData : IResettable
        {
            /// <summary>
            /// Tick which the collisions happened.
            /// </summary>
            public uint Tick;
            /// <summary>
            /// Hits for Tick.
            /// </summary>
            public HashSet<Collider> Hits;

            public ColliderData(uint tick, HashSet<Collider> hits)
            {
                Tick = tick;
                Hits = hits;
            }

            public void InitializeState() { }
            public void ResetState()
            {
                Tick = TimeManagerCls.UNSET_TICK;
                CollectionCaches<Collider>.StoreAndDefault(ref Hits);
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
        /// How long of collision history to keep. Lower values will result in marginally better memory usage at the cost of collision histories desynchronizing on clients with excessive latency.
        /// </summary>
        [Tooltip("How long of collision history to keep. Lower values will result in marginally better memory usage at the cost of collision histories desynchronizing on clients with excessive latency.")]
        [Range(0.1f, 2f)]
        [SerializeField]
        private float _historyDuration = 0.5f;
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
        /// <summary>
        /// The history of collider data.
        /// </summary>
        private ResettableRingBuffer<ColliderData> _colliderDataHistory;
        /// <summary>
        /// True if colliders have been searched for at least once.
        /// We cannot check the null state on _colliders because Unity has a habit of initializing collections on it's own.
        /// </summary>
        private bool _collidersFound;
        /// <summary>
        /// True to cache collision histories for comparing start and exits.
        /// </summary>
        private bool _useCache => (OnEnter != null || OnExit != null);
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
            _colliderDataHistory = new();
            _hits = CollectionCaches<Collider>.RetrieveArray();
            if (_hits.Length < _maximumSimultaneousHits)
                _hits = new Collider[_maximumSimultaneousHits];
        }

        private void OnDestroy()
        {
            //ResettableCollectionCaches<ColliderData>.StoreAndDefault(ref _colliderDataHistory);
            CollectionCaches<Collider>.StoreAndDefault(ref _hits, _hits.Length);
        }

        public override void OnStartNetwork()
        {
            FindColliders();

            //Initialize the ringbuffer. Server only needs 1 tick worth of history.
            uint historyTicks = (base.IsServerStarted) ? 1 : TimeManager.TimeToTicks(_historyDuration);
            _colliderDataHistory.Initialize((int)historyTicks);

            //Events needed by server and client.
            TimeManager.OnPostPhysicsSimulation += TimeManager_OnPostPhysicsSimulation;
        }

        public override void OnStartClient()
        {
            //Events only needed by the client.
            PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
        }

        public override void OnStopClient()
        {
            //Events only needed by the client.
            PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;

        }

        public override void OnStopNetwork()
        {
            TimeManager.OnPostPhysicsSimulation -= TimeManager_OnPostPhysicsSimulation;
        }


        /// <summary>
        /// When using TimeManager for physics timing, this is called immediately after the physics simulation has occured for the tick.
        /// While using Unity for physics timing, this is called during Update, only if a physics frame.
        /// This may be useful if you wish to run physics differently for stacked scenes.
        private void TimeManager_OnPostPhysicsSimulation(float delta)
        {
            CheckColliders(TimeManager.LocalTick, false);
        }

        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// </summary>
        private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
        {
            CheckColliders(clientTick, true);
        }

        /// <summary>
        /// Cleans history up to, while excluding tick.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CleanHistory(uint tick)
        {
            if (_useCache)
            {
                int removeCount = 0;
                int historyCount = _colliderDataHistory.Count;
                for (int i = 0; i < historyCount; i++)
                {
                    if (_colliderDataHistory[i].Tick >= tick)
                        break;
                    removeCount++;
                }

                for (int i = 0; i < removeCount; i++)
                    _colliderDataHistory[i].ResetState();
                _colliderDataHistory.RemoveRange(true, removeCount);
            }
            //Cache is not used.
            else
            {
                ClearColliderDataHistory();
            }
        }

        /// <summary>
        /// Units to extend collision traces by. This is used to prevent missed overlaps when colliders do not intersect enough.
        /// </summary>
        public virtual float GetAdditionalSize() => _additionalSize;

        /// <summary>
        /// Checks for any trigger changes;
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckColliders(uint tick, bool replay)
        {
            //Should not be possible as tick always starts on 1.
            if (tick == TimeManagerCls.UNSET_TICK)
                return;

            const int INVALID_HISTORY_VALUE = -1;

            HashSet<Collider> current = CollectionCaches<Collider>.RetrieveHashSet();
            HashSet<Collider> previous = null;

            int previousHitsIndex = INVALID_HISTORY_VALUE;
            /* Server only keeps 1 history so
             * if server is started then
             * simply clean one. When the server is
             * started replay will never be true, so this
             * will only call once per tick. */
            if (base.IsServerStarted && tick > 0)
                CleanHistory(tick - 1);

            if (_useCache)
            {
                if (replay)
                {
                    previousHitsIndex = GetHistoryIndex(tick - 1, false);
                    if (previousHitsIndex != -1)
                        previous = _colliderDataHistory[previousHitsIndex].Hits;
                }
                //Not replaying.
                else
                {
                    if (_colliderDataHistory.Count > 0)
                    {
                        ColliderData cd = _colliderDataHistory[_colliderDataHistory.Count - 1];
                        /* If the hit tick one before current then it can be used, otherwise
                        * use a new collection for previous. */
                        if (cd.Tick == (tick - 1))
                            previous = cd.Hits;
                    }
                }
            }
            //Not using history, clear it all.
            else
            {
                ClearColliderDataHistory();
            }

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

            // Check each collider for triggers.
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

                // Check the hits for triggers.
                for (int i = 0; i < hits; i++)
                {
                    Collider hit = _hits[i];
                    if (hit == null || hit == col)
                        continue;

                    /* If not in previous then add and
                     * invoke enter. */
                    if (previous == null || !previous.Contains(hit))
                        OnEnter?.Invoke(hit);

                    //Also add to current hits.
                    current.Add(hit);
                    OnStay?.Invoke(hit);
                }
            }

            if (previous != null)
            {
                //Check for stays and exits.
                foreach (Collider col in previous)
                {
                    //If it was in previous but not current, it has exited.
                    if (!current.Contains(col))
                        OnExit?.Invoke(col);
                }
            }

            //If not using the cache then clean up collections.
            if (_useCache)
            {
                //If not replaying add onto the end. */
                if (!replay)
                {
                    AddToEnd();
                }
                /* If a replay then set current colliders
                 * to one entry past historyIndex. If the next entry
                 * beyond historyIndex is for the right tick it can be
                 * updated, otherwise a result has to be inserted. */
                else
                {
                    /* Previous hits was not found in history so we
                     * cannot assume current results go right after the previousIndex.
                     * Find whichever index is the closest to tick and return it. 
                     * 
                     * If an exact match is not found for tick then the entry just after
                     * tick will be returned. This will let us insert current hits right
                     * before that entry. */
                    if (previousHitsIndex == -1)
                    {
                        int currentIndex = GetHistoryIndex(tick, true);
                        AddDataToIndex(currentIndex);
                    }
                    //If previous hits are known then the index to update is right after previous index.
                    else
                    {
                        int insertIndex = (previousHitsIndex + 1);
                        /* InsertIndex is out of bounds which means
                         * to add onto the end. */
                        if (insertIndex >= _colliderDataHistory.Count)
                            AddToEnd();
                        //Not the last entry to insert in the middle.
                        else
                            AddDataToIndex(insertIndex);
                    }

                    /* Adds data to an index. If the tick
                     * matches on index with the current tick then
                     * replace the entry. Otherwise insert to the
                     * correct location. */
                    void AddDataToIndex(int index)
                    {
                        ColliderData colliderData = new ColliderData(tick, current);
                        /* If insertIndex is the same tick then replace, otherwise
                         * put in front of. */
                        //Replace.
                        if (_colliderDataHistory[index].Tick == tick)
                        {
                            _colliderDataHistory[index].ResetState();
                            _colliderDataHistory[index] = colliderData;
                        }
                        //Insert before.
                        else
                        {
                            _colliderDataHistory.Insert(index, colliderData);
                        }
                    }
                }

                void AddToEnd()
                {
                    ColliderData colliderData = new ColliderData(tick, current);
                    _colliderDataHistory.Add(colliderData);
                }

            }
            /* If not using caching then store results from this run. */
            else
            {
                CollectionCaches<Collider>.Store(current);
            }

            //Returns history index for a tick.
            /* GetClosest will return the closest match which is
             * past lTick if lTick could not be found. */
            int GetHistoryIndex(uint lTick, bool getClosest)
            {
                for (int i = 0; i < _colliderDataHistory.Count; i++)
                {
                    uint localTick = _colliderDataHistory[i].Tick;
                    if (localTick == lTick)
                        return i;
                    /* Tick is too high, any further results
                     * will also be too high. */
                    if (localTick > tick)
                    {
                        if (getClosest)
                            return i;
                        else
                            return INVALID_HISTORY_VALUE;
                    }
                }

                //Fall through.
                return INVALID_HISTORY_VALUE;
            }
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            foreach (ColliderData cd in _colliderDataHistory)
                cd.ResetState();
            _colliderDataHistory.Clear();
        }
    }


}