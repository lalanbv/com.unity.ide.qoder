/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Microsoft.Unity.VisualStudio.Editor
{
    internal static class FileUtility
    {
        public const char WinSeparator = '\\';
        public const char UnixSeparator = '/';

        private const string PackageName = "com.unity.ide.qoder";
        private static string _cachedPackageRootPath;
        private static bool _packagePathResolved;

        public static string GetAbsolutePath(string path)
        {
#if UNITY_6000_5_OR_NEWER
			return UnityEditor.FileUtil
				.PathToAbsolutePath(path)
				.NormalizePathSeparators();
#else
            return Path.GetFullPath(path);
#endif
        }

        public static string GetPackageAssetFullPath(params string[] components)
        {
            var packageRoot = GetPackageRootPath();
            return GetAbsolutePath(Path.Combine(packageRoot, Path.Combine(components)));
        }

        private static string GetPackageRootPath()
        {
            if (_packagePathResolved)
                return _cachedPackageRootPath;

            _cachedPackageRootPath = ResolvePackageRootPath();
            _packagePathResolved = true;
            return _cachedPackageRootPath;
        }

        private static string ResolvePackageRootPath()
        {
            string resolvedPath;

            // 策略1: 通过程序集位置获取（最可靠，适用于已编译 DLL）
            resolvedPath = TryGetPathFromAssemblyLocation();
            if (!string.IsNullOrEmpty(resolvedPath) && ValidatePackagePath(resolvedPath))
                return resolvedPath;

            // 策略2: 通过 AssetDatabase 查找 asmdef 文件（Editor 模式下可靠）
#if UNITY_EDITOR
            resolvedPath = TryGetPathFromAssetDatabase();
            if (!string.IsNullOrEmpty(resolvedPath) && ValidatePackagePath(resolvedPath))
                return resolvedPath;
#endif

            // 策略3: 回退到默认 Packages 路径
            resolvedPath = GetAbsolutePath(Path.Combine("Packages", PackageName));
            if (ValidatePackagePath(resolvedPath))
                return resolvedPath;

            // 策略4: 尝试 Assets 目录下的常见位置
            var assetsPath = GetAbsolutePath(Path.Combine("Assets", "ide", $"{PackageName}-master", "Editor"));
            if (Directory.Exists(assetsPath))
                return GetAbsolutePath(Path.Combine("Assets", "ide", $"{PackageName}-master"));

            Debug.LogWarning($"[{PackageName}] Unable to resolve package root path, using default Packages path.");
            return GetAbsolutePath(Path.Combine("Packages", PackageName));
        }

        private static string TryGetPathFromAssemblyLocation()
        {
            try
            {
                var assembly = typeof(FileUtility).Assembly;
                var assemblyLocation = assembly.Location;

                if (string.IsNullOrEmpty(assemblyLocation))
                    return null;

                // 程序集位于 Editor 子目录，需要获取父目录作为包根目录
                var editorDir = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrEmpty(editorDir))
                    return null;

                var dirName = Path.GetFileName(editorDir);
                if (string.Equals(dirName, "Editor", StringComparison.OrdinalIgnoreCase))
                    return Path.GetDirectoryName(editorDir);

                return editorDir;
            }
            catch (Exception)
            {
                return null;
            }
        }

#if UNITY_EDITOR
        private static string TryGetPathFromAssetDatabase()
        {
            try
            {
                var guids = AssetDatabase.FindAssets($"t:asmdef {PackageName}");
                if (guids == null || guids.Length == 0)
                    return null;

                foreach (var guid in guids)
                {
                    var asmdefPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(asmdefPath))
                        continue;

                    var fileName = Path.GetFileNameWithoutExtension(asmdefPath);
                    if (!string.Equals(fileName, PackageName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // asmdef 位于 Editor 目录，获取其父目录作为包根目录
                    var editorDir = Path.GetDirectoryName(asmdefPath);
                    if (string.IsNullOrEmpty(editorDir))
                        continue;

                    var dirName = Path.GetFileName(editorDir);
                    if (string.Equals(dirName, "Editor", StringComparison.OrdinalIgnoreCase))
                        return GetAbsolutePath(Path.GetDirectoryName(editorDir));

                    return GetAbsolutePath(editorDir);
                }
            }
            catch (Exception)
            {
                // Silently fail and try next strategy
            }

            return null;
        }
#endif

        private static bool ValidatePackagePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // 验证 Editor 子目录是否存在
            var editorPath = Path.Combine(path, "Editor");
            return Directory.Exists(editorPath);
        }

        public static string GetAssetFullPath(string asset)
        {
            var basePath = GetAbsolutePath(Path.Combine(Application.dataPath, ".."));
            return GetAbsolutePath(Path.Combine(basePath, NormalizePathSeparators(asset)));
        }

        public static string NormalizePathSeparators(this string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (Path.DirectorySeparatorChar == WinSeparator)
                path = path.Replace(UnixSeparator, WinSeparator);
            if (Path.DirectorySeparatorChar == UnixSeparator)
                path = path.Replace(WinSeparator, UnixSeparator);

            return path.Replace(string.Concat(WinSeparator, WinSeparator), WinSeparator.ToString());
        }

        public static string NormalizeWindowsToUnix(this string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return path.Replace(WinSeparator, UnixSeparator);
        }

        internal static bool IsFileInProjectRootDirectory(string fileName)
        {
            var relative = MakeRelativeToProjectPath(fileName);
            if (string.IsNullOrEmpty(relative))
                return false;

            return relative == Path.GetFileName(relative);
        }

        public static string MakeAbsolutePath(this string path)
        {
            if (string.IsNullOrEmpty(path)) { return string.Empty; }

            return Path.IsPathRooted(path) ? path : GetAbsolutePath(path);
        }

        // returns null if outside of the project scope
        internal static string MakeRelativeToProjectPath(string fileName)
        {
            var basePath = GetAbsolutePath(Path.Combine(Application.dataPath, ".."));
            fileName = NormalizePathSeparators(fileName);

            if (!Path.IsPathRooted(fileName))
                fileName = Path.Combine(basePath, fileName);

            if (!fileName.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return null;

            return fileName
                .Substring(basePath.Length)
                .Trim(Path.DirectorySeparatorChar);
        }
    }
}