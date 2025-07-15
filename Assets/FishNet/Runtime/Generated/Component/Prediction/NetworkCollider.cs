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
    public abstract class NetworkCollider : NetworkColliderBase
    {
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
        private Dictionary<Collider, CollisionData> _enteredColliders;

        protected override void Awake()
        {
            base.Awake();

            _enteredColliders = CollectionCaches<Collider, CollisionData>.RetrieveDictionary();
            _hits = CollectionCaches<Collider>.RetrieveArray();
            if (_hits.Length < MaximumSimultaneousHits)
                _hits = new Collider[MaximumSimultaneousHits];
        }

        private void OnDestroy()
        {
            CollectionCaches<Collider, CollisionData>.StoreAndDefault(ref _enteredColliders);
            CollectionCaches<Collider>.StoreAndDefault(ref _hits, _hits.Length);
        }

        /// <summary>
        /// Called by the PredictionManager immediately before a reconcile begins.
        /// </summary>
        protected override void PredictionManager_OnPreReconcile(uint clientTick, uint serverTick)
        {
            /* Remove entries older than the reconcile clientTick, if
             * the entry is exited - as in the collider is no longer occupied. */
            if (_enteredColliders.Count > 0)
            {
                List<Collider> entriesToRemove = CollectionCaches<Collider>.RetrieveList();

                foreach (KeyValuePair<Collider, CollisionData> kvp in _enteredColliders)
                {
                    uint exitTick = kvp.Value.ExitTick;
                    if (exitTick != TimeManagerCls.UNSET_TICK && exitTick < clientTick)
                        entriesToRemove.Add(kvp.Key);
                }

                foreach (Collider entry in entriesToRemove)
                    _enteredColliders.Remove(entry);

                CollectionCaches<Collider>.Store(entriesToRemove);
            }

            /* Call base only after removing old entries. This ensures old entries are removed
             * before CheckColliders is called. */
            base.PredictionManager_OnPreReconcile(clientTick, serverTick);
        }

        /// <summary>
        /// Checks for any collider changes;
        /// </summary>
        protected override void CheckColliders(uint clientTick)
        {
            // Initial checks failed.
            if (!TryPrepareColliderCheck(clientTick))
                return;

            HashSet<Collider> current = CollectionCaches<Collider>.RetrieveHashSet();
            Dictionary<Collider, CollisionData> entered = _enteredColliders;

            /* Previous may not be set here if there were
             * no collisions during the previous tick. */

            // The rotation of the object for box colliders.
            Quaternion rotation = transform.rotation;

            // Check each collider for triggers.
            foreach (Collider col in _colliders)
            {
                if (!col.enabled)
                    continue;
                if (IsTrigger != col.isTrigger)
                    continue;

                // Number of hits from the checks.
                int hits;
                if (col is SphereCollider sphereCollider)
                    hits = GetSphereColliderHits(sphereCollider, InteractableLayers);
                else if (col is CapsuleCollider capsuleCollider)
                    hits = GetCapsuleColliderHits(capsuleCollider, InteractableLayers);
                else if (col is BoxCollider boxCollider)
                    hits = GetBoxColliderHits(boxCollider, rotation, InteractableLayers);
                else
                    hits = 0;

                /* Check hits for enter/exit callbacks. */
                for (int i = 0; i < hits; i++)
                {
                    Collider hit = _hits[i];
                    if (hit == null || hit == col)
                        continue;

                    current.Add(hit);

                    // Already entered.
                    if (entered.TryGetValueIL2CPP(hit, out CollisionData collisionData))
                    {
                        /* If entered tick is beyond the tick being checked then
                         * that means the collider entered at a later time, and something
                         * is not aligning. Invoke OnExit and OnEnter again. */
                        if (collisionData.EnterTick >= clientTick || collisionData.ExitTick != TimeManagerCls.UNSET_TICK)
                        {
                            OnExit?.Invoke(hit);
                            OnEnter?.Invoke(hit);
                            // Also update position in collection.
                            entered[hit] = new(clientTick);
                        }
                    }
                    // Not yet in entered state.
                    else
                    {
                        OnEnter?.Invoke(hit);
                        // Also update position in collection.
                        entered[hit] = new(clientTick);
                    }

                    // Always invoke OnStay when collider hits.
                    OnStay?.Invoke(hit);
                }

                List<Collider> collidersExited = CollectionCaches<Collider>.RetrieveList();
                /* Check to invoke exit on any colliders which are no longer
                 * in the entered state. */
                foreach (Collider c in entered.Keys)
                {
                    // Collider was still entered, no need to check exit.
                    if (current.Contains(c))
                        continue;
                    /* Entered tick will be the same as tick if first
                     * entering for this tick. It's not possible for Unity physics
                     * to invoke Enter/Exit on the same tick, as it doesn't make sense
                     * to anyway. When the same tick, continue. */
                    if (entered[c].EnterTick == clientTick)
                        continue;

                    collidersExited.Add(c);
                }

                // Invoke for exited and remove from entered.
                foreach (Collider c in collidersExited)
                {
                    /* If here then the entered collider was not hit
                     * this trace. Invoke exit and remove from entered. */
                    OnExit?.Invoke(c);
                    if (IsServerStarted)
                    {
                        entered.Remove(c);
                    }
                    else
                    {
                        /* Only re-add if the entered tick is beyond
                         * the current tick; this would indicate a new enter.
                         * Otherwise, we are at an exit only. */
                        uint enteredTick = entered[c].EnterTick;
                        if (enteredTick > clientTick)
                            entered[c] = new(entered[c].EnterTick, clientTick);
                        else
                            entered.Remove(c);
                    }
                    // entered.Remove(c);
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
            radius += AdditionalSize;
            return gameObject.scene.GetPhysicsScene().OverlapSphere(center, radius, _hits, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        /// <summary>
        /// Checks for Capsule collisions.
        /// </summary>
        /// <returns>Number of colliders hit.</returns>
        private int GetCapsuleColliderHits(CapsuleCollider capsuleCollider, int layerMask)
        {
            capsuleCollider.GetCapsuleCastParams(out Vector3 start, out Vector3 end, out float radius);
            radius += AdditionalSize;
            return gameObject.scene.GetPhysicsScene().OverlapCapsule(start, end, radius, _hits, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        /// <summary>
        /// Checks for Box collisions.
        /// </summary>
        /// <returns>Number of colliders hit.</returns>
        private int GetBoxColliderHits(BoxCollider boxCollider, Quaternion rotation, int layerMask)
        {
            boxCollider.GetBoxOverlapParams(out Vector3 center, out Vector3 halfExtents);
            Vector3 additional = Vector3.one * AdditionalSize;
            halfExtents += additional;
            return gameObject.scene.GetPhysicsScene().OverlapBox(center, halfExtents, _hits, rotation, layerMask, QueryTriggerInteraction.UseGlobal);
        }

        /// <summary>
        /// Finds colliders on this object to check.
        /// </summary>
        /// <param name = "force">True to set colliders again even if already found. This action will clear stored collider states.</param>
        /// <returns>True if colliders should be found again.</returns>
        public override bool TryFindColliders(bool force = false)
        {
            if (!base.TryFindColliders(force))
                return false;

            ClearColliderDataHistory(invokeOnExit: true);
            _colliders = GetComponents<Collider>();

            return true;
        }

        /// <summary>
        /// Resets this NetworkBehaviour so that it may be added to an object pool.
        /// </summary>
        public override void ResetState(bool asServer)
        {
            ClearColliderDataHistory(invokeOnExit: true);
            base.ResetState(asServer);
        }

        /// <summary>
        /// Clears stored collider states.
        /// </summary>
        /// <param name = "invokeOnExit">True to invoke OnExit if a collider is stored in the OnEntered state. When called during a reconcile this used the current ClientReplayTick, otherwise uses LocalTick.</param>
        protected override void ClearColliderDataHistory(bool invokeOnExit)
        {
            if (_enteredColliders == null)
                return;

            /* Data needs to exist to iterate, and managers are needed
             * to get the proper tick to invoke. */
            if (invokeOnExit)
            {
                foreach (KeyValuePair<Collider, CollisionData> kvp in _enteredColliders)
                {
                    /* This indicates an exit has not yet invoked.
                     * It's possible for an item to invoked an exit and still
                     * have its state cached for properly executing events during
                     * a reconcile. */
                    if (kvp.Value.ExitTick == TimeManagerCls.UNSET_TICK)
                        OnExit?.Invoke(kvp.Key);
                }
            }

            _enteredColliders.Clear();
        }
    }
}