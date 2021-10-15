using FishNet.Documenting;
using FishNet.Broadcast;

namespace FishNet.Managing.Scened
{

    /// <summary>
    /// Sent to clients to load networked scenes.
    /// </summary>
    [APIExclude]
    public struct LoadScenesBroadcast : IBroadcast
    {
        public LoadQueueData QueueData;
    }

    /// <summary>       
    /// Sent to clients to unload networked scenes.
    /// </summary>     
    [APIExclude]
    public struct UnloadScenesBroadcast : IBroadcast
    {
        public UnloadQueueData QueueData;
    }

    /// <summary> 
    /// Sent to server to indicate which scenes a client has loaded.
    /// </summary>
    [APIExclude]
    public struct ClientScenesLoadedBroadcast : IBroadcast
    {
        public SceneLookupData[] SceneLookupDatas;
    }

}