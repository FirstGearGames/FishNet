using FishNet.Documenting;


namespace FishNet.Object.Synchronizing.SecretMenu
{
    /// <summary>
    /// Internal SyncVar extensions.
    /// </summary>
    [APIExclude]
    public static class SyncVarExtensions
    {
        /// <summary>
        /// Dirties SyncVars.
        /// </summary>
        /// <param name="obj"></param>
        [APIExclude]
        public static void Dirty(this object obj) { }
    }


}