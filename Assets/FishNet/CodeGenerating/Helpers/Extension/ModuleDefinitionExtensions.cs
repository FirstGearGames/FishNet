using FishNet.CodeGenerating.ILCore;
using MonoFN.Cecil;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace FishNet.CodeGenerating.Helping.Extension
{

    public static class ModuleDefinitionExtensions
    {

        /// <summary>
        /// Gets a class within CodegenSession.Module.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        public static TypeDefinition GetClass(this ModuleDefinition moduleDef, string className)
        {
            return CodegenSession.Module.GetType(FishNetILPP.RUNTIME_ASSEMBLY_NAME, className);
        }

        public static TypeReference ImportReference<T>(this ModuleDefinition moduleDef)
        {
            return CodegenSession.ImportReference(typeof(T));
        }

        public static MethodReference ImportReference(this ModuleDefinition moduleDef, Expression<Action> expression)
        {
            return ImportReference(moduleDef, (LambdaExpression)expression);
        }
        public static MethodReference ImportReference<T>(this ModuleDefinition module, Expression<Action<T>> expression)
        {
            return ImportReference(module, (LambdaExpression)expression);
        }

        public static MethodReference ImportReference(this ModuleDefinition module, LambdaExpression expression)
        {
            if (expression.Body is MethodCallExpression outermostExpression)
            {
                MethodInfo methodInfo = outermostExpression.Method;
                return module.ImportReference(methodInfo);
            }

            if (expression.Body is NewExpression newExpression)
            {
                ConstructorInfo methodInfo = newExpression.Constructor;
                // constructor is null when creating an ArraySegment<object>
                methodInfo = methodInfo ?? newExpression.Type.GetConstructors()[0];
                return module.ImportReference(methodInfo);
            }

            if (expression.Body is MemberExpression memberExpression)
            {
                var property = memberExpression.Member as PropertyInfo;
                return module.ImportReference(property.GetMethod);
            }

            throw new ArgumentException($"Invalid Expression {expression.Body.GetType()}");
        }


    }


}