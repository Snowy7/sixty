using UnityEngine;
using Ia.Core.Debugging;

namespace Ia.Core.Config
{
    /// <summary>
    /// Singleton-style access to IaGlobalSettings.
    /// Looks up or auto-creates an asset in editor.
    /// </summary>
    public static class IaGlobal
    {
        private const string ResourcesPath = "IaGlobalSettings";
        private static IaGlobalSettings _settings;

        public static IaGlobalSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = Resources.Load<IaGlobalSettings>(ResourcesPath);
#if UNITY_EDITOR
                    if (_settings == null)
                    {
                        // Try harder in editor: search asset database
                        string[] guids = UnityEditor.AssetDatabase.FindAssets(
                            "t:IaGlobalSettings"
                        );
                        if (guids != null && guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(
                                guids[0]
                            );
                            _settings = UnityEditor.AssetDatabase.LoadAssetAtPath<
                                IaGlobalSettings
                            >(path);
                        }
                    }
#endif
                    if (_settings != null && _settings.logSettings != null)
                    {
                        IaLogger.Initialize(_settings.logSettings);
                    }
                }

                return _settings;
            }
        }
    }
}