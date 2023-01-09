using FishNet.Documenting;
using FishNet.Managing.Logging;
using System.Runtime.CompilerServices;
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
                _logging = ScriptableObject.CreateInstance<LevelLoggingConfiguration>();
            else
                _logging = _logging.Clone();

            _logging.InitializeOnce();
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
            return (nm == null) ? false : nm.CanLog(loggingType);
        }

        /// <summary>
        /// True if can log for loggingType.
        /// </summary>
        /// <param name="loggingType">Type of logging being filtered.</param>
        /// <returns></returns>
        public bool CanLog(LoggingType loggingType)
        {
            return _logging.CanLog(loggingType);
        }


        /// <summary>
        /// Performs a common log, should logging settings permit it.
        /// </summary>
        [APIExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StaticLog(string value) => InstanceFinder.NetworkManager?.Log(value);

        /// <summary>
        /// Performs a common log, should logging settings permit it.
        /// </summary>
        public void Log(string value)
        {
            _logging.Log(value);
        }

        /// <summary>
        /// Performs a log using the loggingType, should logging settings permit it.
        /// </summary>
        public void Log(LoggingType loggingType, string value)
        {
            if (loggingType == LoggingType.Common)
                _logging.Log(value);
            else if (loggingType == LoggingType.Warning)
                _logging.LogWarning(value);
            else if (loggingType == LoggingType.Error)
                _logging.LogError(value);
        }

        /// <summary>
        /// Performs a warning log, should logging settings permit it.
        /// </summary>
        /// 
        [APIExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StaticLogWarning(string value) => InstanceFinder.NetworkManager?.LogWarning(value);
        /// <summary>
        /// Performs a warning log, should logging settings permit it.
        /// </summary>
        public void LogWarning(string value)
        {
            _logging.LogWarning(value);
        }

        /// <summary>
        /// Performs an error log, should logging settings permit it.
        /// </summary>
        [APIExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StaticLogError(string value) => InstanceFinder.NetworkManager?.LogError(value);
        /// <summary>
        /// Performs an error log, should logging settings permit it.
        /// </summary>
        public void LogError(string value)
        {
            _logging.LogError(value);
        }

    }

}