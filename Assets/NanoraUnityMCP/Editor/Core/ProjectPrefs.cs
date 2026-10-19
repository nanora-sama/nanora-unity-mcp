using UnityEditor;
using UnityEngine;

namespace NanoraUnityMCP.Core
{
    /// <summary>
    /// Provides project-specific EditorPrefs keys.
    /// EditorPrefs is global to the Unity Editor installation, so this utility
    /// appends a hash of the project path to make keys unique per project.
    /// </summary>
    public static class ProjectPrefs
    {
        private static string s_ProjectHash;

        /// <summary>
        /// Returns a stable hash suffix derived from the current project's data path.
        /// </summary>
        private static string ProjectHash
        {
            get
            {
                if (s_ProjectHash == null)
                {
                    // Application.dataPath is unique per project (e.g. "C:/Projects/MyGame/Assets")
                    s_ProjectHash = Application.dataPath.GetHashCode().ToString("X8");
                }
                return s_ProjectHash;
            }
        }

        /// <summary>
        /// Converts a base EditorPrefs key into a project-specific key.
        /// Example: "UnityMCP_Port" → "UnityMCP_Port_A1B2C3D4"
        /// </summary>
        public static string Key(string baseKey)
        {
            return $"{baseKey}_{ProjectHash}";
        }

        public static bool GetBool(string baseKey, bool defaultValue = false)
        {
            return EditorPrefs.GetBool(Key(baseKey), defaultValue);
        }

        public static void SetBool(string baseKey, bool value)
        {
            EditorPrefs.SetBool(Key(baseKey), value);
        }

        public static int GetInt(string baseKey, int defaultValue = 0)
        {
            return EditorPrefs.GetInt(Key(baseKey), defaultValue);
        }

        public static void SetInt(string baseKey, int value)
        {
            EditorPrefs.SetInt(Key(baseKey), value);
        }

        public static string GetString(string baseKey, string defaultValue = "")
        {
            return EditorPrefs.GetString(Key(baseKey), defaultValue);
        }

        public static void SetString(string baseKey, string value)
        {
            EditorPrefs.SetString(Key(baseKey), value);
        }

        public static void DeleteKey(string baseKey)
        {
            EditorPrefs.DeleteKey(Key(baseKey));
        }
    }
}
