using System;

namespace FishNet.Object.Prediction
{
    /* Held idle input (opt-in).
     *
     * An owner standing still produces the same input every tick. Without this, every one of those ticks is sent (the input
     * is not "default", since it carries an aim and a has-data marker), which for an idle player is most of its upload, and
     * the server needs every one of them because an empty queue runs DEFAULT data, which a game cannot treat as "the same as
     * before".
     *
     * With a type opted in, an input that repeats the previous one and may be held (CanHold) does not restart the owner's
     * resend countdown, so after the usual redundant resends the owner stops sending, apart from a keepalive. The server,
     * finding the queue empty after such an input, runs that input again on each tick (HELD), numbered on from the last one,
     * exactly what the owner is running. When real input resumes, the held ticks are lined up by number with what arrives
     * (ReplicateHoldPlanner): identical repeats are dropped, the first different input runs on its own tick, and a late one
     * runs at once. Nothing the owner sent is ever discarded unless it is identical to what the server already ran. */

    /// <summary>
    /// Opts a replicate type into held idle input. Both delegates must be set; value types only.
    /// </summary>
    public static class ReplicateIdle<T> where T : IReplicateData
    {
        /// <summary>True when this input may be repeated by the server in the owner's absence: it starts nothing (no one-shot action) and is safe to run again.</summary>
        public static Func<T, bool> CanHold;

        /// <summary>True when two inputs are the same apart from their tick.</summary>
        public static Func<T, T, bool> SameInput;

        /// <summary>True when the type is opted in.</summary>
        public static bool Enabled => CanHold != null && SameInput != null && typeof(T).IsValueType;

        /// <summary>True when <paramref name = "current"/> repeats <paramref name = "previous"/> and may be held.</summary>
        public static bool IsRepeat(T previous, T current) => Enabled && CanHold(current) && SameInput(previous, current);
    }

    /// <summary>
    /// Settings for held idle input.
    /// </summary>
    public static class ReplicateIdle
    {
        /// <summary>Ticks between keepalive sends of a held input, so a server that lost the last ones still learns it.</summary>
        public static uint KeepaliveTicks = 30;
    }

    /// <summary>What the server does on one tick while it is holding an owner's input.</summary>
    public enum ReplicateHoldAction : byte
    {
        /// <summary>Run the held input again, on the next tick number.</summary>
        RunHeld = 0,
        /// <summary>The queue's head repeats the held input: discard it and decide again.</summary>
        DropHead = 1,
        /// <summary>The queue's head is due (or late): run it and stop holding.</summary>
        RunHead = 2,
    }

    /// <summary>
    /// The server's per-tick decision while holding an owner's input. Pure, so it is tested on its own.
    /// </summary>
    public static class ReplicateHoldPlanner
    {
        /// <summary>
        /// Decides one step. Called repeatedly while it returns <see cref = "ReplicateHoldAction.DropHead"/>.
        /// </summary>
        /// <param name = "hasHead">True when the queue holds an input.</param>
        /// <param name = "headTick">The head's tick.</param>
        /// <param name = "headIsRepeat">True when the head repeats the held input.</param>
        /// <param name = "nextTick">The tick number the next held run would take.</param>
        /// <param name = "maximumWaitTicks">The most ticks a real input may wait for its number; beyond that the numbering jumps forward so it waits exactly this long (the owner's clock moved ahead while idle).</param>
        /// <param name = "runTick">For <see cref = "ReplicateHoldAction.RunHeld"/>: the tick number to run it on.</param>
        public static ReplicateHoldAction Next(bool hasHead, uint headTick, bool headIsRepeat, uint nextTick, uint maximumWaitTicks, out uint runTick)
        {
            runTick = nextTick;
            if (!hasHead)
                return ReplicateHoldAction.RunHeld;
            // The same as what is being synthesised: running it would only repeat a tick or skip ahead.
            if (headIsRepeat)
                return ReplicateHoldAction.DropHead;
            // Due now, or late (its tick was already run as held): run it. Late input is never discarded.
            if ((int)(headTick - nextTick) <= 0)
                return ReplicateHoldAction.RunHead;
            // Early: wait for its number, but never longer than the buffer allows.
            uint wait = headTick - nextTick;
            if (wait > maximumWaitTicks)
                runTick = headTick - maximumWaitTicks;
            return ReplicateHoldAction.RunHeld;
        }
    }
}
