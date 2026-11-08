using UnityEngine;

namespace NanoraUnityMCP.Core
{
    /// <summary>
    /// Compatibility layer for Unity API differences across versions.
    /// Unity 2021.3 does not have FindFirstObjectByType / FindObjectsByType
    /// or the FindObjectsSortMode / FindObjectsInactive enums.
    /// </summary>
    public static class UnityCompatibility
    {
        /// <summary>
        /// Replacement for Object.FindFirstObjectByType&lt;T&gt;()
        /// Falls back to Object.FindObjectOfType&lt;T&gt;() on Unity 2021.3.
        /// </summary>
        public static T FindFirstObjectByType<T>() where T : Object
        {
#if UNITY_2022_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        /// <summary>
        /// Replacement for Object.FindFirstObjectByType(type)
        /// Falls back to Object.FindObjectOfType(type) on Unity 2021.3.
        /// </summary>
        public static Object FindFirstObjectByType(System.Type type)
        {
#if UNITY_2022_1_OR_NEWER
            return Object.FindFirstObjectByType(type);
#else
            return Object.FindObjectOfType(type);
#endif
        }

        /// <summary>
        /// Replacement for Object.FindObjectsByType&lt;T&gt;(FindObjectsSortMode.None)
        /// Falls back to Object.FindObjectsOfType&lt;T&gt;() on Unity 2021.3.
        /// </summary>
        public static T[] FindObjectsByType<T>() where T : Object
        {
#if UNITY_2022_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        /// <summary>
        /// Replacement for Object.FindObjectsByType(type, FindObjectsSortMode.None)
        /// Falls back to Object.FindObjectsOfType(type) on Unity 2021.3.
        /// </summary>
        public static Object[] FindObjectsByType(System.Type type)
        {
#if UNITY_2022_1_OR_NEWER
            return Object.FindObjectsByType(type, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType(type);
#endif
        }

        /// <summary>
        /// Replacement for Object.FindObjectsByType(type, FindObjectsInactive, FindObjectsSortMode.None)
        /// Falls back to Object.FindObjectsOfType(type, includeInactive) on Unity 2021.3.
        /// </summary>
        public static Object[] FindObjectsByType(System.Type type, bool includeInactive)
        {
#if UNITY_2022_1_OR_NEWER
            return Object.FindObjectsByType(type,
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType(type, includeInactive);
#endif
        }

        /// <summary>
        /// Replacement for Object.FindAnyObjectByType(type): returns any loaded instance.
        /// Unity 6000.6 deprecates FindFirstObjectByType in favour of this order-independent call.
        /// Falls back to Object.FindObjectOfType(type) on Unity 2021.3.
        /// </summary>
        public static Object FindAnyObjectByType(System.Type type)
        {
#if UNITY_2022_1_OR_NEWER
            return Object.FindAnyObjectByType(type);
#else
            return Object.FindObjectOfType(type);
#endif
        }
    }
}
