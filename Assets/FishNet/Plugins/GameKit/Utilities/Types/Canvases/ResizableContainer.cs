using TriInspector;
using UnityEngine;


namespace GameKit.Utilities.Types.CanvasContainers
{

    [DeclareFoldoutGroup("Sizing")]
    public class ResizableContainer : FloatingContainer
    {
        #region Serialized.
        /// <summary>
        /// Minimum and maximum range for widwth and height of the RectTransform.
        /// </summary>
        [Tooltip("Minimum and maximum range for width and height of the RectTransform.")]
        [Group("Sizing")]
        public FloatRange2D SizeLimits = new FloatRange2D()
        {
            X = new FloatRange(0f, 999999f),
            Y = new FloatRange(0f, 999999f)
        };        
        #endregion

        #region Private.
        /// <summary>
        /// Size to use.
        /// </summary>
        private Vector2 _desiredSize;
        /// <summary>
        /// True to ignore size limitations.
        /// </summary>
        private bool _ignoreSizeLimits;
        #endregion

        /// <summary>
        /// Sets a size, and resizes if needed.
        /// Other transform values must be set separately using inherited methods.
        /// </summary>
        /// <param name="size">New size to use.</param>
        /// <param name="ignoreSizeLimits">True to ignore serialized Size limits.</param>
        /// <param name="resizeOnce">True to resize once and immediately, false to resize over a couple frames to work-around Unity limitations. The canvas will not show until a resize completes.</param>
        public void SetSizeAndShow(Vector2 size, bool ignoreSizeLimits = false, bool resizeOnce = false)
        {
            _ignoreSizeLimits = ignoreSizeLimits;
            _desiredSize = size;

            if (resizeOnce)
                ResizeAndShow(true);
            else
                RectTransformResizer.Resize(new RectTransformResizer.ResizeDelegate(ResizeAndShow));
        }

        /// <summary>
        /// Resizes this canvas.
        /// </summary>
        protected virtual void ResizeAndShow(bool complete)
        {
           float widthRequired = _desiredSize.x;
            float heightRequired = _desiredSize.y;
            //Clamp width and height.
            widthRequired = Mathf.Clamp(widthRequired, SizeLimits.X.Minimum, SizeLimits.X.Maximum);
            heightRequired = Mathf.Clamp(heightRequired, SizeLimits.Y.Minimum, SizeLimits.Y.Maximum);
            base.RectTransform.sizeDelta = new Vector2(widthRequired, heightRequired);

            if (complete)
            {
                base.Move();
                base.Show();
            }
        }

    }


}