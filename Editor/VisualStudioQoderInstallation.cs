/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using SimpleJSON;
using IOPath = System.IO.Path;
using Debug = UnityEngine.Debug;

namespace Microsoft.Unity.VisualStudio.Editor
{
    internal class VisualStudioQoderInstallation : VisualStudioInstallation
    {
        private static readonly IGenerator _generator = GeneratorFactory.GetInstance(GeneratorStyle.SDK);

        internal const string ReuseExistingWindowKey = "qoder_reuse_existing_window";
        private const string MicrosoftUnityExtensionId = "visualstudiotoolsforunity.vstuc";

        public override bool SupportsAnalyzers => true;

        public override Version LatestLanguageVersionSupported => new Version(13, 0);

        private string GetExtensionPath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var searchPaths = new[]
            {
                IOPath.Combine(userProfile, ".qoder", "extensions"),
                IOPath.Combine(userProfile, ".vscode", "extensions"),
                IOPath.Combine(userProfile, ".vscode-insiders", "extensions"),
            };

            foreach (var extensionsPath in searchPaths)
            {
                if (!Directory.Exists(extensionsPath))
                    continue;

                var found = Directory
                    .EnumerateDirectories(extensionsPath, $"{MicrosoftUnityExtensionId}*")
                    .OrderByDescending(n => n)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(found))
                    return found;
            }

