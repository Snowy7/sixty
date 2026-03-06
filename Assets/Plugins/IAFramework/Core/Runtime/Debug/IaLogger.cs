using Ia.Core.Update;
using UnityEngine;

namespace Ia.Core.Debugging
{
    /// <summary>
    /// Central logging for I.A Framework, with category + level filtering.
    /// </summary>
    public static class IaLogger
    {
        private static IaLogSettings m_settings;

        public static void Initialize(IaLogSettings settings)
        {
            m_settings = settings;
        }

        private static bool ShouldLog(IaLogCategory category, IaLogLevel level)
        {
            if (m_settings == null)
            {
                // If not configured, log everything in editor, errors only in builds.
#if UNITY_EDITOR
                return true;
#else
                return level <= IaLogLevel.Error;
#endif
            }

            if (!m_settings.enableLogging)
                return false;

            if (level > m_settings.minimumLogLevel)
                return false;

            if (!m_settings.IsCategoryEnabled(category))
                return false;

            return true;
        }

        public static void Info(
            IaLogCategory category,
            object message,
            Object context = null
        )
        {
            if (!ShouldLog(category, IaLogLevel.Info))
                return;

#if UNITY_EDITOR
            Debug.Log(FormatMessage("INFO", category, message), context);
#else
            Debug.Log(FormatMessage("INFO", category, message));
#endif
        }
        
        public static void Info(
            IaBehaviour behaviour,
            object message
        )
        {
            IaLogCategory cat = GetCategoryFromBehaviour(behaviour);
            Info(
                cat,
                $"[{behaviour.name}] {message}",
                behaviour
            );
        }

        public static void Warning(
            IaLogCategory category,
            object message,
            Object context = null
        )
        {
            if (!ShouldLog(category, IaLogLevel.Warning))
                return;

#if UNITY_EDITOR
            Debug.LogWarning(FormatMessage("WARN", category, message), context);
#else
            Debug.LogWarning(FormatMessage("WARN", category, message));
#endif
        }

        public static void Warning(
            IaBehaviour behaviour,
            object message
        )
        {
            IaLogCategory cat = GetCategoryFromBehaviour(behaviour);
            Warning(
                cat,
                $"[{behaviour.name}] {message}",
                behaviour
            );
        }

        public static void Error(
            IaLogCategory category,
            object message,
            Object context = null
        )
        {
            if (!ShouldLog(category, IaLogLevel.Error))
                return;

#if UNITY_EDITOR
            Debug.LogError(FormatMessage("ERR ", category, message), context);
#else
            Debug.LogError(FormatMessage("ERR ", category, message));
#endif
        }
        
        public static void Error(
            IaBehaviour behaviour,
            object message
        )
        {
            IaLogCategory cat = GetCategoryFromBehaviour(behaviour);
            Error(
                cat,
                $"[{behaviour.name}] {message}",
                behaviour
            );
        }

        public static void Verbose(
            IaLogCategory category,
            object message,
            Object context = null
        )
        {
            if (!ShouldLog(category, IaLogLevel.Verbose))
                return;

#if UNITY_EDITOR
            Debug.Log(FormatMessage("VERB", category, message), context);
#endif
        }
        
        public static void Verbose(
            IaBehaviour behaviour,
            object message
        )
        {
            IaLogCategory cat = GetCategoryFromBehaviour(behaviour);
            Verbose(
                cat,
                $"[{behaviour.name}] {message}",
                behaviour
            );
        }

        private static string FormatMessage(
            string level,
            IaLogCategory category,
            object message
        )
        {
            return $"[I.A][{level}][{category}] {message}";
        }
        
        private static IaLogCategory GetCategoryFromBehaviour(IaBehaviour behaviour)
        {
            // For now, all IaBehaviour logs go under Update category.
            return IaLogCategory.Update;
        }
    }
}