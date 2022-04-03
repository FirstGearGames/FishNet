using UnityEngine;

namespace FishNet.Utility
{


    public class DDOLFinder : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Singleton instance of this class.
        /// </summary>
        public static DDOLFinder Instance { get; private set; }
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
                gameObject.name = "DDOLFinder";
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// Returns the current DDOL or creates one if not yet created.
        /// </summary>
        public static DDOLFinder GetDDOL()
        {
            //Not yet made.
            if (Instance == null)
            {
                GameObject obj = new GameObject();
                DDOLFinder ddol = obj.AddComponent<DDOLFinder>();
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