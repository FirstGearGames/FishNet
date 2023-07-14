using UnityEngine;

namespace GameKit.Utilities.Types
{


    public class DDOL : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Singleton instance of this class.
        /// </summary>
        public static DDOL Instance { get; private set; }
        #endregion

        private void Awake()
        {
            FirstInitialize();
        }

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        private void FirstInitialize()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Multiple DDOL scripts found. There should be only one.");
                return;
            }
            else
            {
                Instance = this;
                gameObject.name = "FirstGearGames DDOL";
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// Returns the current DDOL or creates one if not yet created.
        /// </summary>
        public static DDOL GetDDOL()
        {
            //Not yet made.
            if (Instance == null)
            {
                GameObject obj = new GameObject();
                DDOL ddol = obj.AddComponent<DDOL>();
                return ddol;
            }
            //Already  made.
            else
            {
                return Instance;
            }
        }
    }


}