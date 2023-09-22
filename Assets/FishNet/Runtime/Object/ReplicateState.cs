using FishNet.Utility.Constant;
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
        /// Data is user made, such if it were created within OnTick.
        /// This occurs when a replicate is called from user code.
        /// </summary>
        UserCreated = 1,
        /// <summary>
        /// No data was made from the user; default data is used with an estimated tick.
        /// This occurs on non-owned objects or server when a replicate is called from user code, and there are no datas enqeued.
        /// </summary>
        Predicted = 2,
        /// <summary>
        /// Data is user made, such if it were created within OnTick.
        /// This occurs when a replicate is replaying past datas, triggered by a reconcile. 
        /// </summary>
        ReplayedUserCreated = 3,
        /// <summary>
        /// No data was made from the user; default data is used with an estimated tick.
        /// This occurs when a replicate would be replaying past datas, triggered by a reconcile, but there is no user created data for the tick.
        /// </summary>
        ReplayedPredicted = 4,
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
        public static bool IsReplayed(this ReplicateState value) => (value == ReplicateState.ReplayedPredicted || value == ReplicateState.ReplayedUserCreated);
        /// <summary>
        /// Returns if value is user created.
        /// </summary>
        public static bool IsUserCreated(this ReplicateState value) => (value == ReplicateState.UserCreated || value == ReplicateState.ReplayedUserCreated);
        /// <summary>
        /// Returns if value is predicted.
        /// </summary>
        public static bool IsPredicted(this ReplicateState value) => !value.IsUserCreated();
    }
}