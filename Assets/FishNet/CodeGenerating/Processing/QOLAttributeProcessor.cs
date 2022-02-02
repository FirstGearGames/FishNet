using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Managing.Logging;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace FishNet.CodeGenerating.Processing
{
    internal class QolAttributeProcessor
    {

        internal bool Process(TypeDefinition typeDef)
        {
            bool modified = false;
            List<MethodDefinition> methods = typeDef.Methods.ToList();
            foreach (MethodDefinition methodDef in methods)
            {
                //Has RPC attribute, doesn't quality for a quality of life attribute.
                if (CodegenSession.NetworkBehaviourRpcProcessor.GetRpcAttribute(methodDef, false, out _) != null)
                    continue;

                QolAttributeType qolType;
                CustomAttribute qolAttribute = GetQOLAttribute(methodDef, out qolType);
                if (qolAttribute == null)
                    continue;

                /* This is a one time check to make sure the qolType is
                 * a supported value. Multiple methods beyond this rely on the
                 * value being supported. Rather than check in each method a
                 * single check is performed here. */
                if (qolType != QolAttributeType.Server && qolType != QolAttributeType.Client)
                {
                    CodegenSession.LogError($"QolAttributeType of {qolType.ToString()} is unhandled.");
                    continue;
                }

                CreateAttributeMethod(methodDef, qolAttribute, qolType);
                modified = true;
            }

            return modified;
        }

        /// <summary>
        /// Returns the RPC attribute on a method, if one exist. Otherwise returns null.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="rpcType"></param>
        /// <returns></returns>
        private CustomAttribute GetQOLAttribute(MethodDefinition methodDef, out QolAttributeType qolType)
        {
            CustomAttribute foundAttribute = null;
            qolType = QolAttributeType.None;
            //Becomes true if an error occurred during this process.
            bool error = false;

            foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
            {
                QolAttributeType thisQolType = CodegenSession.AttributeHelper.GetQolAttributeType(customAttribute.AttributeType.FullName);
                if (thisQolType != QolAttributeType.None)
                {
                    //A qol attribute already exist.
                    if (foundAttribute != null)
                    {
                        CodegenSession.LogError($"{methodDef.Name} {thisQolType.ToString()} method cannot have multiple quality of life attributes.");
                        error = true;
                    }
                    ////Static method.
                    //if (methodDef.IsStatic)
                    //{
                    //    CodegenSession.AddError($"{methodDef.Name} {thisQolType.ToString()} method cannot be static.");
                    //    error = true;
                    //}
                    //Abstract method.
                    if (methodDef.IsAbstract)
                    {
                        CodegenSession.LogError($"{methodDef.Name} {thisQolType.ToString()} method cannot be abstract.");
                        error = true;
                    }

                    //If all checks passed.
                    if (!error)
                    {
                        foundAttribute = customAttribute;
                        qolType = thisQolType;
                    }
                }
            }

            //If an error occurred then reset results.
            if (error)
            {
                foundAttribute = null;
                qolType = QolAttributeType.None;
            }

            return foundAttribute;
        }

        /// <summary>
        /// Modifies the specified method to use QolType.
        /// </summary>
        private void CreateAttributeMethod(MethodDefinition methodDef, CustomAttribute qolAttribute, QolAttributeType qolType)
        {
            bool inheritsNetworkBehaviour = methodDef.DeclaringType.InheritsNetworkBehaviour();

            TypeDefinition typeDef = methodDef.DeclaringType;
            ILProcessor processor = methodDef.Body.GetILProcessor();

            if (qolType == QolAttributeType.Client)
            {
                if (BuildInformation.RemoveClientLogic)
                {
                   
                }
                else
                {
                    LoggingType logging = qolAttribute.GetField("Logging", LoggingType.Warning);
                    /* Since isClient also uses insert first
                     * it will be put ahead of the IsOwner check, since the
                     * codegen processes it after IsOwner. EG... 
                     * IsOwner will be added first, then IsClient will be added first over IsOwner. */
                    bool requireOwnership = qolAttribute.GetField("RequireOwnership", false);
                    //If (!base.IsOwner);
                    if (requireOwnership)
                        CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(processor, logging, false, true);

                    if (BuildInformation.CheckIsClient)
                    {
                        CodegenSession.ObjectHelper.CreateIsClientCheck(processor, methodDef, logging, inheritsNetworkBehaviour, true);
                    }
                }
            }
            else if (qolType == QolAttributeType.Server)
            {
                if (BuildInformation.RemoveServerLogic)
                {
                   
                }
                else
                {
                    if (BuildInformation.CheckIsServer)
                    {
                        LoggingType logging = qolAttribute.GetField("Logging", LoggingType.Warning);
                        CodegenSession.ObjectHelper.CreateIsServerCheck(processor, methodDef, logging, !inheritsNetworkBehaviour, true);
                    }
                }

            }
        }

    }
}