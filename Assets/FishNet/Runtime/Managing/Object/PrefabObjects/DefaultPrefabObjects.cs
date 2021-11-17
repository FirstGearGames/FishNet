using FishNet.Documenting;
#if UNITY_EDITOR
using FishNet.Editing;
#endif
using FishNet.Object;

namespace FishNet.Managing.Object
{

    [APIExclude]
    //[CreateAssetMenu(fileName = "New DefaultPrefabObjects", menuName = "FishNet/Spawnable Prefabs/Default Prefab Objects")]
    public class DefaultPrefabObjects : SinglePrefabObjects
    {

#if UNITY_EDITOR
        /* Try to recover invalid/null prefab errors in editor.
         * This can occur when simlinking or when the asset processor
         * doesn't function properly. */
        public override NetworkObject GetObject(bool asServer, int id)
        {
            //Only error check cases where the collection may be wrong.
            bool error = (id >= base.Prefabs.Count ||
                base.Prefabs[id] == null);

            if (error)
            {
                base.Clear();
                DefaultPrefabsFinder.PopulateDefaultPrefabs();
            }

            return base.GetObject(asServer, id);
        }
#endif

    }

}