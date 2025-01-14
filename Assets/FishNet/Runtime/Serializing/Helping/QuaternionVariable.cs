using System;
using FishNet.Managing;
using UnityEngine;

namespace FishNet.Serializing.Helping
{
    [System.Flags]
    public enum VariableDeltaQuaternionFlag : byte
    {
        Unset = 0,
        LargestIsX = (1 << 0),
        LargestIsY = (1 << 1),
        LargestIsZ = (1 << 2),
        //This flag can be discarded via refactor if we need it later.
        LargestIsW = (1 << 3),
        NextAIsLarger = (1 << 4),
        NextBIsLarger = (1 << 5),
        NextCIsLarger = (1 << 6),
        LargestIsNegative = (1 << 7),
    }

    public static class VariableDeltaQuaternionFlagExtensions
    {
        /// <summary>
        /// Returns if whole contains part.
        /// </summary>
        internal static bool FastContains(this VariableDeltaQuaternionFlag whole, VariableDeltaQuaternionFlag part) => (whole & part) == part;
    }

    public struct VariableOutput
    {
        public uint AWholeDifference;
        public uint BWholeDifference;
        public uint CWholeDifference;
        public uint DWholeDifference;

        public float AFloatDifference;
        public float BFloatDifference;
        public float CFloatDifference;
        public float DFloatDifference;

        public bool NextALarger;
        public bool NextBLarger;
        public bool NextCLarger;

        public bool LargestIsX;
        public bool LargestIsY;
        public bool LargestIsZ;
        public bool LargestIsW;

        public bool LargestIsNegative;
    }

