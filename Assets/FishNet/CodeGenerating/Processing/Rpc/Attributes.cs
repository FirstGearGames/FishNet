using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Object.Helping;
using MonoFN.Cecil;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Processing.Rpc
{
    internal class Attributes
    {

        /// <summary>
        /// Returns if methodDef has any Rpc attribute.
        /// </summary>
        public bool HasRpcAttributes(MethodDefinition methodDef)
        {
            foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
            {
                RpcType rt = CodegenSession.AttributeHelper.GetRpcAttributeType(customAttribute);
                if (rt != RpcType.None)
                    return true;
            }

            //Fall through, nothing found.
            return false;
        }

        /// <summary>
        /// Returns a collection of RpcAttribute for methodDef.
        /// </summary>
        public List<AttributeData> GetRpcAttributes(MethodDefinition methodDef)
        {
            List<AttributeData> results = new List<AttributeData>();

            foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
            {
                RpcType rt = CodegenSession.AttributeHelper.GetRpcAttributeType(customAttribute);
                if (rt != RpcType.None)
                    results.Add(new AttributeData(customAttribute, rt));
            }

            //Nothing found, exit early.
            if (results.Count == 0)
            {
                return results;
            }
            //If more than one attribute make sure the combination is allowed.
            else if (results.Count >= 2)
            {
                RpcType allRpcTypes = results.GetCombinedRpcType();
                if (allRpcTypes != (RpcType.Observers | RpcType.Target))
                {
                    CodegenSession.LogError($"{methodDef.Name} contains multiple RPC attributes. Only ObserversRpc and TargetRpc attributes may be combined.");
                    return new List<AttributeData>();
                }
            }

            //Next validate that the method is setup properly for each rpcType.
            foreach (AttributeData ra in results)
            {
                //If not valid then return empty list.
                if (!IsRpcMethodValid(methodDef, ra.RpcType))
                    return new List<AttributeData>();
            }
           
            return results;
        }

        /// <summary>
        /// Returns if a RpcMethod can be serialized and has a proper signature.
        /// </summary>
        private bool IsRpcMethodValid(MethodDefinition methodDef, RpcType rpcType)
        {
            //Static method.
            if (methodDef.IsStatic)
            {
                CodegenSession.LogError($"{methodDef.Name} RPC method cannot be static.");
                return false;
            }
            //Is generic type.
            else if (methodDef.HasGenericParameters)
            {
                CodegenSession.LogError($"{methodDef.Name} RPC method cannot contain generic parameters.");
                return false;
            }
            //Abstract method.
            else if (methodDef.IsAbstract)
            {
                CodegenSession.LogError($"{methodDef.Name} RPC method cannot be abstract.");
                return false;
            }
            //Non void return.
            else if (methodDef.ReturnType != methodDef.Module.TypeSystem.Void)
            {
                CodegenSession.LogError($"{methodDef.Name} RPC method must return void.");
                return false;
            }
            //Misc failing conditions.
            else
            {
            }
            //TargetRpc but missing correct parameters.
            if (rpcType == RpcType.Target)
            {
                if (methodDef.Parameters.Count == 0 || !methodDef.Parameters[0].Is(typeof(NetworkConnection)))
                {
                    CodegenSession.LogError($"Target RPC {methodDef.Name} must have a NetworkConnection as the first parameter.");
                    return false;
                }
            }

            //Make sure all parameters can be serialized.
            for (int i = 0; i < methodDef.Parameters.Count; i++)
            {
                ParameterDefinition parameterDef = methodDef.Parameters[i];

                //If NetworkConnection, TargetRpc, and first parameter.
                if ((i == 0) && (rpcType == RpcType.Target) && parameterDef.Is(typeof(NetworkConnection)))
                    continue;

                if (parameterDef.ParameterType.IsGenericParameter)
                {
                    CodegenSession.LogError($"RPC method{methodDef.Name} contains a generic parameter. This is currently not supported.");
                    return false;
                }

                //Can be serialized/deserialized.
                bool canSerialize = CodegenSession.GeneralHelper.HasSerializerAndDeserializer(parameterDef.ParameterType, true);
                if (!canSerialize)
                {
                    CodegenSession.LogError($"RPC method {methodDef.Name} parameter type {parameterDef.ParameterType.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                    return false;
                }

            }

            //Fall through, success.
            return true;
        }

    }
}