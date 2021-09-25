using FishNet.Managing;
using FishNet.Serializing;

namespace FishNet.Object.Synchronizing
{
    /// <summary>
    /// Add to custom classes which inherit from SyncBase for your own sync types.
    /// </summary>
    public interface ICustomSync
    {
        /// <summary>
        /// Return the serialized type.
        /// </summary>
        /// <returns></returns>
        object GetSerializedType();
    }


}