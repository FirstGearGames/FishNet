using System.Runtime.CompilerServices;

namespace FishNet.Managing.Timing
{

    public struct EstimatedTick
    {

        /// <summary>
        /// How to handle old ticks, specifically related to EstimatedTick.
        /// </summary>
        public enum OldTickOption : byte
        {
            /// <summary>
            /// Completely ignore old ticks.
            /// </summary>
            Discard = 0,
            /// <summary>
            /// Set LastRemoteTick but do not update RemoteTick.
            /// </summary>
            SetLastRemoteTick = 1,
            /// <summary>
            /// Set LastRemoteTick and RemoteTick.
            /// </summary>
            SetRemoteTick = 2,
        }

        /// <summary>
        /// Local tick when this was last updated.
        /// </summary>
        public uint LocalTick;
        /// <summary>
        /// Last remote tick this was updated with that was not out of order or a duplicate.
        /// </summary>
        public uint RemoteTick;
        /// <summary>
        /// Last remote tick received regardless if it was out of order or a duplicate.
        /// </summary>
        public uint LastRemoteTick;
        /// <summary>
        /// True if the value was updated this tick.
        /// </summary>
        public bool IsCurrent(TimeManager tm) => (!IsUnset && LocalTick == tm.LocalTick);
        /// <summary>
        /// True if value is unset.
        /// </summary>
        //Only need to check one value for unset as they all would be if not set.
        public bool IsUnset => (LocalTick == 0);
        /// <summary>
        /// Number of ticks LocalTick is being current LocalTick.
        /// </summary>
        public uint LocalTickDifference(TimeManager tm)
        {
            long value = (tm.LocalTick - LocalTick);
            //Shouldn't be possible to be less than 0.
            if (value < 0)
                return 0;
            else if (value > uint.MaxValue)
                value = uint.MaxValue;

            return (uint)value;
        }

        /// <summary>
        /// Updates values.
        /// </summary>
        /// <param name="nm">NetworkManager to use.</param>
        /// <param name="remoteTick">Remote tick being updated.</param>
        /// <param name="ignoreOldTicks">True to not update if remote tick is older or equal to the last updated value.</param>
        /// <returns>True if was able to update values.</returns>
        public bool Update(TimeManager tm, uint remoteTick, OldTickOption oldTickOption = OldTickOption.Discard)
        {
            //Always set LastRemoteTick even if out of order.
            LastRemoteTick = remoteTick;
            //If cannot update with old values return.
            if (oldTickOption != OldTickOption.SetRemoteTick && remoteTick <= RemoteTick)
                return false;

            //nm is assumed set here.
            LocalTick = tm.LocalTick;
            RemoteTick = remoteTick;

            return true;
        }

        /// <summary>
        /// Current estimated value.
        /// </summary>
        /// <param name="nm">NetworkManager to use. When null default value will be returned.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Value(TimeManager tm)
        {
            return Value(tm, out _);
        }

        /// <summary>
        /// Current estimated value. Outputs if value is current.
        /// </summary>
        /// <param name="nm">NetworkManager to use. When null default value will be returned.</param>
        /// <param name="isCurrent">True if the value was updated this local tick.</param>
        public uint Value(TimeManager tm, out bool isCurrent)
        {
            isCurrent = IsCurrent(tm);
            if (tm == null)
                return 0;
            if (IsUnset)
                return 0;

            uint diff = (tm.LocalTick - LocalTick);
            return (diff + RemoteTick);
        }

        /// <summary>
        /// Resets values to unset.
        /// </summary>
        public void Reset()
        {
            LocalTick = 0;
            RemoteTick = 0;
            LastRemoteTick = 0;
        }
    }


}