﻿using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Component.Observing
{
    [CreateAssetMenu(menuName = "FishNet/Observers/Host Only Condition", fileName = "New Host Only Condition")]
    public class HostOnlyCondition : ObserverCondition
    {
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            /* Only return true if connection is the local client.
             * This check only runs on the server, so if local client
             * is true then they must also be the server (clientHost). */
            return (base.NetworkObject.ClientManager.Connection == connection);
        }

        public override bool Timed()
        {
            return false;
        }

        public override ObserverCondition Clone()
        {
            HostOnlyCondition copy = ScriptableObject.CreateInstance<HostOnlyCondition>();
            return copy;
        }
    }
}
