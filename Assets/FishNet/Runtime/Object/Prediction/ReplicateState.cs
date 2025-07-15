#if !FISHNET_STABLE_REPLICATESTATES
using System;
using FishNet.Utility;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]

namespace FishNet.Object.Prediction
{
    [Flags]
    public enum ReplicateState : byte
    {
        /// <summary>
        /// The default value of this state.
        /// This value should never occur when a replicate runs.
        /// </summary>
        Invalid = 0,
        /// <summary>
        /// Server and clients use this flag.
        /// Flag will be set if data tick has run outside a reconcile, such as from user code within OnTick.
        /// </summary>
        Ticked = 1 << 0, // 1
        /// <summary>
        /// Only client will use this flag.
        /// Flag is set if data is being run during a reconcile.
        /// </summary>
        Replayed = 1 << 1, // 2
        /// <summary>
        /// Server and client use this flag.
        /// Data has been created by the server or client.
        /// This indicates that data is known and was intentionally sent.
        /// </summary>
        Created = 1 << 2 // 4
    }

    public static class ReplicateStateExtensions
    {
        /// <summary>
        /// Returns if value is valid.
        /// This should never be false.
        /// </summary>
        public static bool IsValid(this ReplicateState value) => value != ReplicateState.Invalid;

        /// <summary>
        /// Returns if value contains ReplicateState.Ticked.
        /// </summary>
        public static bool ContainsTicked(this ReplicateState value) => value.FastContains(ReplicateState.Ticked);

        /// <summary>
        /// Returns if value contains ReplicateState.Created.
        /// </summary>
        public static bool ContainsCreated(this ReplicateState value) => value.FastContains(ReplicateState.Created);

        /// <summary>
        /// Returns if value contains ReplicateState.Replayed.
        /// </summary>
        public static bool ContainsReplayed(this ReplicateState value) => value.FastContains(ReplicateState.Replayed);

        [Obsolete("Use ContainsReplayed.")]
        public static bool IsReplayed(this ReplicateState value) => value.ContainsReplayed();

        /// <summary>
        /// Returns if value is (ReplicateState.Ticked | ReplicateState.Created).
        /// </summary>
        public static bool IsTickedCreated(this ReplicateState value) => value == (ReplicateState.Ticked | ReplicateState.Created);

        /// <summary>
        /// Returns if value equals ReplicateState.Ticked.
        /// </summary>
        public static bool IsTickedNonCreated(this ReplicateState value) => value == ReplicateState.Ticked;

        /// <summary>
        /// Returns if value is (ReplicateState.Replayed | ReplicateState.Ticked | ReplicateState.Created).
        /// </summary>
        public static bool IsReplayedCreated(this ReplicateState value) => value == (ReplicateState.Replayed | ReplicateState.Created);

        /// <summary>
        /// Returns if value is ReplicateState.Replayed without ReplicateState.Ticked nor ReplicateState.Created.
        /// </summary>
        public static bool IsFuture(this ReplicateState value) => value == ReplicateState.Replayed;

        [Obsolete("Use ContainsCreated.")]
        public static bool IsCreated(this ReplicateState value) => value.ContainsCreated();

        /// <summary>
        /// True if part is containined within whole.
        /// </summary>
        public static bool FastContains(this ReplicateState whole, ReplicateState part) => (whole & part) == part;
    }
}
#else
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
#endif