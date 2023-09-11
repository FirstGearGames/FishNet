using System.Collections.Generic;
using UnityEngine;

namespace GameKit.Utilities
{

    public static class Transforms
    {
        /// <summary>
        /// Returns a position for the rectTransform ensuring it's fully on the screen.
        /// </summary>
        /// <param name="desiredPosition">Preferred position for the rectTransform.</param>
        /// <param name="padding">How much padding the transform must be from the screen edges.</param>
        public static Vector3 GetOnScreenPosition(this RectTransform rectTransform, Vector3 desiredPosition, Vector2 padding)
        {
            Vector2 scale = new Vector2(rectTransform.localScale.x, rectTransform.localScale.y);
            //Value of which the tooltip would exceed screen bounds.
            //If there would be overshoot then adjust to be just on the edge of the overshooting side.
            float overshoot;

            float halfWidthRequired = ((rectTransform.sizeDelta.x * scale.x) / 2f) + padding.x;
            overshoot = (Screen.width - (desiredPosition.x + halfWidthRequired));
            //If overshooting on the right.
            if (overshoot < 0f)
                desiredPosition.x += overshoot;
            overshoot = (desiredPosition.x - halfWidthRequired);
            //If overshooting on the left.
            if (overshoot < 0f)
                desiredPosition.x = halfWidthRequired;

            float halfHeightRequired = ((rectTransform.sizeDelta.y * scale.y) / 2f) + padding.y;
            overshoot = (Screen.height - (desiredPosition.y + halfHeightRequired));
            //If overshooting on the right.
            if (overshoot < 0f)
                desiredPosition.y += overshoot;
            overshoot = (desiredPosition.y - halfHeightRequired);
            //If overshooting on the left.
            if (overshoot < 0f)
                desiredPosition.y = halfHeightRequired;

            return desiredPosition;
        }

        /// <summary>
        /// Sets a parent for src while maintaining position, rotation, and scale of src.
        /// </summary>
        /// <param name="parent">Transform to become a child of.</param>
        public static void SetParentAndKeepTransform(this Transform src, Transform parent)
        {
            Vector3 pos = src.position;
            Quaternion rot = src.rotation;
            Vector3 scale = src.localScale;

            src.SetParent(parent);
            src.position = pos;
            src.rotation = rot;
            src.localScale = scale;
        }

        /// <summary>
        /// Destroys all children under the specified transform.
        /// </summary>
        /// <param name="t"></param>
        public static void DestroyChildren(this Transform t, bool destroyImmediately = false)
        {
            foreach (Transform child in t)
            {
                if (destroyImmediately)
                    MonoBehaviour.DestroyImmediate(child.gameObject);
                else
                    MonoBehaviour.Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// Destroys all children of a type under the specified transform.
        /// </summary>
        /// <param name="t"></param>
        public static void DestroyChildren<T>(this Transform t, bool destroyImmediately = false) where T : MonoBehaviour
        {
            T[] children = t.GetComponentsInChildren<T>();
            foreach (T child in children)
            {
                if (destroyImmediately)
                    MonoBehaviour.DestroyImmediate(child.gameObject);
                else
                    MonoBehaviour.Destroy(child.gameObject);
            }
        }


        /// <summary>
        /// Gets components in children and optionally parent.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="results"></param>
        /// <param name="parent"></param>
        /// <param name="includeParent"></param>
        /// <param name="includeInactive"></param>
        public static void GetComponentsInChildren<T>(this Transform parent, List<T> results, bool includeParent = true, bool includeInactive = false) where T : Component
        {
            if (!includeParent)
            {
                List<T> current = GameKit.Utilities.CollectionCaches<T>.RetrieveList();
                for (int i = 0; i < parent.childCount; i++)
                {
                    parent.GetChild(i).GetComponentsInChildren(includeInactive, current);
                    results.AddRange(current);
                }
                GameKit.Utilities.CollectionCaches<T>.Store(current);
            }
            else
            {
                parent.GetComponentsInChildren(includeInactive, results);
            }
        }

        /// <summary>
        /// Returns the position of this transform.
        /// </summary>
        public static Vector3 GetPosition(this Transform t, bool localSpace)
        {
            return (localSpace) ? t.localPosition : t.position;
        }
        /// <summary>
        /// Returns the rotation of this transform.
        /// </summary>
        public static Quaternion GetRotation(this Transform t, bool localSpace)
        {
            return (localSpace) ? t.localRotation : t.rotation;
        }
        /// <summary>
        /// Returns the scale of this transform.
        /// </summary>
        public static Vector3 GetScale(this Transform t)
        {
            return t.localScale;
        }

        /// <summary>
        /// Sets the position of this transform.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="localSpace"></param>
        public static void SetPosition(this Transform t, bool localSpace, Vector3 pos)
        {
            if (localSpace)
                t.localPosition = pos;
            else
                t.position = pos;
        }
        /// <summary>
        /// Sets the position of this transform.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="localSpace"></param>
        public static void SetRotation(this Transform t, bool localSpace, Quaternion rot)
        {
            if (localSpace)
                t.localRotation = rot;
            else
                t.rotation = rot;
        }
        /// <summary>
        /// Sets the position of this transform.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="localSpace"></param>
        public static void SetScale(this Transform t, Vector3 scale)
        {
            t.localScale = scale;
        }

    }


}