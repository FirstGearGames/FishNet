using System.Collections.Generic;

namespace GameKit.Dependencies.Utilities
{
    public static class DictionaryFN
    {

        /// <summary>
        /// Uses a hacky way to TryGetValue on a dictionary when using IL2CPP and on mobile.
        /// This is to support older devices that don't properly handle IL2CPP builds.
        /// </summary>
        public static bool TryGetValueIL2CPP<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
#if ENABLE_IL2CPP && UNITY_IOS || UNITY_ANDROID
            if (dict.ContainsKey(key))
            {
                value = dict[key];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
#else
            return dict.TryGetValue(key, out value);
#endif
        }

        /// <summary>
        /// Returns values as an allocated list.
        /// </summary>
        /// <returns></returns>
        public static List<TValue> ValuesToList<TKey, TValue>(this IDictionary<TKey, TValue> dict)
        {
            List<TValue> result = new(dict.Count);
            dict.ValuesToList(ref result);
            return result;
        }

        /// <summary>
        /// Clears a collection and populates it with this dictionaries values.
        /// </summary>
        public static void ValuesToList<TKey, TValue>(this IDictionary<TKey, TValue> dict, ref List<TValue> result)
        {
            result.Clear();
            foreach (TValue item in dict.Values)
                result.Add(item);
        }
        
        /// <summary>
        /// Clears a collection and populates it with this dictionaries keys.
        /// </summary>
        public static void KeysToList<TKey, TValue>(this IDictionary<TKey, TValue> dict, ref List<TKey> result)
        {
            result.Clear();
            foreach (TKey item in dict.Keys)
                result.Add(item);
        }

    }

}