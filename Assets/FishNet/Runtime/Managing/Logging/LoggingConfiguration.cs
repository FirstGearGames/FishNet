using System;
using UnityEngine;

namespace FishNet.Managing.Logging
{

    [CreateAssetMenu(fileName = "New LoggingConfiguration", menuName = "FishNet/Logging/Logging Configuration")]
    public class LoggingConfiguration : ScriptableObject
    {

        #region Serialized.
        /// <summary>
        /// True to use logging features. False to disable all logging.
        /// </summary>
        [Tooltip("True to use logging features. False to disable all logging.")]
        [SerializeField]
        private bool _loggingEnabled = true;
        /// <summary>
        /// True to write logs to disk. This only applies in builds.
        /// </summary>
        [Tooltip("True to write logs to disk. This only applies in builds.")]
        [SerializeField]
        private bool _writeLogs = false;
        /// <summary>
        /// Type of logging to use for development builds and editor.
        /// </summary>
        [Tooltip("Type of logging to use for development builds and editor.")]
        [SerializeField]
        private LoggingType _developmentLogging = LoggingType.Common;
        /// <summary>
        /// Type of logging to use for GUI builds.
        /// </summary>
        [Tooltip("Type of logging to use for GUI builds.")]
        [SerializeField]
        private LoggingType _guiLogging = LoggingType.Warning;
        /// <summary>
        /// Type of logging to use for headless builds.
        /// </summary>
        [Tooltip("Type of logging to use for headless builds.")]
        [SerializeField]
        private LoggingType _headlessLogging = LoggingType.Error;
        #endregion

        #region Private.
        /// <summary>
        /// True when initialized.
        /// </summary>
        private bool _initialized = false;
        /// <summary>
        /// Highest type which can be logged.
        /// </summary>
        private LoggingType _highestLoggingType = LoggingType.Off;
        #endregion

        public void LoggingConstructor(bool loggingEnabled, LoggingType development, LoggingType gui, LoggingType headless)
        {
            _loggingEnabled = loggingEnabled;
            _developmentLogging = development;
            _guiLogging = gui;
            _headlessLogging = headless;
        }

        /// <summary>
        /// Initializes script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void FirstInitialize()
        {
            byte currentHighest = (byte)LoggingType.Off;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            currentHighest = Math.Max(currentHighest, (byte)_developmentLogging);
#endif
#if !UNITY_EDITOR && UNITY_SERVER
            currentHighest = Math.Max(currentHighest, (byte)_headlessLogging);
#endif
#if !UNITY_EDITOR && UNITY_SERVER
            currentHighest = Math.Max(currentHighest, (byte)_guiLogging);
#endif
            _highestLoggingType = (LoggingType)currentHighest;
            _initialized = true;
        }

        /// <summary>
        /// Returns true if logging is possible.
        /// </summary>
        /// <param name="loggingType"></param>
        /// <returns></returns>
        public bool CanLog(LoggingType loggingType)
        {
            if (!_loggingEnabled)
                return false;

            if (!_initialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Application.isPlaying)
                    Debug.LogError("CanLog called before being initialized.");
                else
                    return true;
#endif
                return false; 
            }

            return ((byte)loggingType <= (byte)_highestLoggingType);                
        }

        /// <summary>
        /// Returns if logging can be written to disk.
        /// </summary>
        /// <returns></returns>
        internal bool CanWrite()
        {
            return _writeLogs;
        }

        /// <summary>
        /// Clones this logging configuration.
        /// </summary>
        /// <returns></returns>
        internal LoggingConfiguration Clone()
        {
            LoggingConfiguration copy = ScriptableObject.CreateInstance<LoggingConfiguration>();
            copy.LoggingConstructor(_loggingEnabled, _developmentLogging, _guiLogging, _headlessLogging);
            return copy;
        }
    }
}