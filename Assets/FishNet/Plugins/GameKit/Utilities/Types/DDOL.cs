using System;
using UnityEngine;

namespace GameKit.Utilities.Types
{


    public class DDOL : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Singleton instance of this class.
        /// </summary>
        [Obsolete("Use GetDDOL().")] //Remove on 2023/06/01.
        public static DDOL Instance => GetDDOL();
        /// <summary>
        /// Created instance of DDOL.
        /// </summary>
        private static DDOL _instance;
        #endregion

        /// <summary>
        /// Returns the current DDOL or creates one if not yet created.
        /// </summary>
        public static DDOL GetDDOL()
        {
            //Not yet made.
            if (_instance == null)
            {
                GameObject obj = new GameObject();
                obj.name = "FirstGearGames DDOL";
                DDOL ddol = obj.AddComponent<DDOL>();
                DontDestroyOnLoad(ddol);
                _instance = ddol;
                return ddol; 
            }
            //Already  made.
            else
            {
                return _instance;
            }
        }
    }


}