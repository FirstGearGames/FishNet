#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Editing.Beta
{
    public class BetaModeMenu : MonoBehaviour
    {
        #region const.
        private const string NETWORKTRANSFORM_POSITION_PACKING_DEFINE = "FISHNET_NETWORKTRANSFORM_POSITION_PACKING";
        private const string STABLE_RECURSIVE_DESPAWNS_DEFINE = "FISHNET_STABLE_RECURSIVE_DESPAWNS";
        private const string THREADED_TICKSMOOTHERS_DEFINE = "FISHNET_THREADED_TICKSMOOTHERS";
        private const string THREADED_COLLIDER_ROLLBACK_DEFINE = "FISHNET_THREADED_COLLIDER_ROLLBACK";
        private const string ANIMATOR_CHANNEL_DEFINE = "FISHNET_ANIMATOR_CHANNEL";
        #endregion

        
        #region Channel Animator
        #if FISHNET_ANIMATOR_CHANNEL
        [MenuItem("Tools/Fish-Networking/Beta/Disable Animator Channel", false, -1102)]
        private static void DisableAnimatorChannel() => SetAnimatorChannel(useStable: true);
        #else
        [MenuItem("Tools/Fish-Networking/Beta/Enable Animator Channel", false, -1102)]
        private static void EnableAnimatorChannel() => SetAnimatorChannel(useStable: false);
        #endif
        private static void SetAnimatorChannel(bool useStable)
        {
            bool result = DeveloperMenu.RemoveOrAddDefine(ANIMATOR_CHANNEL_DEFINE, removeDefine: useStable);
            if (result)
                Debug.LogWarning($"Beta Animator Channel is now {GetBetaEnabledText(useStable)}.");
        }
        #endregion
        
        #region Beta Recursive Despawns
        #if FISHNET_STABLE_RECURSIVE_DESPAWNS
        [MenuItem("Tools/Fish-Networking/Beta/Enable Recursive Despawns", false, -1101)]
        private static void EnableBetaRecursiveDespawns() => SetBetaRecursiveDespawns(useStable: false);
        #else
        [MenuItem("Tools/Fish-Networking/Beta/Disable Recursive Despawns", false, -1101)]
        private static void DisableBetaRecursiveDespawns() => SetBetaRecursiveDespawns(useStable: true);
        #endif
        private static void SetBetaRecursiveDespawns(bool useStable)
        {
            bool result = DeveloperMenu.RemoveOrAddDefine(STABLE_RECURSIVE_DESPAWNS_DEFINE, removeDefine: !useStable);
            if (result)
                Debug.LogWarning($"Beta Recursive Despawns are now {GetBetaEnabledText(useStable)}.");
        }
        #endregion

        #region Beta ThreadedSmothers
        /* Changes by https://github.com/belplaton
         * Content: Threaded TickSmoothers
         *      Migrating the network interpolation system for the graphical world to a multithreaded Unity Jobs + Burst implementation. */
        #if FISHNET_THREADED_TICKSMOOTHERS
        [MenuItem("Tools/Fish-Networking/Beta/Disable Threaded TickSmoothers", false, -1101)]
        private static void DisableBetaThreadedSmoothers() => SetBetaThreadedSmoothers(useStable: true);
        #else
        [MenuItem("Tools/Fish-Networking/Beta/Enable Threaded TickSmoothers", false, -1101)]
        private static void EnableBetaThreadedSmoothers()
        {
            #if UNITYMATHEMATICS || UNITYMATHEMATICS_131 || UNITYMATHEMATICS_132
            SetBetaThreadedSmoothers(useStable: false);
            #else
            Debug.LogError($"You must install the package com.unity.mathematics to use Beta Threaded TickSmoothers.");
            #endif
        }
        #endif

        private static void SetBetaThreadedSmoothers(bool useStable)
        {
            bool result = DeveloperMenu.RemoveOrAddDefine(THREADED_TICKSMOOTHERS_DEFINE, removeDefine: useStable);
            if (result)
                Debug.LogWarning($"Beta Threaded TickSmoothers are now {GetBetaEnabledText(useStable)}.");
        }
        #endregion

        #region Beta Threaded Collider Rollback
        /* Changes by https://github.com/belplaton
         * Content: Threaded Collider Rollback
         *      Migrating collider rollback -- commonly used for hitbox tracing -- to a multithreaded Unity Jobs + Burst implementation. */
        #if FISHNET_THREADED_COLLIDER_ROLLBACK
        [MenuItem("Tools/Fish-Networking/Beta/Disable Threaded Collider Rollback", false, -1101)]
        private static void DisableBetaThreadedColliderRollback() => SetBetaThreadedColliderRollback(useStable: true);
        #else
        [MenuItem("Tools/Fish-Networking/Beta/Enable Threaded Collider Rollback", false, -1101)]
        private static void EnableBetaThreadedColliderRollback()
        {
            #if UNITYMATHEMATICS || UNITYMATHEMATICS_131 || UNITYMATHEMATICS_132
            SetBetaThreadedColliderRollback(useStable: false);
            #else
            Debug.LogError($"You must install the package com.unity.mathematics to use Beta Threaded Collider Rollhack..");
            #endif
        }
        #endif

        private static void SetBetaThreadedColliderRollback(bool useStable)
        {
            bool result = DeveloperMenu.RemoveOrAddDefine(THREADED_COLLIDER_ROLLBACK_DEFINE, removeDefine: useStable);
            if (result)
                Debug.LogWarning($"Beta Threaded Collider Rollbacks are now {GetBetaEnabledText(useStable)}.");
        }
        #endregion

        #region Beta NetworkTransform Position Packing
        /* Content: NetworkTransform Position Packing
         *      Adds Position Packing Bits and Position Compression Scale to NetworkTransform, so positions can be packed into 24 bits or at a scale other than 100.
         *      This changes what is sent over the network, so the define is set for every build target: a server and its clients must be built with the same setting. */
        #if FISHNET_NETWORKTRANSFORM_POSITION_PACKING
        [MenuItem("Tools/Fish-Networking/Beta/Disable NetworkTransform Position Packing", false, -1101)]
        private static void DisableBetaNetworkTransformPositionPacking() => SetBetaNetworkTransformPositionPacking(useStable: true);
        #else
        [MenuItem("Tools/Fish-Networking/Beta/Enable NetworkTransform Position Packing", false, -1101)]
        private static void EnableBetaNetworkTransformPositionPacking() => SetBetaNetworkTransformPositionPacking(useStable: false);
        #endif
        private static void SetBetaNetworkTransformPositionPacking(bool useStable)
        {
            bool result = DeveloperMenu.RemoveOrAddDefineForAllBuildTargets(NETWORKTRANSFORM_POSITION_PACKING_DEFINE, removeDefine: useStable);
            if (result)
                Debug.LogWarning($"Beta NetworkTransform Position Packing is now {GetBetaEnabledText(useStable)} for all build targets. Servers and clients must be built with the same setting.");
        }
        #endregion

        private static string GetBetaEnabledText(bool useStable)
        {
            return useStable ? "disabled" : "enabled";
        }
    }
}

#endif