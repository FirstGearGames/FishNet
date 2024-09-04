using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using UnityEngine;

namespace FishNet.Example.ComponentStateSync
{
    public static class AMSSerializer
    {
        public static void WriteAMS(this Writer w, AMonoScript value)
        {
        }
        public static AMonoScript ReadAMS(this Reader r)
        {
            return default;
        }
    }
    public class ComponentSyncStateBehaviour : NetworkBehaviour
    {
        /// <summary>  
        /// Using my custom SyncType for Structy.
        /// </summary>
        
        private readonly ComponentStateSync<AMonoScript> _syncScript = new();

        private void Awake()
        {
            AMonoScript ams = GetComponent<AMonoScript>();
            //Initialize with the component of your choice.
            _syncScript.Initialize(ams);
            //Optionally listen for changes.
            _syncScript.OnChange += _syncScript_OnChange;
        }

        /// <summary>
        /// Called when enabled state changes for SyncScript.
        /// </summary>
        private void _syncScript_OnChange(AMonoScript component, bool prevState, bool nextState, bool asServer)
        {
            Debug.Log($"Change received on {component.GetType().Name}. New value is {nextState}. Received asServer {asServer}.");
        }

        private void Update()
        {
            //Every so often flip the state of the component.
            if (base.IsServerStarted && Time.frameCount % 200 == 0)
                _syncScript.Enabled = !_syncScript.Enabled;
        }

    }


}