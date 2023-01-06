using FishNet.Managing.Timing;
using MonoFN.Cecil;
using System;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{

    internal class TimeManagerHelper : CodegenBase
    {

        #region Reflection references.
        internal MethodReference LocalTick_MethodRef;
        internal MethodReference TickDelta_MethodRef;
        internal MethodReference MaximumBufferedInputs_MethodRef;
        internal MethodReference PhysicsMode_MethodRef;
        internal MethodReference InvokeOnReconcile_MethodRef;
        internal MethodReference InvokeOnReplicateReplay_MethodRef;
        #endregion

        public override bool ImportReferences()
        {
            //TimeManager infos.
            Type timeManagerType = typeof(TimeManager);
            foreach (System.Reflection.PropertyInfo pi in timeManagerType.GetProperties())
            {
                if (pi.Name == nameof(TimeManager.LocalTick))
                    LocalTick_MethodRef = base.ImportReference(pi.GetMethod);
                //else if (pi.Name == nameof(TimeManager.MaximumBufferedInputs))
                //    MaximumBufferedInputs_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(TimeManager.PhysicsMode))
                    PhysicsMode_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(TimeManager.TickDelta))
                    TickDelta_MethodRef = base.ImportReference(pi.GetMethod);
            }

            foreach (System.Reflection.MethodInfo mi in timeManagerType.GetMethods())
            {
                if (mi.Name == nameof(TimeManager.InvokeOnReconcileInternal))
                    InvokeOnReconcile_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(TimeManager.InvokeOnReplicateReplayInternal))
                    InvokeOnReplicateReplay_MethodRef = base.ImportReference(mi);
            }

            return true;
        }


    }
}
