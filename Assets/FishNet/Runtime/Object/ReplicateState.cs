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
        /// Data being used is confirmed to be actual, non-future data.
        /// </summary>
        NewData = 1,
        /// <summary>
        /// No data was available for use from the replicate queue.
        /// When this value is present you may manually predict future data.
        /// </summary>
        UnsetData = 2,
        /// <summary>
        /// Data which was confirmed is being replayed.
        /// </summary>
        ReplayedNewData = 3,
        /// <summary>
        /// Data which was unset or predicted is being replayed.
        /// </summary>
        ReplayedUnsetData = 4,
    }
}