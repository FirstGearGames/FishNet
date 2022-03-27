using FishNet.Documenting;
using System;
using UnityEngine;

namespace FishNet.Managing.Logging
{

    /// <summary>
    /// Configuration ScriptableObject specifying which data to log. Used in conjuction with NetworkManager.
    /// </summary>
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
        private bool _initialized;
        /// <summary>
        /// Highest type which can be logged.
        /// </summary>
        private LoggingType _highestLoggingType = LoggingType.Off;
        #endregion

        [APIExclude]
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
        internal void InitializeOnceInternal()
        {
            byte currentHighest = (byte)LoggingType.Off;
#if UNITY_SERVER //if headless.
            currentHighest = Math.Max(currentHighest, (byte)_headlessLogging);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD //if editor or development.
            currentHighest = Math.Max(currentHighest, (byte)_developmentLogging);
#endif
#if !UNITY_EDITOR && !UNITY_SERVER //if a build.
            currentHighest = Math.Max(currentHighest, (byte)_guiLogging);
#endif
            _highestLoggingType = (LoggingType)currentHighest;
            _initialized = true;
        }

        /// <summary>
        /// True if can log for loggingType.
        /// </summary>
        /// <param name="loggingType">Type of logging being filtered.</param>
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
            return false;
            //return _writeLogs;
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