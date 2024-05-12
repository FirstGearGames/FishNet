#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.Documenting;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Logging
{

    /// <summary>
    /// Configuration ScriptableObject specifying which data to log. Used in conjuction with NetworkManager.
    /// </summary>
    [CreateAssetMenu(fileName = "New LevelLoggingConfiguration", menuName = "FishNet/Logging/Level Logging Configuration")]
    public class LevelLoggingConfiguration : LoggingConfiguration
    {

        #region Serialized.
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
            base.LoggingEnabled = loggingEnabled;
            _developmentLogging = development;
            _guiLogging = gui;
            _headlessLogging = headless;
        }

        /// <summary>
        /// Initializes script for use.
        /// </summary>
        /// <param name="manager"></param>
        public override void InitializeOnce()
        {
            byte currentHighest = (byte)LoggingType.Off;
#if UNITY_SERVER
            currentHighest = Math.Max(currentHighest, (byte)_headlessLogging);
#elif DEVELOPMENT
            currentHighest = Math.Max(currentHighest, (byte)_developmentLogging);
#else
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
        public override bool CanLog(LoggingType loggingType)
        {
            if (!base.LoggingEnabled)
                return false;

            if (!_initialized)
            {
#if DEVELOPMENT
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
        /// Logs a common value if can log.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Log(string value)
        {
            if (CanLog(LoggingType.Common))
                Debug.Log(value);
        }

        /// <summary>
        /// Logs a warning value if can log.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void LogWarning(string value)
        {
            if (CanLog(LoggingType.Warning))
                Debug.LogWarning(value);
        }

        /// <summary>
        /// Logs an error value if can log.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void LogError(string value)
        {
            if (CanLog(LoggingType.Error))
                Debug.LogError(value);
        }

        /// <summary>
        /// Clones this logging configuration.
        /// </summary>
        /// <returns></returns>
        public override LoggingConfiguration Clone()
        {
            LevelLoggingConfiguration copy = ScriptableObject.CreateInstance<LevelLoggingConfiguration>();
            copy.LoggingConstructor(base.LoggingEnabled, _developmentLogging, _guiLogging, _headlessLogging);
            return copy;
        }
    }
}