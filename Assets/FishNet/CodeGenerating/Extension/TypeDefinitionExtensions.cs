﻿using FishNet.CodeGenerating.Helping.Extension;
using MonoFN.Cecil;

namespace FishNet.CodeGenerating.Extension
{


    internal static class TypeDefinitionExtensions
    {
        public static MethodReference GetMethodReferenceInBase(this TypeDefinition td, CodegenSession session, string methodName)
        {
            MethodDefinition baseMd = td.GetMethodDefinitionInBase(session, methodName);
            if (baseMd == null)
                return null;


            MethodReference baseMr;
            TypeReference baseTr = td.BaseType;
            if (baseTr.CachedResolve(session).HasGenericParameters)
            {
                GenericInstanceType git = (GenericInstanceType)baseTr;
                baseMr = new MethodReference(baseMd.Name, baseMd.ReturnType, git)
                {
                    HasThis = baseMd.HasThis,
                    CallingConvention = baseMd.CallingConvention,
                    ExplicitThis = baseMd.ExplicitThis,
                };
                foreach (ParameterDefinition pd in baseMd.Parameters)
                {
                    session.ImportReference(pd.ParameterType);
                    baseMr.Parameters.Add(pd);
                }
            }
            else
            {
                baseMr = session.ImportReference(baseMd);
            }

            return baseMr;
        }
        /// <summary>
        /// Returns a method in the next base class.
        /// </summary>
        public static MethodDefinition GetMethodDefinitionInBase(this TypeDefinition td, CodegenSession session, string methodName)
        {
            if (td.BaseType == null)
            {
                session.LogError($"BaseType for {td.FullName} is null.");
                return null;
            }

            TypeDefinition baseTd = td.BaseType.CachedResolve(session);
            return baseTd.GetMethod(methodName);
        }


        /// <summary>
        /// Returns a method in the next base class.
        /// </summary>
        public static MethodReference GetMethodReference(this TypeDefinition td, CodegenSession session, string methodName)
        {
            MethodDefinition md = td.GetMethod(methodName);
            //Not found.
            if (md == null)
                return null;

            return md.GetMethodReference(session);
        }

        /// <summary>
        /// Gets a MethodReference or creates one if missing.
        /// </summary>
        public static MethodReference GetOrCreateMethodReference(this TypeDefinition td, CodegenSession session, string methodName, MethodAttributes attributes, TypeReference returnType, out bool created)
        {
            MethodDefinition md = td.GetMethod(methodName);
            //Not found.
            if (md == null)
            {
                md = new MethodDefinition(methodName, attributes, returnType);
                td.Methods.Add(md);
                created = true;
            }
            else
            {
                created = false;
            }

            return md.GetMethodReference(session);
        }


        /// <summary>
        /// Gets a MethodDefinition or creates one if missing.
        /// </summary>
        public static MethodDefinition GetOrCreateMethodDefinition(this TypeDefinition td, CodegenSession session, string methodName, MethodAttributes attributes, TypeReference returnType, out bool created)
        {
            MethodDefinition md = td.GetMethod(methodName);
            //Not found.
            if (md == null)
            {
                md = new MethodDefinition(methodName, attributes, returnType);
                td.Methods.Add(md);
                created = true;
            }
            else
            {
                created = false;
            }

            return md;
        }

        /// <summary>
        /// Gets a MethodDefinition or creates one if missing.
        /// </summary>
        public static MethodDefinition GetOrCreateMethodDefinition(this TypeDefinition td, CodegenSession session, string methodName, MethodDefinition methodTemplate, bool copyParameters, out bool created)
        {
            MethodDefinition md = td.GetMethod(methodName);
            //Not found.
            if (md == null)
            {
                TypeReference returnType = session.ImportReference(methodTemplate.ReturnType);
                md = new MethodDefinition(methodName, methodTemplate.Attributes, returnType)
                {
                    ExplicitThis = methodTemplate.ExplicitThis,
                    AggressiveInlining = methodTemplate.AggressiveInlining,
                    Attributes = methodTemplate.Attributes,
                    CallingConvention = methodTemplate.CallingConvention,
                    HasThis = methodTemplate.HasThis,
                };
                md.Body.InitLocals = methodTemplate.Body.InitLocals;

                if (copyParameters)
                {
                    foreach (ParameterDefinition pd in methodTemplate.Parameters)
                        md.Parameters.Add(pd);
                }

                td.Methods.Add(md);
                created = true;
            }
            else
            {
                created = false;
            }

            return md;
        }



        /// <summary>
        /// Returns a method in any inherited classes. The first found method is returned.
        /// </summary>
        public static MethodDefinition GetMethodDefinitionInAnyBase(this TypeDefinition td, CodegenSession session, string methodName)
        {
            while (td != null)
            {
                foreach (MethodDefinition md in td.Methods)
                {
                    if (md.Name == methodName)
                        return md;
                }

                try
                {
                    td = td.GetNextBaseTypeDefinition(session);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the next base type.
        /// </summary>
        public static TypeDefinition GetNextBaseTypeDefinition(this TypeDefinition typeDef, CodegenSession session)
        {
            return (typeDef.BaseType == null) ? null : typeDef.BaseType.CachedResolve(session);
        }

        /// <summary>
        /// Creates a FieldReference.
        /// </summary>
        public static FieldReference CreateFieldReference(this FieldDefinition fd, CodegenSession session)
        {
            FieldReference fr;
            TypeDefinition declaringType = fd.DeclaringType;
            //Is generic.
            if (declaringType.HasGenericParameters)
            {
                GenericInstanceType git = new GenericInstanceType(declaringType);
                foreach (GenericParameter item in declaringType.GenericParameters)
                    git.GenericArguments.Add(item);
                fr = new FieldReference(fd.Name, fd.FieldType, git);
                return fr;
            }
            //Not generic.
            else
            {
                return session.ImportReference(fd);
            }
        }

        /// <summary>
        /// Gets a FieldReference or creates it if missing.
        /// </summary>
        public static FieldReference GetOrCreateFieldReference(this TypeDefinition td, CodegenSession session, string fieldName, FieldAttributes attributes, TypeReference fieldTypeRef, out bool created)
        {
            FieldReference fr = td.GetFieldReference(fieldName, session);
            if (fr == null)
            {
                fr = td.CreateFieldDefinition(session, fieldName, attributes, fieldTypeRef);
                created = true;
            }
            else
            {
                created = false;
            }

            return fr;
        }

        /// <summary>
        /// Creates a FieldReference.
        /// </summary>
        public static FieldReference CreateFieldDefinition(this TypeDefinition td, CodegenSession session, string fieldName, FieldAttributes attributes, TypeReference fieldTypeRef)
        {
            FieldDefinition fd = new FieldDefinition(fieldName, attributes, fieldTypeRef);
            td.Fields.Add(fd);
            return fd.CreateFieldReference(session);
        }



    }


}