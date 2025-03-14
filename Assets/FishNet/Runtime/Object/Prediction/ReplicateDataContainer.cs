using FishNet.Utility;
using System.Runtime.CompilerServices;
using FishNet.CodeGenerating;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]

namespace FishNet.Object.Prediction
{
    [MakePublic]
    internal struct ReplicateDataContainer<T> where T : IReplicateData
    {
        /// <summary>
        /// Replicate data.
        /// </summary>
        public  T Data;
        /// <summary>
        /// True if the data was created locally or came through the network as created.
        /// </summary>
        public bool IsCreated;
        /// <summary>
        /// Channel the data came in on.
        /// </summary>
        public readonly Channel Channel;

        public ReplicateDataContainer(T data, Channel channel) : this(data, channel, tick: 0, isCreated: false) { }

        public ReplicateDataContainer(T data, Channel channel, bool isCreated) : this(data, channel, tick: 0, isCreated) { }
        public ReplicateDataContainer(T data, Channel channel, uint tick) : this(data, channel, tick, isCreated: false) { }

        public ReplicateDataContainer(T data, Channel channel, uint tick, bool isCreated)
        {
            Data = data;
            Channel = channel;
            Data.SetTick(tick);
            IsCreated = isCreated;
        }

        /// <summary>
        /// A shortcut to calling Data.SetTick.
        /// </summary>
        public void SetDataTick(uint tick) => Data.SetTick(tick);
        
        public void Dispose()
        {
            Data.Dispose();
        }

        public static ReplicateDataContainer<T> GetDefault(uint tick) => new(default(T), Channel.Unreliable, tick);

        public static ReplicateDataContainer<T> GetDefault() => GetDefault(tick: 0);
    }
    
}