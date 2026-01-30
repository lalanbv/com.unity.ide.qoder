/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Microsoft.Unity.VisualStudio.Editor
{
    internal static class PackageInfoHelper
    {
        private const string DefaultDisplayName = "Qoder Editor";
        private const string DefaultVersion = "1.0.0";

        private static string s_displayName;
        private static string s_version;
        private static bool s_initialized;

        public static string DisplayName
        {
            get
            {
#if UNITY_EDITOR
                EnsureInitialized();
#endif
                return s_displayName;
            }
        }

        public static string Version
        {
            get
            {
#if UNITY_EDITOR
                EnsureInitialized();
#endif
                return s_version;
            }
        }
#if UNITY_EDITOR
        private static void EnsureInitialized()
        {
            if (s_initialized)
                return;

            s_initialized = true;
            s_displayName = DefaultDisplayName;
            s_version = DefaultVersion;

            try
            {
                var assembly = typeof(PackageInfoHelper).Assembly;
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
                if (packageInfo != null)
                {
                    s_displayName = packageInfo.displayName ?? DefaultDisplayName;
                    s_version = packageInfo.version ?? DefaultVersion;
                    Debug.Log($"[Qoder] Package info loaded via PackageManager: {s_displayName} v{s_version}");
                    return;
                }

                var packageJsonPath = FindPackageJson(assembly);
                if (!string.IsNullOrEmpty(packageJsonPath))
                {
                    Debug.Log($"[Qoder] Found package.json at: {packageJsonPath}");
                    ParsePackageJson(packageJsonPath);
                    Debug.Log($"[Qoder] Package info loaded via package.json: {s_displayName} v{s_version}");
                }
                else
                {
                    Debug.LogWarning("[Qoder] Could not find package.json, using default values");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Qoder] Failed to load package info: {ex.Message}");
            }
        }
#endif
        private static string FindPackageJson(System.Reflection.Assembly assembly)
        {
            string packageJsonPath = null;

            try
            {
                var assemblyPath = assembly.Location;
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    packageJsonPath = SearchUpward(Path.GetDirectoryName(assemblyPath));
                }
            }
            catch { }

            if (string.IsNullOrEmpty(packageJsonPath))
            {
                var scriptPath = GetScriptFilePath();
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    packageJsonPath = SearchUpward(Path.GetDirectoryName(scriptPath));
                }
            }

            return packageJsonPath;
        }

        private static string SearchUpward(string startDir)
        {
            var currentDir = startDir;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(currentDir); i++)
            {
                var candidate = Path.Combine(currentDir, "package.json");
                if (File.Exists(candidate))
                    return candidate;
                currentDir = Path.GetDirectoryName(currentDir);
            }

            return null;
        }

        private static void ParsePackageJson(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                var jsonNode = SimpleJSON.JSON.Parse(json);
                if (jsonNode != null)
                {
                    var displayName = jsonNode["displayName"];
                    if (displayName != null && !string.IsNullOrEmpty(displayName.Value))
                        s_displayName = displayName.Value;

                    var version = jsonNode["version"];
                    if (version != null && !string.IsNullOrEmpty(version.Value))
                        s_version = version.Value;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Qoder] Failed to parse package.json: {ex.Message}");
            }
        }

        private static string GetScriptFilePath([CallerFilePath] string path = "")
        {
            return path;
        }
    }
}
