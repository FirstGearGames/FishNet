using System;
using FishNet.Utility;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]

namespace FishNet.Object
{
    [System.Flags]
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
        Ticked = (1 << 0),
        /// <summary>
        /// Only client will use this flag.
        /// Flag is set if data is being run during a reconcile.
        /// </summary>
        Replayed = (1 << 1),
        /// <summary>
        /// Server and client use this flag.
        /// Data has been created by the server or client.
        /// This indicates that data is known and was intentionally sent.
        /// </summary>
        Created = (1 << 2),

        /// <summary>
        /// Value is seen on server and clients.
        /// Client or server has data on the object for the tick.
        /// Clients will only see this value on spectated objects when PredictionManager is using Appended state order.
        /// </summary>
        [Obsolete("Use IsTickedCreated() instead.")]
        CurrentCreated = (Ticked | Created),
        /// <summary>
        /// Value is only seen on clients.
        /// Client has data on the object for the tick.
        /// Client is currently reconciling.
        /// </summary>
        [Obsolete("Use IsReplayedCreated() instead.")]
        ReplayedCreated = (Replayed | Created),
        /// <summary>
        /// Value is only seen on clients when they do not own the object.
        /// Tick is in the future and data cannot yet be known.
        /// This can be used to exit replicate early to not process actions, or create actions based on previous datas.
        /// </summary>
        [Obsolete("Use IsTickedNonCreated() instead.")]
        CurrentFuture = Ticked,
        /// <summary>
        /// Value is only seen on clients when they do not own the object.
        /// Tick is in the future and data cannot yet be known.
        /// Client is currently reconciling.
        /// This can be used to exit replicate early to not process actions, or create actions based on previous datas.
        /// </summary>
        [Obsolete("Use IsFuture() instead.")]
        ReplayedFuture = Replayed,
    }

    public static class ReplicateStateExtensions
    {
        /// <summary>
        /// Returns if value is valid.
        /// This should never be false.
        /// </summary>
        public static bool IsValid(this ReplicateState value) => (value != ReplicateState.Invalid);

        /// <summary>
        /// Returns if value contains ReplicateState.Ticked.
        /// </summary>
        public static bool ContainsTicked(this ReplicateState value) => value.FastContains(ReplicateState.Ticked);

        /// <summary>
        /// Returns if value contains ReplicateState.Created.
        /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
        public static bool ContainsCreated(this ReplicateState value) => value.FastContains(ReplicateState.Created) || value == ReplicateState.CurrentCreated || value == ReplicateState.ReplayedCreated;
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// Returns if value contains ReplicateState.Replayed.
        /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
        public static bool ContainsReplayed(this ReplicateState value) => value.FastContains(ReplicateState.Replayed) || value == ReplicateState.ReplayedCreated || value == ReplicateState.ReplayedFuture;
#pragma warning restore CS0618 // Type or member is obsolete

        [Obsolete("Use ContainsReplayed.")]
        public static bool IsReplayed(this ReplicateState value) => value.ContainsReplayed();

        /// <summary>
        /// Returns if value is (ReplicateState.Ticked | ReplicateState.Created).
        /// </summary>
        public static bool IsTickedCreated(this ReplicateState value) => (value == (ReplicateState.Ticked | ReplicateState.Created));

        /// <summary>
        /// Returns if value equals (ReplicateState.Ticked.
        /// </summary>
        public static bool IsTickedNonCreated(this ReplicateState value) => (value == ReplicateState.Ticked);

        /// <summary>
        /// Returns if value is (ReplicateState.Replayed | ReplicateState.Ticked | ReplicateState.Created).
        /// </summary>
        public static bool IsReplayedCreated(this ReplicateState value) => (value == (ReplicateState.Replayed | ReplicateState.Created));

        /// <summary>
        /// Returns if value is ReplicateState.Replayed without ReplicateState.Ticked nor ReplicateState.Created.
        /// </summary>
        public static bool IsFuture(this ReplicateState value) => value.ContainsReplayed() && !value.ContainsTicked() && !value.ContainsCreated();

        [Obsolete("Use ContainsCreated.")]
        public static bool IsCreated(this ReplicateState value) => value.ContainsCreated();

        /// <summary>
        /// True if part is containined within whole.
        /// </summary>
        public static bool FastContains(this ReplicateState whole, ReplicateState part) => (whole & part) == whole;
    }
}