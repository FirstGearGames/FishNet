using FishNet.Documenting;
using FishNet.Managing.Logging;
using UnityEngine;

namespace FishNet.Managing
{
    public partial class NetworkManager : MonoBehaviour
    {
        #region Serialized.
        /// <summary>
        /// Logging configuration to use. When empty default logging settings will be used.
        /// </summary>
        [Tooltip("Logging configuration to use. When empty default logging settings will be used.")]
        [SerializeField]
        private LoggingConfiguration _logging;
        #endregion

        #region Const.
        private const string ERROR_LOGGING_PREFIX = "Error - ";
        private const string WARNING_LOGGING_PREFIX = "Warning - ";
        private const string COMMON_LOGGING_PREFIX = "Log - ";
        #endregion

        /// <summary>
        /// Initializes logging settings.
        /// </summary>
        private void InitializeLogging()
        {
            if (_logging == null)
                _logging = ScriptableObject.CreateInstance<LoggingConfiguration>();
            else
                _logging = _logging.Clone();

            _logging.InitializeOnceInternal();
        }


        /// <summary>
        /// True if can log for loggingType.
        /// </summary>
        /// <param name="loggingType"></param>
        /// <returns></returns>
        [APIExclude]
        public static bool StaticCanLog(LoggingType loggingType)
        {
            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null || !nm.CanLog(loggingType))
                return false;
            else
                return true;
        }

        /// <summary>
        /// True if can log for loggingType.
        /// </summary>
        /// <param name="loggingType">Type of logging being filtered.</param>
        /// <returns></returns>
        public bool CanLog(LoggingType loggingType)
        {
            if (_logging == null)
                return true;
            else
                return _logging.CanLog(loggingType);
        }

        /// <summary>
        /// Performs a common log, should logging settings permit it.
        /// </summary>
        /// <param name="o"></param>
        public void Log(string txt)
        {
            if (CanLog(LoggingType.Common))
            {
                Debug.Log(txt);
                WriteLog(LoggingType.Common, txt);
            }
        }

        /// <summary>
        /// Performs a warning log, should logging settings permit it.
        /// </summary>
        /// <param name="o"></param>
        public void LogWarning(string txt)
        {
            if (CanLog(LoggingType.Warning))
            {
                Debug.LogWarning(txt);
                WriteLog(LoggingType.Warning, txt);
            }
        }

        /// <summary>
        /// Performs an error log, should logging settings permit it.
        /// </summary>
        /// <param name="o"></param>
        public void LogError(string txt)
        {
            if (CanLog(LoggingType.Error))
            {
                Debug.LogError(txt);
                WriteLog(LoggingType.Error, txt);
            }
        }

        /// <summary>
        /// Writes log to file.
        /// </summary>
        /// <param name="txt"></param>
        private void WriteLog(LoggingType loggingType, string txt)
        {
            string prefix;
            if (loggingType == LoggingType.Common)
                prefix = COMMON_LOGGING_PREFIX;
            else if (loggingType == LoggingType.Warning)
                prefix = WARNING_LOGGING_PREFIX;
            else if (loggingType == LoggingType.Error)
                prefix = ERROR_LOGGING_PREFIX;
            else
                prefix = string.Empty;

        }
    }

}