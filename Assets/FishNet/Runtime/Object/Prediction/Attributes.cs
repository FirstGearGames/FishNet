using System;

namespace FishNet.Object.Prediction
{
    /// <summary>
    /// Replicated methods are to be called from clients and will run the same data and logic on the server.
    /// Only data used as method arguments will be serialized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ReplicateAttribute : Attribute 
    {
        /// <summary>
        /// How many past datas to resend.
        /// </summary>
        public byte Resends = 5;
    }
    /// <summary>
    /// Reconcile methods indicate how to reset your script or object after the server has replicated user data.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ReconcileAttribute : Attribute 
    {
        /// <summary>
        /// How many times to resend reconcile.
        /// </summary>
        public byte Resends = 3;
    }

}