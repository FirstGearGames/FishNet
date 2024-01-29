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
        /// Data had been received for the current tick.
        /// This occurs when a replicate is called on owner, or when receiving forwarded inputs.
        /// </summary>
        CurrentCreated = 1,
        /// <summary>
        /// Data was not received for the current tick.
        /// Either no data was available to forward or their may be latency concerns resulting in late packets.
        /// </summary>
        CurrentPredicted = 2,
        /// <summary>
        /// Data was not received for the replayed tick.
        /// This occurs when a replicate would be replaying past datas, triggered by a reconcile, but there is no user created data for the tick.
        /// </summary>
        ReplayedPredicted = 3,
        /// <summary>
        /// Data was received for the replayed tick.
        /// This occurs when a replicate would be replaying past datas, triggered by a reconcile, and there is user created data for the tick.
        /// </summary>
        ReplayedCreated = 4,
        /// <summary>
        /// Client has not run the tick locally yet. This can be used to exit replicate early to not process actions, or create actions based on previous datas.
        /// </summary>
        Future = 5,
    }

    public static class ReplicateStateExtensions
    {
        /// <summary>
        /// Returns if value is valid.
        /// </summary>
        public static bool IsValid(this ReplicateState value) => (value != ReplicateState.Invalid);
        /// <summary>
        /// Returns if value is replayed.
        /// </summary>
        public static bool IsReplayed(this ReplicateState value) => (value == ReplicateState.ReplayedPredicted || value == ReplicateState.ReplayedCreated || value == ReplicateState.Future);
        /// <summary>
        /// Returns if value is user created.
        /// </summary>
        public static bool IsCreated(this ReplicateState value) => (value == ReplicateState.CurrentCreated || value == ReplicateState.ReplayedCreated);
        /// <summary>
        /// Returns if value is predicted.
        /// </summary>
        public static bool IsPredicted(this ReplicateState value) => (value == ReplicateState.Future || value == ReplicateState.CurrentPredicted || value == ReplicateState.ReplayedPredicted);
    }
}