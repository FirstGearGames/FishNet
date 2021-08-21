using UnityEngine;

namespace FishNet.Object
{
    [System.Serializable]
    public struct SceneTransformProperties
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;
        public SceneTransformProperties(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Position = position;
            Rotation = rotation;
            LocalScale = localScale;
        }
    }
}

