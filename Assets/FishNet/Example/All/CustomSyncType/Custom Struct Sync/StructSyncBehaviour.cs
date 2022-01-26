using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace FishNet.Example.CustomSyncObject
{

    public class StructSyncBehaviour : NetworkBehaviour
    {
        /// <summary>
        /// Using my custom SyncType for Structy.
        /// </summary>
        [SyncObject]
        private readonly StructySync _structy = new StructySync();

        private void Awake()
        {
            //Listen for change events.
            _structy.OnChange += _structy_OnChange;
        }

        private void _structy_OnChange(StructySync.CustomOperation op, Structy oldItem, Structy newItem, bool asServer)
        {
            Debug.Log("Changed " + op.ToString() + ", " + newItem.Age + ", " + asServer);
        }

        private void Update()
        {
            //Every so often increase the age property on structy using StructySync, my custom sync type.
            if (base.IsServer && Time.frameCount % 200 == 0)
            {
                /* Custom code inside StructySync to return
                 * current value. You can expose this, or don't, however
                 * you like. */
                Structy s = _structy.GetValue(true);
                //Increase age.
                _structy.SetAge((ushort)(s.Age + 1));
            }
        }

    }


}