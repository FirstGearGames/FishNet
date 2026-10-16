#if !FISHNET_THREADED_COLLIDER_ROLLBACK
using System;
using System.Runtime.CompilerServices;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{
    public partial class ColliderRollback
    {
        internal enum BoundingBoxType
        {
            /// <summary>
            /// Disable this feature.
            /// </summary>
            Disabled,
            /// <summary>
            /// Manually specify the dimensions of a bounding box.
            /// </summary>
            Manual
        }

        // PROSTART
        internal enum FrameRollbackTypes
        {
            LerpFirst,
            LerpMiddle,
            Exact
        }

        /// <summary>
        /// Used to store where colliders are during the snapshot.
        /// </summary>
        internal struct ColliderSnapshot
        {
            public ColliderSnapshot(Transform t)
            {
                WorldPosition = t.position;
                WorldRotation = t.rotation;
            }

            /// <summary>
            /// WorldPosition of transform during snapshot.
            /// </summary>
            public Vector3 WorldPosition;
            /// <summary>
            /// WorldRotation of transform during snapshot.
            /// </summary>
            public Quaternion WorldRotation;

            public void UpdateValues(Transform t)
            {
                WorldPosition = t.position;
                WorldRotation = t.rotation;
            }
        }

        /// <summary>
        /// Used to store where colliders start before rollbacks.
        /// </summary>
        internal class RollingCollider : IResettable
        {
            #region Private.
            /// <summary>
            /// Current snapshots for this collider.
            /// </summary>
            private ColliderSnapshot[] _snapshots;
            /// <summary>
            /// Index to write a snapshot in.
            /// </summary>
            private int _writeIndex;
            /// <summary>
            /// True if snapshots are being recycled rather than written for the first time.
            /// </summary>
            private bool _recycleSnapshots;
            /// <summary>
            /// Maximum number of array entries to fill.
            /// </summary>
            private int _maxSnapshots;
            /// <summary>
            /// Transform collider is for.
            /// </summary>
            private Transform _transform;
            /// <summary>
            /// LocalPosition of transform at start.
            /// </summary>
            private Vector3 _localPosition;
            /// <summary>
            /// LocalRotation of transform at start.
            /// </summary>
            private Quaternion _localRotation;
            #endregion

            public RollingCollider() { }

            /// <summary>
            /// Received when ReturnForward is called on ColliderRollback.
            /// </summary>
            public void Return()
            {
                _transform.localPosition = _localPosition;
                _transform.localRotation = _localRotation;
            }

            /// <summary>
            /// Received when Rollback is called on ColliderRollback.
            /// </summary>
            public void Rollback(FrameRollbackTypes rollbackType, int endFrame, float percent)
            {
                // Exact frame.
                if (rollbackType == FrameRollbackTypes.Exact)
                {
                    int index = GetSnapshotIndex(endFrame);
                    _transform.SetPositionAndRotation(_snapshots[index].WorldPosition, _snapshots[index].WorldRotation);
                }
                // Start frame.
                else if (rollbackType == FrameRollbackTypes.LerpFirst)
                {
                    // Lerp between actual position and the most recent snapshot.
                    int index = GetSnapshotIndex(endFrame);
                    _transform.position = Vector3.Lerp(_transform.position, _snapshots[index].WorldPosition, percent);
                    _transform.rotation = Quaternion.Lerp(_transform.rotation, _snapshots[index].WorldRotation, percent);
                }
                // Middle frame.
                else if (rollbackType == FrameRollbackTypes.LerpMiddle)
                {
                    // Lerp between end frame and the one before it.
                    int firstFrame = GetSnapshotIndex(endFrame - 1);
                    int secondFrame = GetSnapshotIndex(endFrame);

                    _transform.position = Vector3.Lerp(_snapshots[firstFrame].WorldPosition, _snapshots[secondFrame].WorldPosition, percent);
                    _transform.rotation = Quaternion.Lerp(_snapshots[firstFrame].WorldRotation, _snapshots[secondFrame].WorldRotation, percent);
                }
            }

            /// <summary>
            /// Adds a snapshot for this collider.
            /// </summary>
            public void AddSnapshot()
            {
                // Not yet recycling, make a new snapshot.
                if (!_recycleSnapshots)
                    _snapshots[_writeIndex] = new(_transform);
                // Snapshot array traversed already, start recycling.
                else
                    _snapshots[_writeIndex].UpdateValues(_transform);

                _writeIndex++;
                if (_writeIndex >= _maxSnapshots)
                {
                    _writeIndex = 0;
                    _recycleSnapshots = true;
                }
            }

            /// <summary>
            /// Gets a snapshot on the specified index.
            /// </summary>
            /// <returns></returns>
            private int GetSnapshotIndex(int historyCount)
            {
                /* Since write index is increased after a write
                 * we must reduce it by 1 to get to the last
                 * write index, before removing history count. */
                int index = _writeIndex - 1 - historyCount;
                // If negative value start taking from the back.
                if (index < 0)
                {
                    /* Cannot take from back, snapshots aren't filled yet.
                     * Instead take the oldest snapshot, which in this case
                     * would be index 0. */
                    if (!_recycleSnapshots)
                        return 0;
                    // Snapshots filled, take from back.
                    else
                        return _maxSnapshots + index;
                }
                // Not a negative value, return as is.
                else
                {
                    return index;
                }
            }

            public void Initialize(Transform t, int maxSnapshots)
            {
                _transform = t;
                _localPosition = t.localPosition;
                _localRotation = t.localRotation;
                _maxSnapshots = maxSnapshots;

                _snapshots = CollectionCaches<ColliderSnapshot>.RetrieveArray();
                if (_snapshots.Length < maxSnapshots)
                    Array.Resize(ref _snapshots, maxSnapshots);
            }

            public void ResetState()
            {
                CollectionCaches<ColliderSnapshot>.StoreAndDefault(ref _snapshots, _maxSnapshots);
                _writeIndex = 0;
                _maxSnapshots = 0;
                _recycleSnapshots = false;
                _transform = null;
                _localPosition = default;
                _localRotation = default;
            }

            public void InitializeState() { }
        }
        // PROEND
    }
}
#endif