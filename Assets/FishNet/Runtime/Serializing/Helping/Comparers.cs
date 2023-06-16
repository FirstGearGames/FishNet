using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Serializing.Helping
{

    public class GeneratedComparer<T>
    {
        /// <summary>
        /// Compare if T is default.
        /// </summary>
        public static Func<T, bool> IsDefault { get; set; }
        /// <summary>
        /// Compare if T is the same as T2.
        /// </summary>
        public static Func<T, T, bool> Compare { get; set; }
    }
     

    public class Comparers
    {
        /// <summary>
        /// Returns if A equals B using EqualityCompare.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool EqualityCompare<T>(T a, T b)
        {
            return EqualityComparer<T>.Default.Equals(a, b);
        }

        public static bool IsDefault<T>(T t)
        {
            return t.Equals(default(T));
        }

        public static bool IsEqualityCompareDefault<T>(T a)
        {
            return EqualityComparer<T>.Default.Equals(a, default(T));
        }
    }


    internal class SceneComparer : IEqualityComparer<Scene>
    {
        public bool Equals(Scene a, Scene b)
        {
            if (!a.IsValid() || !b.IsValid())
                return false;

            if (a.handle != 0 || b.handle != 0)
                return (a.handle == b.handle);

            return (a.name == b.name);
        }

        public int GetHashCode(Scene obj)
        {
            return obj.GetHashCode();
        }
    }

}
