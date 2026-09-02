using FishNet.Utility.Extension;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Serializing.Helping
{
    internal sealed class SceneHandleEqualityComparer : EqualityComparer<Scene>
    {
        public override bool Equals(Scene a, Scene b)
        {
            return a.GetRawHandle() == b.GetRawHandle();
        }

        public override int GetHashCode(Scene obj)
        {
            return obj.GetRawHandle().GetHashCode();
        }
    }
}