    public static class QuaternionDeltaVariableCompression
    {
        /// <summary>
        /// Write a compressed a delta Quaternion using a variable precision.
        /// </summary>
        public static void Compress(Writer writer, Quaternion valueA, Quaternion valueB, out VariableOutput output, float precision = 0.001f)
        {
            output = new();

            uint multiplier = (uint)Mathf.RoundToInt(1f / precision);

            //Position where the next byte is to be written.
            int startPosition = writer.Position;
            //Skip one byte so the flags can be inserted after everything else is writteh.
            writer.Skip(1);

            VariableDeltaQuaternionFlag flags = VariableDeltaQuaternionFlag.Unset;
            long largestUValue = -1;

            /* This becomes true if the largest difference is negative on valueB.
             * EG: if Y is the largest and value.Y is < 0f then largestIsNegative becomes true. */
            bool largestIsNegative = false;

            /* Set next is larger values, and output differneces. */
            bool xIsLarger = GetNextIsLarger(valueA.x, valueB.x, multiplier, out uint xDifference);
            UpdateLargestValues(xDifference, valueB.x, VariableDeltaQuaternionFlag.LargestIsX);

            bool yIsLarger = GetNextIsLarger(valueA.y, valueB.y, multiplier, out uint yDifference);
            UpdateLargestValues(yDifference, valueB.y, VariableDeltaQuaternionFlag.LargestIsY);

            bool zIsLarger = GetNextIsLarger(valueA.z, valueB.z, multiplier, out uint zDifference);
            UpdateLargestValues(zDifference, valueB.z, VariableDeltaQuaternionFlag.LargestIsZ);

            bool wIsLarger = GetNextIsLarger(valueA.w, valueB.w, multiplier, out uint wDifference);
            UpdateLargestValues(wDifference, valueB.w, VariableDeltaQuaternionFlag.LargestIsW);

            //If flags are unset something went wrong. This should never be possible.
            if (flags == VariableDeltaQuaternionFlag.Unset)
            {
                //Write that flags are unset and error.
                writer.InsertUInt8Unpacked((byte)flags, startPosition);
                NetworkManagerExtensions.LogError($"Flags should not be unset.");
                return;
            }

            //Updates largest values and flags.
            void UpdateLargestValues(uint checkedValue, float fValue, VariableDeltaQuaternionFlag newFlag)
            {
                if (checkedValue > largestUValue)
                {
                    largestUValue = checkedValue;
                    flags = newFlag;
                    largestIsNegative = (fValue < 0f);
                }
            }

            /* Write all but largest. */

            //X is largest.
            if (flags == VariableDeltaQuaternionFlag.LargestIsX)
                WriteValues(yDifference, yIsLarger, zDifference, zIsLarger, wDifference, wIsLarger, ref output);
            //Y is largest.
            else if (flags == VariableDeltaQuaternionFlag.LargestIsY)
                WriteValues(xDifference, xIsLarger, zDifference, zIsLarger, wDifference, wIsLarger, ref output);
            //Z is largest.
            else if (flags == VariableDeltaQuaternionFlag.LargestIsZ)
                WriteValues(xDifference, xIsLarger, yDifference, yIsLarger, wDifference, wIsLarger, ref output);
            //W is largest.
            else if (flags == VariableDeltaQuaternionFlag.LargestIsW)
                WriteValues(xDifference, xIsLarger, yDifference, yIsLarger, zDifference, zIsLarger, ref output);

            /* This must be set after values are written since the enum
             * checks above use ==, rather than a bit comparer. */
            if (largestIsNegative)
                flags |= VariableDeltaQuaternionFlag.LargestIsNegative;

            void WriteValues(uint aValue, bool aIsLarger, uint bValue, bool bIsLarger, uint cValue, bool cIsLarger, ref VariableOutput lOutput)
            {
                writer.WriteUnsignedPackedWhole(aValue);
                if (aIsLarger)
                    flags |= VariableDeltaQuaternionFlag.NextAIsLarger;

                writer.WriteUnsignedPackedWhole(bValue);
                if (bIsLarger)
                    flags |= VariableDeltaQuaternionFlag.NextBIsLarger;

                writer.WriteUnsignedPackedWhole(cValue);
                if (cIsLarger)
                    flags |= VariableDeltaQuaternionFlag.NextCIsLarger;

                lOutput.AWholeDifference = aValue;
                lOutput.BWholeDifference = bValue;
                lOutput.CWholeDifference = cValue;

                lOutput.AFloatDifference = ((float)aValue / multiplier);
                lOutput.BFloatDifference = ((float)bValue / multiplier);
                lOutput.CFloatDifference = ((float)cValue / multiplier);

                lOutput.NextALarger = aIsLarger;
                lOutput.NextBLarger = bIsLarger;
                lOutput.NextCLarger = cIsLarger;
            }

            output.LargestIsX = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsX);
            output.LargestIsY = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsY);
            output.LargestIsZ = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsZ);
            output.LargestIsW = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsW);

            output.LargestIsNegative = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsNegative);

            //Insert flags.
            writer.InsertUInt8Unpacked((byte)flags, startPosition);
        }

        /// <summary>
        /// Write a compressed a delta Quaternion using a variable precision.
        /// </summary>
        public static Quaternion Decompress(Reader reader, Quaternion valueA, out VariableOutput output, float precision = 0.001f)
        {
            output = new();

            uint multiplier = (uint)Mathf.RoundToInt(1f / precision);

            VariableDeltaQuaternionFlag flags = (VariableDeltaQuaternionFlag)reader.ReadUInt8Unpacked();

            //Unset flags mean something went wrong in writing.
            if (flags == VariableDeltaQuaternionFlag.Unset)
            {
                NetworkManagerExtensions.LogError($"Unset flags were returned.");
                return default;
            }

            /* These values will be in order of X Y Z W.
             * Whichever value is the highest will be left out.
             *
             * EG: if Y was the highest then the following will be true...
             * a = X
             * b = Z
             * c = W */
            uint aWholeDifference = (uint)reader.ReadUnsignedPackedWhole();
            uint bWholeDifference = (uint)reader.ReadUnsignedPackedWhole();
            uint cWholeDifference = (uint)reader.ReadUnsignedPackedWhole();

            float aFloatDifference = (float)aWholeDifference / multiplier;
            float bFloatDifference = (float)bWholeDifference / multiplier;
            float cFloatDifference = (float)cWholeDifference / multiplier;

            output.AWholeDifference = aWholeDifference;
            output.BWholeDifference = bWholeDifference;
            output.CWholeDifference = cWholeDifference;

            output.AFloatDifference = aFloatDifference;
            output.BFloatDifference = bFloatDifference;
            output.CFloatDifference = cFloatDifference;

            output.NextALarger = flags.FastContains(VariableDeltaQuaternionFlag.NextAIsLarger);
            output.NextBLarger = flags.FastContains(VariableDeltaQuaternionFlag.NextBIsLarger);
            output.NextCLarger = flags.FastContains(VariableDeltaQuaternionFlag.NextCIsLarger);

            output.LargestIsX = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsX);
            output.LargestIsY = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsY);
            output.LargestIsZ = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsZ);
            output.LargestIsW = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsW);

            output.LargestIsNegative = flags.FastContains(VariableDeltaQuaternionFlag.LargestIsNegative);

            //Invert differences as needed so they can all be added onto the previous value as negative or positive.
            if (!flags.FastContains(VariableDeltaQuaternionFlag.NextAIsLarger))
                aFloatDifference *= -1f;
            if (!flags.FastContains(VariableDeltaQuaternionFlag.NextBIsLarger))
                bFloatDifference *= -1f;
            if (!flags.FastContains(VariableDeltaQuaternionFlag.NextCIsLarger))
                cFloatDifference *= -1f;

            float nextA;
            float nextB;
            float nextC;

            /* Add onto the previous value. */
            if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsX))
            {
                nextA = valueA.y + aFloatDifference;
                nextB = valueA.z + bFloatDifference;
                nextC = valueA.w + cFloatDifference;
            }
            else if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsY))
            {
                nextA = valueA.x + aFloatDifference;
                nextB = valueA.z + bFloatDifference;
                nextC = valueA.w + cFloatDifference;
            }
            else if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsZ))
            {
                nextA = valueA.x + aFloatDifference;
                nextB = valueA.z + bFloatDifference;
                nextC = valueA.w + cFloatDifference;
            }
            /* We do not really need the 'largest is W' since we know if
             * the other 3 are not the largest, then the remaining must be.
             * We have the available packing to use though, so use them
             * for now. */
            else if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsW))
            {
                nextA = valueA.x + aFloatDifference;
                nextB = valueA.y + bFloatDifference;
                nextC = valueA.z + cFloatDifference;
            }
            else
            {
                NetworkManagerExtensions.LogError($"Largest axes was not handled. Flags {flags}.");
                return default;
            }

            float abcMagnitude = GetMagnitude(nextA, nextB, nextC);

            float nextD = 1f - abcMagnitude;
            /* NextD should always be positive. But depending on precision
             * the calculated result could be negative due to missing decimals.
             * When negative make positive so nextD will normalize properly. */
            if (nextD < 0f)
                nextD *= -1f;

            nextD = (float)Math.Sqrt(nextD);
            
            //Get magnitude of all values.
            static float GetMagnitude(float a, float b, float c, float d = 0f) => (a * a + b * b + c * c + d * d);

            if (nextD >= 0f && flags.FastContains(VariableDeltaQuaternionFlag.LargestIsNegative))
                nextD *= -1f;

            if (!TryNormalize())
                return default;
            //Normalizes next values.
            bool TryNormalize()
            {
                float magnitude = (float)Math.Sqrt(GetMagnitude(nextA, nextB, nextC, nextD));
                if (magnitude < float.Epsilon)
                {
                    NetworkManagerExtensions.LogError($"Magnitude cannot be normalized.");
                    return false;
                }

                nextA /= magnitude;
                nextB /= magnitude;
                nextC /= magnitude;
                nextD /= magnitude;

                return true;
            }
            
            /* Add onto the previous value. */
            if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsX))
                return new Quaternion(nextD, nextA, nextB, nextC);
            else if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsY))
                return new Quaternion(nextA, nextD, nextB, nextC);
            else if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsZ))
                return new Quaternion(nextA, nextB, nextD, nextC);
            else if (flags.FastContains(VariableDeltaQuaternionFlag.LargestIsW))
                return new Quaternion(nextA, nextB, nextC, nextD);
            else
                NetworkManagerExtensions.LogError($"Unhandled Largest flag. Received flags are {flags}.");

            return default;
        }

        /// <summary>
        /// Returns if the next value is larger than the previous, and returns unsigned result with multiplier applied.
        /// </summary>
        private static bool GetNextIsLarger(float a, float b, uint lMultiplier, out uint multipliedUResult)
        {
            //Set is b is larger.
            bool bIsLarger = (b > a);

            //Get multiplied u value.
            float value = (bIsLarger) ? (b - a) : (a - b);
            multipliedUResult = (uint)Mathf.RoundToInt(value * lMultiplier);

            return bIsLarger;
        }
    }
}