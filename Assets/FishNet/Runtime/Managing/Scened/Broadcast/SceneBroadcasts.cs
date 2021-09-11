using FishNet.Managing.Scened.Data;
using FishNet.Broadcast;
using FishNet.Serializing.Helping;

namespace FishNet.Managing.Scened.Broadcast
{

    /// <summary>
    /// Sent to clients to load networked scenes.
    /// </summary>
    //[CodegenIncludeInternal]
    public struct LoadScenesBroadcast : IBroadcast
    {   
        public LoadQueueData QueueData;
    }
           
    /// <summary>       
    /// Sent to clients to unload networked scenes.
    /// </summary>     
   // [CodegenIncludeInternal]
    public struct UnloadScenesBroadcast : IBroadcast
    {
        public UnloadQueueData QueueData;
    }
       
    /// <summary> 
    /// Sent to server to indicate which scenes a client has loaded.
    /// </summary>
    //[CodegenIncludeInternal]
    public struct ClientScenesLoadedBroadcast : IBroadcast
    {
        public SceneLookupData[] SceneLookupDatas;
    }
     
}