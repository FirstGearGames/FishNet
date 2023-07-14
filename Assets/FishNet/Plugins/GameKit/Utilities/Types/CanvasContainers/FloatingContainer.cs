using System.Runtime.CompilerServices;
using UnityEngine;


namespace GameKit.Utilities.Types.CanvasContainers
{


    public class FloatingContainer : CanvasGroupFader
    {

        #region Serialized.
        /// <summary>
        /// RectTransform to move.
        /// </summary>
        [Tooltip("RectTransform to move.")]
        [SerializeField]
        private RectTransform _rectTransform;
        #endregion

        #region Private.
        /// <summary>
        /// Desired position.
        /// </summary>
        private Vector3 _positionGoal;
        /// <summary>
        /// Desired rotation.
        /// </summary>
        private Quaternion _rotationGoal;
        /// <summary>
        /// Desired scale.
        /// </summary>
        private Vector3 _scaleGoal;
        #endregion

        /// <summary>
        /// Attachs a gameObject as a child of this object and sets transform valus to default.
        /// </summary>
        /// <param name="go">GameObject to attach.</param>
        public void AttachGameObject(GameObject go)
        {
            if (go == null)
                return;

            Transform goT = go.transform;
            goT.SetParent(transform);
            goT.localPosition = Vector3.zero;
            goT.localRotation = Quaternion.identity;
            goT.localScale = Vector3.one;
        }

        /// <summary>
        /// Shows the container.
        /// </summary>
        /// <param name="position">Position to use.</param>
        /// <param name="rotation">Rotation to use.</param>
        /// <param name="scale">Scale to use.</param>
        /// <param name="pivot">Pivot for rectTransform.</param>
        public virtual void Show(Vector3 position, Quaternion rotation, Vector3 scale, Vector2 pivot)
        {
            UpdatePivot(pivot, false);
            UpdatePositionRotationAndScale(position, rotation, scale);
            base.Show();
        }

        /// <summary>
        /// Shows the container.
        /// </summary>
        /// <param name="position">Position to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Show(Vector3 position)
        {
            Show(position, Quaternion.identity, Vector3.one, _rectTransform.pivot);
        }

        /// <summary>
        /// Shows the container.
        /// </summary>
        /// <param name="position">Position to use.</param>
        /// <param name="rotation">Rotation to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Show(Vector3 position, Quaternion rotation)
        {
            Show(position, rotation, Vector3.one, _rectTransform.pivot);
        }

        /// <summary>
        /// Shows the container.
        /// </summary>
        /// <param name="startingPoint">Transform to use for position, rotation, and scale.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Show(Transform startingPoint)
        {
            if (startingPoint == null)
            {
                Debug.LogError($"A null Transform cannot be used as the starting point.");
                return;
            }

            Show(startingPoint.position, startingPoint.rotation, startingPoint.localScale, _rectTransform.pivot);
        }

        /// <summary>
        /// Updates the rectTransform pivot.
        /// </summary>
        /// <param name="pivot">New pivot.</param>
        public virtual void UpdatePivot(Vector2 pivot, bool move)
        {
            _rectTransform.pivot = pivot;
            if (move)
                Move();
        }

        /// <summary>
        /// Updates to a new position.
        /// </summary>
        /// <param name="position">Next position.</param>
        public virtual void UpdatePosition(Vector3 position, bool move = true)
        {
            _positionGoal = position;
            if (move)
                Move();
        }

        /// <summary>
        /// Updates to a new rotation.
        /// </summary>
        /// <param name="rotation">Next rotation.</param>
        public virtual void UpdateRotation(Quaternion rotation, bool move = true)
        {
            _rotationGoal = rotation;
            if (move)
                Move();
        }

        /// <summary>
        /// Updates to a new scale.
        /// </summary>
        /// <param name="scale">Next scale.</param>
        public virtual void UpdateScale(Vector3 scale, bool move = true)
        {
            _scaleGoal = scale;
            if (move)
                Move();
        }

        /// <summary>
        /// Updates to a new position and rotation.
        /// </summary>
        /// <param name="position">Next position.</param>
        /// <param name="rotation">Next rotation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void UpdatePositionAndRotation(Vector3 position, Quaternion rotation, bool move)
        {
            UpdatePosition(position, false);
            UpdateRotation(rotation, false);
            if (move)
                Move();
        }
        /// <summary>
        /// Updates to a new position, rotation, and scale.
        /// </summary>
        /// <param name="position">Next position.</param>
        /// <param name="rotation">Next rotation.</param>
        /// <param name="scale">Next scale.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void UpdatePositionRotationAndScale(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            UpdatePositionAndRotation(position, rotation, false);
            UpdateScale(scale, false);
            Move();
        }

        /// <summary>
        /// Moves to configured goals.
        /// </summary>
        private void Move()
        {
            _rectTransform.SetPositionAndRotation(_positionGoal, _rotationGoal);
            _rectTransform.localScale = _scaleGoal;
        }
    }


}