﻿using System.Collections.Generic;
using System.Text;

namespace GameKit.Dependencies.Utilities
{

    public static class Arrays
    {
        /// <summary>
        /// Randomizer used for shuffling.
        /// </summary>
        private static System.Random _random = new System.Random();
        /// <summary>
        /// StringBuilder to save performance.
        /// </summary>
        private static StringBuilder _stringBuilder = new StringBuilder();

        /// <summary>
        /// Adds an entry to a list if it does not exist already.
        /// </summary>
        /// <returns>True if being added.</returns>
        public static bool AddUnique<T>(this List<T> list, T value)
        {
            bool contains = list.Contains(value);
            if (!contains)
                list.Add(value);

            return !contains;
        }

        /// <summary>
        /// Cast each item in the collection ToString and returns all values.
        /// </summary>
        /// <returns></returns>
        public static string ToString<T>(this IEnumerable<T> collection, string delimeter = ", ")
        {
            if (collection == null)
                return string.Empty;

            _stringBuilder.Clear();
            foreach (T item in collection)
                _stringBuilder.Append(item.ToString() + delimeter);

            //Remove ending delimeter.
            if (_stringBuilder.Length > delimeter.Length)
                _stringBuilder.Length -= delimeter.Length;

            return _stringBuilder.ToString();
        }

        /// <summary>
        /// Removes an object from a list through re-ordering. This breaks the order of the list for a faster remove.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool FastReferenceRemove<T>(this List<T> list, object value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (object.ReferenceEquals(list[i], value))
                {
                    FastIndexRemove(list, i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes an index from a list through re-ordering. This breaks the order of the list for a faster remove.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="index"></param>
        public static void FastIndexRemove<T>(this List<T> list, int index)
        {
            list[index] = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
        }


        /// <summary>
        /// Shuffles an array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        public static void Shuffle<T>(this T[] array)
        {
            int n = array.Length;
            for (int i = 0; i < (n - 1); i++)
            {
                int r = i + _random.Next(n - i);
                T t = array[r];
                array[r] = array[i];
                array[i] = t;
            }
        }

        /// <summary>
        /// Shuffles a list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lst"></param>
        public static void Shuffle<T>(this List<T> lst)
        {
            int n = lst.Count;
            for (int i = 0; i < (n - 1); i++)
            {
                int r = i + _random.Next(n - i);
                T t = lst[r];
                lst[r] = lst[i];
                lst[i] = t;
            }
        }

    }


}