using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing.Rpc;
using FishNet.Configuring;
using FishNet.Managing.Logging;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace FishNet.CodeGenerating.Processing
{
    internal class QolAttributeProcessor : CodegenBase
    {

        internal bool Process(TypeDefinition typeDef, bool moveStrippedCalls)
        {
            bool modified = false;
            List<MethodDefinition> methods = typeDef.Methods.ToList();

            //PROSTART
            if (moveStrippedCalls)
            {
                MoveStrippedCalls(methods);
                return true;
            }
            //PROEND

            foreach (MethodDefinition md in methods)
            {
                //Has RPC attribute, doesn't quality for a quality of life attribute.
                if (base.GetClass<RpcProcessor>().Attributes.HasRpcAttributes(md))
                    continue;

                QolAttributeType qolType;
                CustomAttribute qolAttribute = GetQOLAttribute(md, out qolType);
                if (qolAttribute == null)
                    continue;

                /* This is a one time check to make sure the qolType is
                 * a supported value. Multiple methods beyond this rely on the
                 * value being supported. Rather than check in each method a
                 * single check is performed here. */
                if (qolType != QolAttributeType.Server && qolType != QolAttributeType.Client)
                {
                    base.LogError($"QolAttributeType of {qolType.ToString()} is unhandled.");
                    continue;
                }

                CreateAttributeMethod(md, qolAttribute, qolType);
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
            //Nothing to check.
            if (methodDef == null || methodDef.CustomAttributes == null)
                return null;

            foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
            {
                QolAttributeType thisQolType = base.GetClass<AttributeHelper>().GetQolAttributeType(customAttribute.AttributeType.FullName);
                if (thisQolType != QolAttributeType.None)
                {
                    //A qol attribute already exist.
                    if (foundAttribute != null)
                    {
                        base.LogError($"{methodDef.Name} {thisQolType.ToString()} method cannot have multiple quality of life attributes.");
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
                        base.LogError($"{methodDef.Name} {thisQolType.ToString()} method cannot be abstract.");
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
            bool inheritsNetworkBehaviour = methodDef.DeclaringType.InheritsNetworkBehaviour(base.Session);

            //True to use InstanceFInder.
            bool useStatic = (methodDef.IsStatic || !inheritsNetworkBehaviour);

            if (qolType == QolAttributeType.Client)
            {
                if (!StripMethod(methodDef))
                {
                    LoggingType logging = qolAttribute.GetField("Logging", LoggingType.Warning);
                    /* Since isClient also uses insert first
                     * it will be put ahead of the IsOwner check, since the
                     * codegen processes it after IsOwner. EG... 
                     * IsOwner will be added first, then IsClient will be added first over IsOwner. */
                    bool requireOwnership = qolAttribute.GetField("RequireOwnership", false);
                    if (requireOwnership && useStatic)
                    {
                        base.LogError($"Method {methodDef.Name} has a [Client] attribute which requires ownership but the method may not use this attribute. Either the method is static, or the script does not inherit from NetworkBehaviour.");
                        return;
                    }
                    //If (!base.IsOwner);
                    if (requireOwnership)
                        base.GetClass<NetworkBehaviourHelper>().CreateLocalClientIsOwnerCheck(methodDef, logging, true, false, true);
                    //Otherwise normal IsClient check.
                    else
                        base.GetClass<NetworkBehaviourHelper>().CreateIsClientCheck(methodDef, logging, useStatic, true);
                }
            }
            else if (qolType == QolAttributeType.Server)
            {
                if (!StripMethod(methodDef))
                {
                    LoggingType logging = qolAttribute.GetField("Logging", LoggingType.Warning);
                    base.GetClass<NetworkBehaviourHelper>().CreateIsServerCheck(methodDef, logging, useStatic, true);
                }
            }

            bool StripMethod(MethodDefinition md)
            {
                //PROSTART
                bool removeLogic = (qolType == QolAttributeType.Server)
                    ? (CodeStripping.StripBuild && CodeStripping.ReleasingForClient)
                    : (CodeStripping.StripBuild && CodeStripping.ReleasingForServer);

                if (removeLogic)
                {
                    if (CodeStripping.StrippingType == StrippingTypes.Redirect)
                        md.DeclaringType.Methods.Remove(md);
                    else if (CodeStripping.StrippingType == StrippingTypes.Empty_Experimental)
                        md.ClearMethodWithRet(base.Session,md.Module);

                    return true;
                }
                //PROEND

                //Fall through.
                return false;
            }
        }

        //PROSTART
        /// <summary>
        /// Moves instructions when are calling a stripped method to a dummy method.
        /// </summary>
        /// <param name="methods"></param>
        private void MoveStrippedCalls(List<MethodDefinition> methods)
        {
            if (!CodeStripping.StripBuild)
                return;
            //Methods were emptied rather than moved.
            if (CodeStripping.StrippingType == StrippingTypes.Empty_Experimental)
                return;

            foreach (MethodDefinition md in methods)
            {
                //Went null at some point. It was likely stripped.
                if (md == null || md.Body == null || md.Body.Instructions == null)
                    continue;

                foreach (Instruction inst in md.Body.Instructions)
                {
                    //Calls a method.
                    if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt || inst.Operand == null)
                    {
                        //This shouldn't be possible but okay.
                        if (inst.Operand == null)
                            continue;

                        MethodDefinition targetMethod;
                        System.Type operandType = inst.Operand.GetType();
                        if (operandType == typeof(MethodDefinition))
                        {
                            targetMethod = (MethodDefinition)inst.Operand;
                        }
                        else if (operandType == typeof(MethodReference))
                        {
                            MethodReference mr = (MethodReference)inst.Operand;
                            targetMethod = mr.Resolve();
                        }
                        //Type isn't found, unable to remove call.
                        else
                        {
                            continue;
                        }
                        //Target method couldn't be looked up.
                        if (targetMethod == null)
                            continue;
                        GetQOLAttribute(targetMethod, out QolAttributeType qt);

                        bool redirectCall;
                        if (qt == QolAttributeType.Client)
                            redirectCall = (CodeStripping.StripBuild && CodeStripping.ReleasingForServer);
                        else if (qt == QolAttributeType.Server)
                            redirectCall = (CodeStripping.StripBuild && CodeStripping.ReleasingForClient);
                        else
                            redirectCall = false;

                        if (redirectCall)
                        {
                            if (md.Module != targetMethod.Module)
                            {
                                base.LogError($"{md.Name} in {md.DeclaringType.Name}/{md.Module.Name} calls method {targetMethod.Name} in {targetMethod.DeclaringType.Name}/{targetMethod.Module.Name}. Code stripping cannot work on client and server attributed methods when they are being called across assemblies. Use an accessor method within {targetMethod.DeclaringType.Name}/{targetMethod.Module.Name} to resolve this.");
                            }
                            else
                            {
                                MethodDefinition dummyMd = GetOrMakeDummyMethod(md, targetMethod);
                                targetMethod.Module.ImportReference(dummyMd);
                                md.Module.ImportReference(dummyMd);
                                inst.Operand = dummyMd;
                            }
                        }
                    }

                }

            }

            //Gets a dummy method in targetMd, or creates it should it not exist.
            MethodDefinition GetOrMakeDummyMethod(MethodDefinition callerMd, MethodDefinition targetMd)
            {
                string mdName = $"CallDummyMethod___{RpcProcessor.GetMethodNameAsParameters(targetMd)}";
                MethodDefinition result = targetMd.DeclaringType.GetMethod(mdName);
                if (result == null)
                {
                    TypeReference returnType;
                    //Is generic.
                    if (targetMd.ReturnType.IsGenericInstance)
                    {
                        TypeReference tr = targetMd.ReturnType;
                        GenericInstanceType git = (GenericInstanceType)tr;
                        returnType = base.ImportReference(git);
                    }
                    else
                    {
                        returnType = base.ImportReference(targetMd.ReturnType);
                    }

                    result = new MethodDefinition(mdName, targetMd.Attributes, returnType);
                    foreach (ParameterDefinition item in targetMd.Parameters)
                    {
                        base.ImportReference(item.ParameterType);
                        result.Parameters.Add(item);
                    }

                    targetMd.DeclaringType.Methods.Add(result);
                    result.ClearMethodWithRet(base.Session,callerMd.Module);
                    result.Body.InitLocals = true;
                }

                callerMd.Module.ImportReference(result);
                targetMd.Module.ImportReference(result);

                return result;
            }


        }
        //PROEND
    }

}
