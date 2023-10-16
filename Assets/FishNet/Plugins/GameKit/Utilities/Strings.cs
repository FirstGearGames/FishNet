
using System;

namespace GameKit.Utilities
{


    public static class Strings
    {
        /// <summary>
        /// Attachs or detaches an suffix to a string.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="suffix"></param>
        /// <param name="addExtension"></param>
        public static string ReturnModifySuffix(string text, string suffix, bool addExtension)
        {
            /* Since saving to a json, add the .json extension if not present.
             * Length must be greater than 6 to contain a character and .json. */
            if (text.Length > (suffix.Length + 1))
            {
                //If to add the extension.
                if (addExtension)
                {
                    //If doesn't contain the extension then add it on.
                    if (!text.Substring(text.Length - suffix.Length).Contains(suffix, StringComparison.CurrentCultureIgnoreCase))
                        return (text + suffix);
                    //Already contains extension.
                    else
                        return text;
                }
                //Remove extension.
                else
                {
                    //If contains extension.
                    if (text.Substring(text.Length - suffix.Length).Contains(suffix, StringComparison.CurrentCultureIgnoreCase))
                        return text.Substring(0, text.Length - (suffix.Length));
                    //Doesn't contain extension.
                    return text;
                }
            }
            //Text isn't long enough to manipulate.
            else
            {
                return text;
            }
        }

        /// <summary>
        /// Returns if a string contains another string using StringComparison.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="contains"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        public static bool Contains(this string s, string contains, StringComparison comp)
        {
            int index = s.IndexOf(contains, comp);
            return (index >= 0);
        }
    }


}