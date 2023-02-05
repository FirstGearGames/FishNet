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
        Observers,
        /// <summary>
        /// Only owner will receive updates.
        /// </summary>
        OwnerOnly,
        /// <summary>
        /// Send to all observers except owner.
        /// </summary>
        ExcludeOwner
    }
}
