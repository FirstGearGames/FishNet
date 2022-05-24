using FishNet.Documenting;
using FishNet.Object.Helping;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using FishNet.Editing;
using UnityEditor;
#endif
using FishNet.Object;

namespace FishNet.Managing.Object
{

    [APIExclude]
    //[CreateAssetMenu(fileName = "New DefaultPrefabObjects", menuName = "FishNet/Spawnable Prefabs/Default Prefab Objects")]
    public class DefaultPrefabObjects : SinglePrefabObjects
    {
        /// <summary>
        /// Sorts prefabs by name and path hashcode.
        /// </summary>
        internal void Sort()
        {
#if UNITY_EDITOR
            if (base.GetObjectCount() == 0)
                return;

            Dictionary<ulong, NetworkObject> hashcodesAndNobs = new Dictionary<ulong, NetworkObject>();
            List<ulong> hashcodes = new List<ulong>();

            foreach (NetworkObject n in base.Prefabs)
            {
                string pathAndName = $"{AssetDatabase.GetAssetPath(n.gameObject)}{n.gameObject.name}";
                ulong hashcode = Hashing.GetStableHash64(pathAndName);
                hashcodesAndNobs[hashcode] = n;
                hashcodes.Add(hashcode);
            }

            //Once all hashes have been made re-add them to prefabs sorted.
            hashcodes.Sort();
            //Build to a new list using sorted hashcodes.
            List<NetworkObject> sortedNobs = new List<NetworkObject>();
            foreach (ulong hc in hashcodes)
                sortedNobs.Add(hashcodesAndNobs[hc]);

            base.Clear();
            base.AddObjects(sortedNobs, false);
#endif
        }


    }

}