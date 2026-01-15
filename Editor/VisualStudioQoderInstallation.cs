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
using IOPath = System.IO.Path;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal class VisualStudioQoderInstallation : VisualStudioInstallation
	{
		private static readonly IGenerator _generator = GeneratorFactory.GetInstance(GeneratorStyle.SDK);

		public override bool SupportsAnalyzers
		{
			get { return true; }
		}

		public override Version LatestLanguageVersionSupported
		{
			get { return new Version(11, 0); }
		}

		public override string[] GetAnalyzers()
		{
			return Array.Empty<string>();
		}

		public override IGenerator ProjectGenerator
		{
			get { return _generator; }
		}

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

		public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
		{
			installation = null;

			if (string.IsNullOrEmpty(editorPath))
				return false;

			if (!IsCandidateForDiscovery(editorPath))
				return false;

			Version version = null;

			try
			{
#if UNITY_EDITOR_WIN
				var versionInfo = FileVersionInfo.GetVersionInfo(editorPath);
				if (versionInfo != null && !string.IsNullOrEmpty(versionInfo.ProductVersion))
				{
					var versionString = versionInfo.ProductVersion.Split('-').First().Split('+').First();
					Version.TryParse(versionString, out version);
				}
#endif
			}
			catch (Exception)
			{
				// do not fail if we are not able to retrieve the exact version number
			}

			installation = new VisualStudioQoderInstallation()
			{
				IsPrerelease = false,
				Name = "Qoder" + (version != null ? $" [{version.ToString(3)}]" : string.Empty),
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

			// Standard installation paths
			foreach (var basePath in new[] { localAppPath, programFiles })
			{
				candidates.Add(IOPath.Combine(basePath, "Qoder", "Qoder.exe"));
				candidates.Add(IOPath.Combine(basePath, "Qoder", "qoder.exe"));
				candidates.Add(IOPath.Combine(basePath, "Qoder", "bin", "qoder.cmd"));
			}
			
			// User profile locations
			candidates.Add(IOPath.Combine(userProfile, ".qoder", "Qoder.exe"));
			candidates.Add(IOPath.Combine(userProfile, ".qoder", "qoder.exe"));
			candidates.Add(IOPath.Combine(userProfile, ".qoder", "bin", "qoder.cmd"));
			
			// AppData Roaming
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			candidates.Add(IOPath.Combine(appData, "Qoder", "bin", "qoder.cmd"));
			candidates.Add(IOPath.Combine(appData, "Qoder", "Qoder.exe"));
			
#elif UNITY_EDITOR_OSX
			var appPath = "/Applications";
			candidates.Add(IOPath.Combine(appPath, "Qoder.app"));
			
			var userApps = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications");
			candidates.Add(IOPath.Combine(userApps, "Qoder.app"));
			
			candidates.Add("/usr/local/bin/qoder");
			candidates.Add(IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qoder", "bin", "qoder"));
			
#elif UNITY_EDITOR_LINUX
			candidates.Add("/usr/bin/qoder");
			candidates.Add("/bin/qoder");
			candidates.Add("/usr/local/bin/qoder");
			candidates.Add(IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qoder", "bin", "qoder"));
			candidates.Add("/snap/bin/qoder");
#endif

			foreach (var candidate in candidates.Distinct())
			{
				if (TryDiscoverInstallation(candidate, out var installation))
					yield return installation;
			}
		}

		public override void CreateExtraFiles(string projectDirectory)
		{
			try
			{
				var qoderDirectory = IOPath.Combine(projectDirectory.NormalizePathSeparators(), ".qoder");
				Directory.CreateDirectory(qoderDirectory);

				CreateSettingsFile(qoderDirectory);
			}
			catch (IOException)
			{
			}
		}

		private void CreateSettingsFile(string qoderDirectory)
		{
			var settingsFile = IOPath.Combine(qoderDirectory, "settings.json");
			if (File.Exists(settingsFile))
				return;

			const string content = @"{
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
    }
}";

			File.WriteAllText(settingsFile, content);
		}

		public override bool Open(string path, int line, int column, string solution)
		{
			line = Math.Max(1, line);
			column = Math.Max(0, column);

			var directory = IOPath.GetDirectoryName(solution);
			var application = Path;

			ProcessRunner.Start(string.IsNullOrEmpty(path) ?
				ProcessStartInfoFor(application, $"\"{directory}\"") :
				ProcessStartInfoFor(application, $"\"{directory}\" -g \"{path}\":{line}:{column}"));

			return true;
		}

		private static ProcessStartInfo ProcessStartInfoFor(string application, string arguments)
		{
#if UNITY_EDITOR_OSX
			// wrap with built-in OSX open feature
			arguments = $"-n \"{application}\" --args {arguments}";
			application = "open";
			return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false, shell: true);
#else
			return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false);
#endif
		}

		public static void Initialize()
		{
		}
	}
}
