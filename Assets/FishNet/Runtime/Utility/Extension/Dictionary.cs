using FishNet.Documenting;
using System.Collections.Generic;

namespace FishNet.Utility.Extension
{
    [APIExclude]
    public static class DictionaryFN
    {

        /// <summary>
        /// Uses a hacky way to TryGetValue on a dictionary when using IL2CPP and on mobile.
        /// This is to support older devices that don't properly handle IL2CPP builds.
        /// </summary>
        public static bool TryGetValueIL2CPP<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
#if ENABLE_IL2CPP && UNITY_IOS || UNITY_ANDROID
            foreach (KeyValuePair<TKey, TValue> item in dict)
            {
                if (item.Key.Equals(key))
                {
                    value = item.Value;
                    return true;
                }    
            }

            value = default(TValue);
            return false;
#else
            return dict.TryGetValue(key, out value);
#endif
        }


    }

}