
namespace FishNet.Observing
{
    /// <summary>
    /// How a condition is handled.
    /// This is intentionally not set as flags.
    /// </summary>
    public enum ObserverConditionType : byte
    {
        Normal = 1,
        Timed = 2,
    }
}
