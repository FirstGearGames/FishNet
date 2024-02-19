namespace FishNet.Object.Synchronizing
{
    /// <summary>
    /// Which clients may receive synchronization updates.
    /// </summary>
    public enum ReadPermission : byte
    {
        /// <summary>
        /// All observers will receive updates.
        /// </summary>
        Observers = 1,
        /// <summary>
        /// Only owner will receive updates.
        /// </summary>
        OwnerOnly = 2,
        /// <summary>
        /// Send to all observers except owner.
        /// </summary>
        ExcludeOwner = 3,
    }
}