            return null;
        }

        public override string[] GetAnalyzers()
        {
            var extensionPath = GetExtensionPath();
            if (string.IsNullOrEmpty(extensionPath))
                return Array.Empty<string>();

            return GetAnalyzers(extensionPath);
        }

        public override IGenerator ProjectGenerator => _generator;

        private static bool IsCandidateForDiscovery(string path)
        {
#if UNITY_EDITOR_OSX
			return Directory.Exists(path) && Regex.IsMatch(path, ".*[Qq]oder.*.app$", RegexOptions.IgnoreCase);
#elif UNITY_EDITOR_WIN
            return File.Exists(path) && Regex.IsMatch(path, ".*[Qq]oder.*.exe$", RegexOptions.IgnoreCase);
#else
			return File.Exists(path) && Regex.IsMatch(path, ".*[Qq]oder$", RegexOptions.IgnoreCase);
#endif
        }

        [Serializable]
        internal class QoderManifest
        {
            public string name;
            public string version;
        }

        public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
        {
            installation = null;

            if (string.IsNullOrEmpty(editorPath))
                return false;

            if (!IsCandidateForDiscovery(editorPath))
                return false;

            Version version = null;
            var isPrerelease = false;

            try
            {
                var manifestBase = GetRealPath(editorPath);

#if UNITY_EDITOR_WIN
                manifestBase = IOPath.GetDirectoryName(manifestBase);
#elif UNITY_EDITOR_OSX
				manifestBase = IOPath.Combine(manifestBase, "Contents");
#else
				var parent = Directory.GetParent(manifestBase);
				manifestBase = parent?.Name == "bin" ? parent.Parent?.FullName : parent?.FullName;
#endif

                if (manifestBase == null)
                    return false;

                var manifestFullPath = IOPath.Combine(manifestBase, "resources", "app", "package.json");
                if (File.Exists(manifestFullPath))
                {
                    var manifest = JsonUtility.FromJson<QoderManifest>(File.ReadAllText(manifestFullPath));
                    Version.TryParse(manifest.version.Split('-').First(), out version);
                    isPrerelease = manifest.version.ToLower().Contains("insider") || manifest.version.ToLower().Contains("beta");
                }
            }
            catch (Exception)
            {
            }

            isPrerelease = isPrerelease || editorPath.ToLower().Contains("insider") || editorPath.ToLower().Contains("beta");
            installation = new VisualStudioQoderInstallation()
            {
                IsPrerelease = isPrerelease,
                Name = "Qoder" + (isPrerelease ? " - Preview" : string.Empty) + (version != null ? $" [{version.ToString(3)}]" : string.Empty),
                Path = editorPath,
                Version = version ?? new Version()
            };

            return true;
        }

        public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
        {
            var candidates = new List<string>();

#if UNITY_EDITOR_WIN
            var localAppPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            foreach (var basePath in new[] { localAppPath, programFiles })
            {
                candidates.Add(IOPath.Combine(basePath, "Qoder", "Qoder.exe"));
                candidates.Add(IOPath.Combine(basePath, "qoder", "qoder.exe"));
            }

            candidates.Add(IOPath.Combine(userProfile, ".qoder", "Qoder.exe"));
            candidates.Add(IOPath.Combine(appData, "Qoder", "Qoder.exe"));

#elif UNITY_EDITOR_OSX
			candidates.Add("/Applications/Qoder.app");
			candidates.Add(IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Qoder.app"));
			candidates.Add("/usr/local/bin/qoder");

#elif UNITY_EDITOR_LINUX
			candidates.Add("/usr/bin/qoder");
			candidates.Add("/bin/qoder");
			candidates.Add("/usr/local/bin/qoder");
			candidates.Add(IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qoder", "bin", "qoder"));
			candidates.Add("/snap/bin/qoder");
			candidates.AddRange(GetXdgCandidates());
#endif

            foreach (var candidate in candidates.Distinct())
            {
                if (TryDiscoverInstallation(candidate, out var installation))
                    yield return installation;
            }
        }

#if UNITY_EDITOR_LINUX
private static readonly Regex DesktopFileExecEntry = new Regex(@"Exec=(\S+)", RegexOptions.Singleline | RegexOptions.Compiled);

private static IEnumerable<string> GetXdgCandidates()
{
    // 首先检查用户本地的 applications 目录
    var userAppsDir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");
    if (Directory.Exists(userAppsDir))
    {
        var userDesktopFile = IOPath.Combine(userAppsDir, "qoder.desktop");
        if (File.Exists(userDesktopFile))
        {
            var exec = TryGetExecFromDesktopFile(userDesktopFile);
            if (!string.IsNullOrEmpty(exec))
                yield return exec;
        }
    }

    // 然后检查 XDG_DATA_DIRS
    var envdirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
    if (string.IsNullOrEmpty(envdirs))
        envdirs = "/usr/local/share:/usr/share"; // 默认值

    var dirs = envdirs.Split(':');
    foreach (var dir in dirs)
    {
        var desktopFile = IOPath.Combine(dir, "applications", "qoder.desktop");
        var exec = TryGetExecFromDesktopFile(desktopFile);
        if (!string.IsNullOrEmpty(exec))
        {
            yield return exec;
            break;
        }
    }
}

private static string TryGetExecFromDesktopFile(string desktopFile)
{
    try
    {
        if (!File.Exists(desktopFile))
            return null;

        var content = File.ReadAllText(desktopFile);
        var match = DesktopFileExecEntry.Match(content);
        if (match.Success)
            return match.Groups[1].Value;
    }
    catch
    {
    }
    return null;
}
#endif

#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
        [System.Runtime.InteropServices.DllImport("libc")]
        private static extern int readlink(string path, byte[] buffer, int buflen);

        internal static string GetRealPath(string path)
        {
            byte[] buf = new byte[512];
            int ret = readlink(path, buf, buf.Length);
            if (ret == -1) return path;
            char[] cbuf = new char[512];
            int chars = System.Text.Encoding.Default.GetChars(buf, 0, ret, cbuf, 0);
            return new String(cbuf, 0, chars);
        }
#else
        internal static string GetRealPath(string path)
        {
            return path;
        }
#endif

        #region Extra Files Creation

        public override void CreateExtraFiles(string projectDirectory)
        {
            try
            {
                var qoderDirectory = IOPath.Combine(projectDirectory.NormalizePathSeparators(), ".qoder");
                var vscodeDirectory = IOPath.Combine(projectDirectory.NormalizePathSeparators(), ".vscode");

                Directory.CreateDirectory(qoderDirectory);

                var enablePatch = !File.Exists(IOPath.Combine(qoderDirectory, ".qoderpatchdisable"));

                // .qoder 目录下的配置文件
                CreateRecommendedExtensionsFile(qoderDirectory, enablePatch);
                CreateSettingsFile(qoderDirectory, enablePatch);
                CreateLaunchFile(qoderDirectory, enablePatch);
                CreateTasksFile(qoderDirectory, enablePatch);

                // 项目根目录下的配置文件
                CreateEditorConfigFile(projectDirectory);
                CreateOmniSharpConfigFile(projectDirectory);

                // 如果存在 .vscode 目录，同步更新关键配置以保持兼容性
                if (Directory.Exists(vscodeDirectory))
                {
                    var vscodeEnablePatch = !File.Exists(IOPath.Combine(vscodeDirectory, ".qoderpatchdisable"));
                    if (vscodeEnablePatch)
                    {
                        PatchLaunchFile(IOPath.Combine(vscodeDirectory, "launch.json"));
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[Qoder] Failed to create extra files: {ex.Message}");
            }
        }

        private const string DefaultLaunchFileContent = @"{
    ""version"": ""0.2.0"",
    ""configurations"": [
        {
            ""name"": ""Attach to Unity"",
            ""type"": ""vstuc"",
            ""request"": ""attach""
        },
        {
            ""name"": ""Unity Editor"",
            ""type"": ""unity"",
            ""request"": ""launch""
        }
    ]
}";

        private static void CreateLaunchFile(string qoderDirectory, bool enablePatch)
        {
            var launchFile = IOPath.Combine(qoderDirectory, "launch.json");
            if (File.Exists(launchFile))
            {
                if (enablePatch)
                    PatchLaunchFile(launchFile);
                return;
            }

            File.WriteAllText(launchFile, DefaultLaunchFileContent);
        }

        private static void PatchLaunchFile(string launchFile)
        {
            try
            {
                const string configurationsKey = "configurations";
                const string typeKey = "type";

                var content = File.ReadAllText(launchFile);
                var launch = JSONNode.Parse(content);

                var configurations = launch[configurationsKey] as JSONArray;
                if (configurations == null)
                {
                    configurations = new JSONArray();
                    launch.Add(configurationsKey, configurations);
                }

                if (configurations.Linq.Any(entry => entry.Value[typeKey].Value == "vstuc"))
                    return;

                var defaultContent = JSONNode.Parse(DefaultLaunchFileContent);
                configurations.Add(defaultContent[configurationsKey][0]);

                WriteAllTextFromJObject(launchFile, launch);
            }
            catch (Exception)
            {
            }
        }

        private void CreateSettingsFile(string qoderDirectory, bool enablePatch)
        {
            var settingsFile = IOPath.Combine(qoderDirectory, "settings.json");
            if (File.Exists(settingsFile))
            {
                if (enablePatch)
                    PatchSettingsFile(settingsFile);
                return;
            }

            var content = @"{
    ""files.exclude"": {
        ""**/.DS_Store"": true,
        ""**/.git"": true,
        ""**/.vs"": true,
        ""**/.gitmodules"": true,
        ""**/.vsconfig"": true,
        ""**/*.booproj"": true,
        ""**/*.pidb"": true,
        ""**/*.suo"": true,
        ""**/*.user"": true,
        ""**/*.userprefs"": true,
        ""**/*.unityproj"": true,
        ""**/*.dll"": true,
        ""**/*.exe"": true,
        ""**/*.pdf"": true,
        ""**/*.mid"": true,
        ""**/*.midi"": true,
        ""**/*.wav"": true,
        ""**/*.gif"": true,
        ""**/*.ico"": true,
        ""**/*.jpg"": true,
        ""**/*.jpeg"": true,
        ""**/*.png"": true,
        ""**/*.psd"": true,
        ""**/*.tga"": true,
        ""**/*.tif"": true,
        ""**/*.tiff"": true,
        ""**/*.3ds"": true,
        ""**/*.3DS"": true,
        ""**/*.fbx"": true,
        ""**/*.FBX"": true,
        ""**/*.lxo"": true,
        ""**/*.LXO"": true,
        ""**/*.ma"": true,
        ""**/*.MA"": true,
        ""**/*.obj"": true,
        ""**/*.OBJ"": true,
        ""**/*.asset"": true,
        ""**/*.cubemap"": true,
        ""**/*.flare"": true,
        ""**/*.mat"": true,
        ""**/*.meta"": true,
        ""**/*.prefab"": true,
        ""**/*.unity"": true,
        ""build/"": true,
        ""Build/"": true,
        ""Library/"": true,
        ""library/"": true,
        ""obj/"": true,
        ""Obj/"": true,
        ""Logs/"": true,
        ""logs/"": true,
        ""ProjectSettings/"": true,
        ""UserSettings/"": true,
        ""temp/"": true,
        ""Temp/"": true
    },
    ""files.associations"": {
        ""*.asset"": ""yaml"",
        ""*.meta"": ""yaml"",
        ""*.prefab"": ""yaml"",
        ""*.unity"": ""yaml""
    },
    ""explorer.fileNesting.enabled"": true,
    ""explorer.fileNesting.patterns"": {
        ""*.sln"": ""*.csproj"",
        ""*.slnx"": ""*.csproj""
    },
    ""dotnet.defaultSolution"": """ + IOPath.GetFileName(ProjectGenerator.SolutionFile()) + @"""
}";

            File.WriteAllText(settingsFile, content);
        }

        private void PatchSettingsFile(string settingsFile)
        {
            try
            {
                const string excludesKey = "files.exclude";
                const string solutionKey = "dotnet.defaultSolution";

                var content = File.ReadAllText(settingsFile);
                var settings = JSONNode.Parse(content);

                var excludes = settings[excludesKey] as JSONObject;
                if (excludes == null)
                    return;

                var patchList = new List<string>();
                var patched = false;

                foreach (var exclude in excludes)
                {
                    if (!bool.TryParse(exclude.Value, out var exc) || !exc)
                        continue;

                    var key = exclude.Key;
                    if (!key.EndsWith(".sln") && !key.EndsWith(".csproj") && !key.EndsWith(".slnx"))
                        continue;

                    if (!Regex.IsMatch(key, "^(\\*\\*[\\\\\\/])?\\*\\.(sln|slnx|csproj)$"))
                        continue;

                    patchList.Add(key);
                    patched = true;
                }

                var defaultSolution = settings[solutionKey];
                var solutionFile = IOPath.GetFileName(ProjectGenerator.SolutionFile());
                if (defaultSolution == null || defaultSolution.Value != solutionFile)
                {
                    settings[solutionKey] = solutionFile;
                    patched = true;
                }

                if (!patched)
                    return;

                foreach (var patch in patchList)
                    excludes.Remove(patch);

                WriteAllTextFromJObject(settingsFile, settings);
            }
            catch (Exception)
            {
            }
        }

        private const string DefaultRecommendedExtensionsContent = @"{
    ""recommendations"": [
        ""visualstudiotoolsforunity.vstuc"",
        ""ms-dotnettools.csharp"",
        ""ms-dotnettools.csdevkit"",
        ""ms-dotnettools.vscode-dotnet-runtime"",
        ""jchannon.csharpextensions"",
        ""k--kato.docomment"",
        ""Unity.unity-debug""
    ],
    ""unwantedRecommendations"": []
}";

        private static void CreateRecommendedExtensionsFile(string qoderDirectory, bool enablePatch)
        {
            var extensionFile = IOPath.Combine(qoderDirectory, "extensions.json");
            if (File.Exists(extensionFile))
            {
                if (enablePatch)
                    PatchRecommendedExtensionsFile(extensionFile);
                return;
            }

            File.WriteAllText(extensionFile, DefaultRecommendedExtensionsContent);
        }

        private static void PatchRecommendedExtensionsFile(string extensionFile)
        {
            try
            {
                const string recommendationsKey = "recommendations";

                var content = File.ReadAllText(extensionFile);
                var extensions = JSONNode.Parse(content);

                var recommendations = extensions[recommendationsKey] as JSONArray;
                if (recommendations == null)
                {
                    recommendations = new JSONArray();
                    extensions.Add(recommendationsKey, recommendations);
                }

                var requiredExtensions = new[] { MicrosoftUnityExtensionId, "ms-dotnettools.csharp" };
                var patched = false;

                foreach (var ext in requiredExtensions)
                {
                    if (recommendations.Linq.Any(entry => entry.Value.Value == ext))
                        continue;

                    recommendations.Add(ext);
                    patched = true;
                }

                if (patched)
                    WriteAllTextFromJObject(extensionFile, extensions);
            }
            catch (Exception)
            {
            }
        }

        private const string DefaultTasksFileContent = @"{
    ""version"": ""2.0.0"",
    ""tasks"": [
        {
            ""label"": ""Build Unity Project"",
            ""type"": ""shell"",
            ""command"": ""echo"",
            ""args"": [""Build triggered from Qoder""],
            ""group"": {
                ""kind"": ""build"",
                ""isDefault"": true
            },
            ""problemMatcher"": [""$msCompile""]
        },
        {
            ""label"": ""Run Unity Tests"",
            ""type"": ""shell"",
            ""command"": ""echo"",
            ""args"": [""Tests triggered from Qoder""],
            ""group"": ""test"",
            ""problemMatcher"": []
        }
    ]
}";

        private static void CreateTasksFile(string qoderDirectory, bool enablePatch)
        {
            var tasksFile = IOPath.Combine(qoderDirectory, "tasks.json");
            if (File.Exists(tasksFile))
                return;

            File.WriteAllText(tasksFile, DefaultTasksFileContent);
        }

        private const string DefaultEditorConfigContent = @"# Unity C# EditorConfig
