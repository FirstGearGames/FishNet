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
        /// True if this can be automatically populated.
        /// </summary>
        internal static bool CanAutomate = true;

        /// <summary>
        /// True if this refreshed while playing.
        /// </summary>
        [System.NonSerialized]
        private bool _refreshedWhilePlaying = false;

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

        /// <summary>
        /// Populates this DefaultPrefabObjects.
        /// </summary>
        internal void AutoPopulateDefaultPrefabs(bool log = true, bool clear = true)
        {
            if (!CanAutomate)
            {
                Debug.Log("Auto populating DefaultPrefabs is blocked.");
                return;
            }

            PopulateDefaultPrefabs(log, clear);
        }

        /// <summary>
        /// Populates this DefaultPrefabObjects.
        /// </summary>
        internal void PopulateDefaultPrefabs(bool log = true, bool clear = true)
        {
#if UNITY_EDITOR
            DefaultPrefabsFinder.PopulateDefaultPrefabs(log, clear);
#endif
        }
        /* Try to recover invalid/null prefab errors in editor.
         * This can occur when simlinking or when the asset processor
         * doesn't function properly. */
        public override NetworkObject GetObject(bool asServer, int id)
        {
            //Only error check cases where the collection may be wrong.
            bool error = (id >= base.Prefabs.Count ||
                base.Prefabs[id] == null);

            if (error && !_refreshedWhilePlaying)
            {
                //This prevents the list from trying to populate several times before exiting play mode.
                _refreshedWhilePlaying = true;
                AutoPopulateDefaultPrefabs(false);
            }

            return base.GetObject(asServer, id);
        }



    }

}