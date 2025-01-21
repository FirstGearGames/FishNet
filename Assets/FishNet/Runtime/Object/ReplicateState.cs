using FishNet.Utility;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object
{

    public enum ReplicateState : byte
    {
        /// <summary>
        /// The default value of this state.
        /// This value should never occur when a replicate runs.
        /// </summary>
        Invalid = 0,
        /// <summary>
        /// Value is seen on server and clients.
        /// Client or server has data on the object for the tick.
        /// Clients will only see this value on spectated objects when PredictionManager is using Appended state order.
        /// </summary>
        CurrentCreated = 1,
        /// <summary>
        /// Value is only seen on server when they do not own the object.
        /// Server does not have data on this non-owned object for the tick but expected to, such as a state should have arrived but did not.
        /// </summary>
        [System.Obsolete("This is currently not used but may be in a later release. Please read summary for value.")]
        CurrentPredicted = 2,
        /// <summary>
        /// Value is only seen on clients when they do not own the object.
        /// Client does not have data for the tick but expected to, such as a state should have arrived but did not.
        /// Client is currently reconciling.
        /// </summary>
        [System.Obsolete("This is currently not used but may be in a later release. Please read summary for value.")]
        ReplayedPredicted = 3,
        /// <summary>
        /// Value is only seen on clients.
        /// Client has data on the object for the tick.
        /// Client is currently reconciling.
        /// </summary>
        ReplayedCreated = 4,
        /// <summary>
        /// Value is only seen on clients when they do not own the object.
        /// Tick is in the future and data cannot yet be known.
        /// This can be used to exit replicate early to not process actions, or create actions based on previous datas.
        /// </summary>
        CurrentFuture = 5,
        /// <summary>
        /// Value is only seen on clients when they do not own the object.
        /// Tick is in the future and data cannot yet be known.
        /// Client is currently reconciling.
        /// This can be used to exit replicate early to not process actions, or create actions based on previous datas.
        /// </summary>
        ReplayedFuture = 6,
    }

    public static class ReplicateStateExtensions
    {
        /// <summary>
        /// Returns if value is valid.
        /// This should never be false.
        /// </summary>
        public static bool IsValid(this ReplicateState value) => (value != ReplicateState.Invalid);
        /// <summary>
        /// Returns if value is replayed.
        /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
        public static bool IsReplayed(this ReplicateState value) => (value == ReplicateState.ReplayedPredicted || value == ReplicateState.ReplayedCreated || value == ReplicateState.ReplayedFuture);
#pragma warning restore CS0618 // Type or member is obsolete
        /// <summary>
        /// Returns if value is user created.
        /// </summary>
        public static bool IsCreated(this ReplicateState value) => (value == ReplicateState.CurrentCreated || value == ReplicateState.ReplayedCreated);
        /// <summary>
        /// Returns if value is predicted.
        /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
        public static bool IsPredicted(this ReplicateState value) => (value == ReplicateState.ReplayedPredicted);
#pragma warning restore CS0618 // Type or member is obsolete
        /// <summary>
        /// Returns if value is in the future.
        /// </summary>
        public static bool IsFuture(this ReplicateState value) => (value == ReplicateState.CurrentFuture || value == ReplicateState.ReplayedFuture);
    }
}