root = true

[*.cs]
indent_style = space
indent_size = 4
tab_width = 4
end_of_line = lf
charset = utf-8-bom
trim_trailing_whitespace = true
insert_final_newline = true

# C# 代码风格
dotnet_sort_system_directives_first = true
csharp_new_line_before_open_brace = all
csharp_indent_case_contents = true
csharp_prefer_braces = true:suggestion

[*.{json,asmdef}]
indent_style = space
indent_size = 2
";

        private void CreateEditorConfigFile(string projectDirectory)
        {
            var editorConfigFile = IOPath.Combine(projectDirectory, ".editorconfig");
            if (File.Exists(editorConfigFile))
                return;

            File.WriteAllText(editorConfigFile, DefaultEditorConfigContent);
        }

        private const string DefaultOmniSharpConfigContent = @"{
    ""MsBuild"": {
        ""UseBundledOnly"": true
    },
    ""RoslynExtensionsOptions"": {
        ""EnableAnalyzersSupport"": true,
        ""EnableImportCompletion"": true,
        ""AnalyzeOpenDocumentsOnly"": true
    },
    ""FormattingOptions"": {
        ""EnableEditorConfigSupport"": true,
        ""OrganizeImports"": true
    }
}";

        private void CreateOmniSharpConfigFile(string projectDirectory)
        {
            var omnisharpFile = IOPath.Combine(projectDirectory, "omnisharp.json");
            if (File.Exists(omnisharpFile))
                return;

            File.WriteAllText(omnisharpFile, DefaultOmniSharpConfigContent);
        }

        private static void WriteAllTextFromJObject(string file, JSONNode node)
        {
            using (var fs = File.Open(file, FileMode.Create))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(node.ToString(aIndent: 4));
            }
        }

        #endregion

        #region Window Reuse & Open

        private Process FindRunningQoderWithSolution(string solutionPath)
        {
            var normalizedTargetPath = solutionPath.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();

#if !UNITY_EDITOR_WIN
			if (!normalizedTargetPath.StartsWith("/"))
				normalizedTargetPath = "/" + normalizedTargetPath;
#endif

            var processes = new List<Process>();

#if UNITY_EDITOR_OSX
			processes.AddRange(Process.GetProcessesByName("Qoder"));
			processes.AddRange(Process.GetProcessesByName("Qoder Helper"));
#elif UNITY_EDITOR_LINUX
			processes.AddRange(Process.GetProcessesByName("qoder"));
			processes.AddRange(Process.GetProcessesByName("Qoder"));
#else
            processes.AddRange(Process.GetProcessesByName("Qoder"));
            processes.AddRange(Process.GetProcessesByName("qoder"));
#endif

            foreach (var process in processes)
            {
                try
                {
                    var workspaces = ProcessRunner.GetProcessWorkspaces(process);
                    if (workspaces == null || workspaces.Length == 0)
                        continue;

                    foreach (var workspace in workspaces)
                    {
                        var normalizedWorkspaceDir = workspace.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();

#if !UNITY_EDITOR_WIN
						if (!normalizedWorkspaceDir.StartsWith("/"))
							normalizedWorkspaceDir = "/" + normalizedWorkspaceDir;
#endif

                        if (string.Equals(normalizedWorkspaceDir, normalizedTargetPath, StringComparison.OrdinalIgnoreCase) ||
                            normalizedWorkspaceDir.StartsWith(normalizedTargetPath + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            return process;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Qoder] Error checking process: {ex.Message}");
                }
            }

            return null;
        }

        private static string TryFindWorkspace(string directory)
        {
            var files = Directory.GetFiles(directory, "*.code-workspace", SearchOption.TopDirectoryOnly);
            if (files.Length == 0 || files.Length > 1)
                return null;
            return files[0];
        }

        public override bool Open(string path, int line, int column, string solution)
        {
            line = Math.Max(1, line);
            column = Math.Max(0, column);

            var directory = IOPath.GetDirectoryName(solution);
            var application = Path;

            var workspace = TryFindWorkspace(directory);
            workspace ??= directory;
            directory = workspace;

            if (EditorPrefs.GetBool(ReuseExistingWindowKey, true))
            {
                var existingProcess = FindRunningQoderWithSolution(directory);
                if (existingProcess != null)
                {
                    try
                    {
                        var args = string.IsNullOrEmpty(path) ? $"--reuse-window \"{directory}\"" : $"--reuse-window -g \"{path}\":{line}:{column}";

                        ProcessRunner.Start(ProcessStartInfoFor(application, args));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Qoder] Error using existing instance: {ex.Message}");
                    }
                }
            }

            var newArgs = string.IsNullOrEmpty(path) ? $"--new-window \"{directory}\"" : $"--new-window \"{directory}\" -g \"{path}\":{line}:{column}";

            ProcessRunner.Start(ProcessStartInfoFor(application, newArgs));
            return true;
        }

        private static ProcessStartInfo ProcessStartInfoFor(string application, string arguments)
        {
#if UNITY_EDITOR_OSX
			arguments = $"-n \"{application}\" --args {arguments}";
			application = "open";
			return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false, shell: true);
#else
            return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false);
#endif
        }

        #endregion

        public static void Initialize()
        {
        }
    }
}