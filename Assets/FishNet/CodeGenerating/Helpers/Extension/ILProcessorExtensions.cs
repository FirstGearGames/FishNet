using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping.Extension
{

    public static class ILProcessorExtensions
    {
        /// <summary>
        /// Inserts instructions at the beginning.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="instructions"></param>
        public static void InsertFirst(this ILProcessor processor, List<Instruction> instructions)
        {
            for (int i = 0; i < instructions.Count; i++)
                processor.Body.Instructions.Insert(i, instructions[i]);
        }

        /// <summary>
        /// Inserts instructions at the end while also moving Ret down.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="instructions"></param>
        public static void InsertLast(this ILProcessor processor, List<Instruction> instructions)
        {
            bool retRemoved = false;
            int startingCount = processor.Body.Instructions.Count;
            //Remove ret if it exist and add it back in later.
            if (startingCount > 0)
            {
                if (processor.Body.Instructions[startingCount - 1].OpCode == OpCodes.Ret)
                {
                    processor.Body.Instructions.RemoveAt(startingCount - 1);
                    retRemoved = true;
                }
            }

            foreach (Instruction inst in instructions)
                processor.Append(inst);

            //Add ret back if it was removed.
            if (retRemoved)
                processor.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Inserts instructions before target.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="instructions"></param>
        public static void InsertBefore(this ILProcessor processor,Instruction target, List<Instruction> instructions)
        {
            int index = processor.Body.Instructions.IndexOf(target);
            for (int i = 0; i < instructions.Count; i++)
                processor.Body.Instructions.Insert(index + i, instructions[i]);
        }

        /// <summary>
        /// Adds instructions to the end of processor.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="instructions"></param>
        public static void Add(this ILProcessor processor, List<Instruction> instructions)
        {
            for (int i = 0; i < instructions.Count; i++)
                processor.Body.Instructions.Add(instructions[i]);
        }

        /// <summary>
        /// Inserts instructions before returns. Only works on void types.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="instructions"></param>
        public static void InsertBeforeReturns(this ILProcessor processor, List<Instruction> instructions)
        {
            if (processor.Body.Method.ReturnType != CodegenSession.Module.TypeSystem.Void)
            {
                CodegenSession.LogError($"Cannot insert instructions before returns on {processor.Body.Method.FullName} because it does not return void.");
                return;
            }

            /* Insert at the end of the method
             * and get the first instruction that was inserted.
             * Any returns or breaks which would exit the method
             * will jump to this instruction instead. */
            processor.InsertLast(instructions);
            Instruction startInst = processor.Body.Instructions[processor.Body.Instructions.Count - instructions.Count];

            //Look for anything that jumps to rets.
            for (int i = 0; i < processor.Body.Instructions.Count; i++)
            {
                Instruction inst = processor.Body.Instructions[i];
                if (inst.Operand is Instruction operInst)
                {
                    if (operInst.OpCode == OpCodes.Ret)
                        inst.Operand = startInst;
                }
            }
        }
    }


}