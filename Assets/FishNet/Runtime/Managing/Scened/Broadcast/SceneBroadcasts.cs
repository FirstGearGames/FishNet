using FishNet.Managing.Scened.Data;
using FishNet.Broadcast;
using FishNet.Serializing.Helping;

namespace FishNet.Managing.Scened.Broadcast
{

    /// <summary>
    /// Sent to clients to load networked scenes.
    /// </summary>
    [CodegenIncludeInternal] 
    public struct LoadScenesBroadcast : IBroadcast
    {   
        public LoadSceneQueueData SceneQueueData;
    }
           
    /// <summary>       
    /// Sent to clients to unload networked scenes.
    /// </summary>     
    [CodegenIncludeInternal]
    public struct UnloadScenesBroadcast : IBroadcast
    {
        public UnloadSceneQueueData SceneQueueData;
    }
       
    /// <summary> 
    /// Sent to server to indicate which scenes a client has loaded.
    /// </summary>
    [CodegenIncludeInternal]
    public struct ClientScenesLoadedBroadcast : IBroadcast
    {
        public SceneReferenceData[] SceneDatas;
    }
     
}