using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Lxy.LauncherIcons.Editor
{
    /// <summary>
    /// Packages Assets/XPackageUse/AppIcons/Common plus the active platform's
    /// overrides. A platform file with the same logical ID wins over Common.
    /// </summary>
    public sealed class LauncherIconBuildProcessor : IPostGenerateGradleAndroidProject, IPostprocessBuildWithReport
    {
        private const string AndroidAliasPrefix = ".UnityLauncherIcon_";
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private static readonly Regex SourceFileName = new Regex(
            @"^icon_([a-z][a-z0-9_]*)\.png$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private enum IconPlatform
        {
            Android,
            IOS,
            OpenHarmony
        }

        private sealed class IconSource
        {
            internal string Id;
            internal string Path;
        }

        public int callbackOrder
        {
            get { return 900; }
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var icons = GetIconSources(IconPlatform.Android);
            if (icons.Count == 0)
            {
                return;
            }

            RequireDefaultIcon(icons, "Android");
            var launcherRoot = ResolveAndroidLauncherRoot(path);
            WriteAndroidMipmaps(icons, Path.Combine(launcherRoot, "src", "main", "res"));
            UpdateAndroidManifest(Path.Combine(launcherRoot, "src", "main", "AndroidManifest.xml"), icons);
            Debug.Log("[LauncherIcon] Packaged " + icons.Count + " Android launcher icons.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var icons = GetIconSources(IconPlatform.IOS);
            if (icons.Count == 0)
            {
                return;
            }

            RequireDefaultIcon(icons, "iOS");
            WriteIOSIcons(report.summary.outputPath, icons);
            Debug.Log("[LauncherIcon] Packaged " + icons.Count + " iOS launcher icons.");
        }

        /// <summary>
        /// Entry point for the project's existing OpenHarmony exporter. Pass the
        /// exported AppScope directory (the direct parent of app.json5) after it
        /// has created the Harmony project and before hvigor builds the HAP.
        /// </summary>
        public static void ExportOpenHarmonyIcons(string appScopeDirectory)
        {
            if (string.IsNullOrWhiteSpace(appScopeDirectory))
            {
                throw new ArgumentException("An OpenHarmony AppScope directory is required.", nameof(appScopeDirectory));
            }

            var icons = GetIconSources(IconPlatform.OpenHarmony);
            if (icons.Count == 0)
            {
                return;
            }

            RequireDefaultIcon(icons, "OpenHarmony");
            var fullAppScopeDirectory = Path.GetFullPath(appScopeDirectory);
            var appJsonPath = Path.Combine(fullAppScopeDirectory, "app.json5");
            if (!File.Exists(appJsonPath))
            {
                throw new FileNotFoundException("OpenHarmony AppScope/app.json5 was not found.", appJsonPath);
            }

            var mediaDirectory = Path.Combine(fullAppScopeDirectory, "resources", "base", "media");
            Directory.CreateDirectory(mediaDirectory);
            foreach (var icon in icons)
            {
                File.Copy(icon.Path, Path.Combine(mediaDirectory, GetHarmonyMediaFileName(icon.Id)), true);
            }

            var appJson = File.ReadAllText(appJsonPath, Encoding.UTF8);
            File.WriteAllText(appJsonPath, UpdateHarmonyAppJson(appJson, icons), new UTF8Encoding(false));
            Debug.Log("[LauncherIcon] Packaged " + icons.Count + " OpenHarmony launcher icons into " + fullAppScopeDirectory);
        }

        [MenuItem("Tools/Lxy/Launcher Icons/Validate Source Catalog")]
        private static void ValidateSourceCatalog()
        {
            var results = new StringBuilder();
            foreach (IconPlatform platform in Enum.GetValues(typeof(IconPlatform)))
            {
                var icons = GetIconSources(platform);
                results.Append(platform).Append(": ").Append(icons.Count).Append(" icon(s)");
                if (icons.Count > 0)
                {
                    RequireDefaultIcon(icons, platform.ToString());
                    results.Append(" [").Append(string.Join(", ", icons.Select(icon => icon.Id).ToArray())).Append(']');
                }

                results.AppendLine();
            }

            Debug.Log("[LauncherIcon] Source catalog is valid.\n" + results);
        }

        private static List<IconSource> GetIconSources(IconPlatform platform)
        {
            var sourceRoot = Path.Combine(Application.dataPath, "XPackageUse", "AppIcons");
            if (!Directory.Exists(sourceRoot))
            {
                return new List<IconSource>();
            }

            var icons = new Dictionary<string, IconSource>(StringComparer.Ordinal);
            AddDirectoryIcons(Path.Combine(sourceRoot, "Common"), icons);
            AddDirectoryIcons(Path.Combine(sourceRoot, GetPlatformDirectoryName(platform)), icons);
            return icons.Values.OrderBy(icon => icon.Id, StringComparer.Ordinal).ToList();
        }

        private static string GetPlatformDirectoryName(IconPlatform platform)
        {
            switch (platform)
            {
                case IconPlatform.Android:
                    return "Android";
                case IconPlatform.IOS:
                    return "iOS";
                case IconPlatform.OpenHarmony:
                    return "OpenHarmony";
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }
        }

        private static void AddDirectoryIcons(string directory, IDictionary<string, IconSource> icons)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (var filePath in Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
            {
                var match = SourceFileName.Match(Path.GetFileName(filePath));
                if (!match.Success)
                {
                    Debug.LogWarning("[LauncherIcon] Ignoring '" + filePath + "'. Source names must be icon_<id>.png.");
                    continue;
                }

                var iconId = match.Groups[1].Value;
                icons[iconId] = new IconSource { Id = iconId, Path = filePath };
            }
        }

        private static void RequireDefaultIcon(IEnumerable<IconSource> icons, string platformName)
        {
            if (!icons.Any(icon => icon.Id == LauncherIconId.Default))
            {
                throw new BuildFailedException(
                    "[LauncherIcon] " + platformName + " has launcher icon sources but no icon_default.png. " +
                    "Add it to Common or the platform override directory.");
            }
        }

        private static string ResolveAndroidLauncherRoot(string gradleProjectPath)
        {
            var parentDirectory = Directory.GetParent(gradleProjectPath);
            if (parentDirectory != null)
            {
                var siblingLauncher = Path.Combine(parentDirectory.FullName, "launcher");
                if (File.Exists(Path.Combine(siblingLauncher, "src", "main", "AndroidManifest.xml")))
                {
                    return siblingLauncher;
                }
            }

            var directLauncher = Path.Combine(gradleProjectPath, "launcher");
            if (File.Exists(Path.Combine(directLauncher, "src", "main", "AndroidManifest.xml")))
            {
                return directLauncher;
            }

            if (File.Exists(Path.Combine(gradleProjectPath, "src", "main", "AndroidManifest.xml")))
            {
                return gradleProjectPath;
            }

            throw new BuildFailedException("[LauncherIcon] Could not find the Android launcher module below " + gradleProjectPath);
        }

        private static void WriteAndroidMipmaps(IEnumerable<IconSource> icons, string resourceDirectory)
        {
            var densities = new[]
            {
                new IconSize("mipmap-ldpi", 36),
                new IconSize("mipmap-mdpi", 48),
                new IconSize("mipmap-hdpi", 72),
                new IconSize("mipmap-xhdpi", 96),
                new IconSize("mipmap-xxhdpi", 144),
                new IconSize("mipmap-xxxhdpi", 192)
            };

            foreach (var density in densities)
            {
                var destinationDirectory = Path.Combine(resourceDirectory, density.DirectoryName);
                Directory.CreateDirectory(destinationDirectory);
                foreach (var icon in icons)
                {
                    WriteResizedPng(icon.Path, Path.Combine(destinationDirectory, GetAndroidResourceName(icon.Id) + ".png"), density.Size);
                }
            }
        }

        private static void UpdateAndroidManifest(string manifestPath, IEnumerable<IconSource> icons)
        {
            if (!File.Exists(manifestPath))
            {
                throw new BuildFailedException("[LauncherIcon] AndroidManifest.xml was not found: " + manifestPath);
            }

            var document = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace);
            var manifest = document.Root;
            var application = manifest == null ? null : manifest.Element(manifest.Name.Namespace + "application");
            if (application == null)
            {
                throw new BuildFailedException("[LauncherIcon] AndroidManifest.xml has no application element.");
            }

            var android = XNamespace.Get(AndroidNamespace);
            var launchActivity = FindLaunchActivity(application, android);
            if (launchActivity == null)
            {
                throw new BuildFailedException("[LauncherIcon] Could not find the Android MAIN/LAUNCHER activity.");
            }

            RemoveLauncherCategories(launchActivity, android);
            foreach (var existingAlias in application.Elements(application.Name.Namespace + "activity-alias").ToList())
            {
                var aliasName = (string)existingAlias.Attribute(android + "name");
                if (!string.IsNullOrEmpty(aliasName) && aliasName.StartsWith(AndroidAliasPrefix, StringComparison.Ordinal))
                {
                    existingAlias.Remove();
                }
            }

            var targetActivity = (string)launchActivity.Attribute(android + "name");
            foreach (var icon in icons)
            {
                application.Add(CreateAndroidAlias(application.Name.Namespace, android, targetActivity, icon.Id));
            }

            document.Save(manifestPath);
        }

        private static XElement FindLaunchActivity(XElement application, XNamespace android)
        {
            foreach (var activity in application.Elements(application.Name.Namespace + "activity"))
            {
                foreach (var filter in activity.Elements(application.Name.Namespace + "intent-filter"))
                {
                    var hasMainAction = filter.Elements(application.Name.Namespace + "action")
                        .Any(action => (string)action.Attribute(android + "name") == "android.intent.action.MAIN");
                    var hasLauncherCategory = filter.Elements(application.Name.Namespace + "category")
                        .Any(category => (string)category.Attribute(android + "name") == "android.intent.category.LAUNCHER");
                    if (hasMainAction && hasLauncherCategory)
                    {
                        return activity;
                    }
                }
            }

            return null;
        }

        private static void RemoveLauncherCategories(XElement activity, XNamespace android)
        {
            foreach (var category in activity.Descendants(activity.Name.Namespace + "category").ToList())
            {
                if ((string)category.Attribute(android + "name") == "android.intent.category.LAUNCHER")
                {
                    category.Remove();
                }
            }
        }

        private static XElement CreateAndroidAlias(XNamespace xmlNamespace, XNamespace android, string targetActivity, string iconId)
        {
            var alias = new XElement(
                xmlNamespace + "activity-alias",
                new XAttribute(android + "name", AndroidAliasPrefix + iconId),
                new XAttribute(android + "targetActivity", targetActivity),
                new XAttribute(android + "icon", "@mipmap/" + GetAndroidResourceName(iconId)),
                new XAttribute(android + "enabled", iconId == LauncherIconId.Default ? "true" : "false"),
                new XAttribute(android + "exported", "true"));
            alias.Add(
                new XElement(
                    xmlNamespace + "intent-filter",
                    new XElement(xmlNamespace + "action", new XAttribute(android + "name", "android.intent.action.MAIN")),
                    new XElement(xmlNamespace + "category", new XAttribute(android + "name", "android.intent.category.LAUNCHER"))));
            return alias;
        }

        private static void WriteIOSIcons(string projectPath, IEnumerable<IconSource> icons)
        {
            var iconDirectory = Path.Combine(projectPath, "LauncherIcons");
            Directory.CreateDirectory(iconDirectory);
            var imageFiles = new List<string>();
            foreach (var icon in icons)
            {
                var baseFileName = "LauncherIcon_" + icon.Id;
                WriteResizedPng(icon.Path, Path.Combine(iconDirectory, baseFileName + ".png"), 60);
                WriteResizedPng(icon.Path, Path.Combine(iconDirectory, baseFileName + "@2x.png"), 120);
                WriteResizedPng(icon.Path, Path.Combine(iconDirectory, baseFileName + "@3x.png"), 180);
                imageFiles.Add(Path.Combine("LauncherIcons", baseFileName + ".png"));
                imageFiles.Add(Path.Combine("LauncherIcons", baseFileName + "@2x.png"));
                imageFiles.Add(Path.Combine("LauncherIcons", baseFileName + "@3x.png"));
            }

            AddIOSResourcesToProject(projectPath, imageFiles);
            UpdateIOSPlist(Path.Combine(projectPath, "Info.plist"), icons);
        }

        private static void AddIOSResourcesToProject(string projectPath, IEnumerable<string> imageFiles)
        {
            var pbxProjectPath = Path.Combine(projectPath, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxProjectPath))
            {
                throw new BuildFailedException("[LauncherIcon] Xcode project was not found: " + pbxProjectPath);
            }

            var projectType = Type.GetType("UnityEditor.iOS.Xcode.PBXProject, UnityEditor.iOS.Extensions.Xcode");
            if (projectType == null)
            {
                throw new BuildFailedException("[LauncherIcon] Unity iOS Build Support is required to package iOS launcher icons.");
            }

            var project = Activator.CreateInstance(projectType);
            projectType.GetMethod("ReadFromFile").Invoke(project, new object[] { pbxProjectPath });
            var mainTargetGuid = (string)projectType.GetMethod("GetUnityMainTargetGuid").Invoke(project, null);
            var addFileMethod = projectType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(method => method.Name == "AddFile" && method.GetParameters().Length >= 2);
            foreach (var imageFile in imageFiles)
            {
                var addFileParameters = addFileMethod.GetParameters();
                var addFileArguments = new object[addFileParameters.Length];
                addFileArguments[0] = imageFile;
                addFileArguments[1] = imageFile;
                for (var index = 2; index < addFileArguments.Length; index++)
                {
                    addFileArguments[index] = addFileParameters[index].HasDefaultValue
                        ? addFileParameters[index].DefaultValue
                        : Activator.CreateInstance(addFileParameters[index].ParameterType);
                }

                var guid = (string)addFileMethod.Invoke(project, addFileArguments);
                projectType.GetMethod("AddFileToBuild", new[] { typeof(string), typeof(string) })
                    .Invoke(project, new object[] { mainTargetGuid, guid });
            }

            projectType.GetMethod("WriteToFile").Invoke(project, new object[] { pbxProjectPath });
        }

        private static void UpdateIOSPlist(string plistPath, IEnumerable<IconSource> icons)
        {
            var document = XDocument.Load(plistPath, LoadOptions.PreserveWhitespace);
            var rootDictionary = document.Root == null ? null : document.Root.Element("dict");
            if (rootDictionary == null)
            {
                throw new BuildFailedException("[LauncherIcon] Info.plist has no root dictionary.");
            }

            var primaryFiles = new[] { "LauncherIcon_default", "LauncherIcon_default@2x", "LauncherIcon_default@3x" };
            var alternateIcons = new XElement("dict");
            foreach (var icon in icons.Where(item => item.Id != LauncherIconId.Default))
            {
                alternateIcons.Add(new XElement("key", icon.Id));
                alternateIcons.Add(CreateIOSIconDictionary("LauncherIcon_" + icon.Id));
            }

            var iconsDictionary = new XElement(
                "dict",
                new XElement("key", "CFBundlePrimaryIcon"),
                CreateIOSIconDictionary(primaryFiles),
                new XElement("key", "CFBundleAlternateIcons"),
                alternateIcons);
            SetPlistValue(rootDictionary, "CFBundleIcons", iconsDictionary);
            SetPlistValue(rootDictionary, "CFBundleIcons~ipad", new XElement(iconsDictionary));
            document.Save(plistPath);
        }

        private static XElement CreateIOSIconDictionary(string baseFileName)
        {
            return CreateIOSIconDictionary(new[] { baseFileName, baseFileName + "@2x", baseFileName + "@3x" });
        }

        private static XElement CreateIOSIconDictionary(IEnumerable<string> fileNames)
        {
            var files = new XElement("array");
            foreach (var fileName in fileNames)
            {
                files.Add(new XElement("string", fileName));
            }

            return new XElement("dict", new XElement("key", "CFBundleIconFiles"), files);
        }

        private static void SetPlistValue(XElement dictionary, string key, XElement value)
        {
            var elements = dictionary.Elements().ToList();
            for (var index = 0; index + 1 < elements.Count; index++)
            {
                if (elements[index].Name == "key" && elements[index].Value == key)
                {
                    elements[index + 1].ReplaceWith(value);
                    return;
                }
            }

            dictionary.Add(new XElement("key", key));
            dictionary.Add(value);
        }

        private static string UpdateHarmonyAppJson(string appJson, IEnumerable<IconSource> icons)
        {
            var appObject = Json5Object.FindNamedObject(appJson, "app");
            if (appObject == null)
            {
                throw new BuildFailedException("[LauncherIcon] app.json5 has no top-level app object.");
            }

            var alternateIcons = new StringBuilder("[\n");
            var alternatives = icons.Where(icon => icon.Id != LauncherIconId.Default).ToList();
            for (var index = 0; index < alternatives.Count; index++)
            {
                var icon = alternatives[index];
                alternateIcons.Append("    { \"name\": \"").Append(icon.Id)
                    .Append("\", \"icon\": \"$media:").Append(GetHarmonyMediaName(icon.Id)).Append("\" }");
                if (index + 1 < alternatives.Count)
                {
                    alternateIcons.Append(',');
                }

                alternateIcons.Append('\n');
            }

            alternateIcons.Append("  ]");
            appObject.SetProperty("icon", "\"$media:" + GetHarmonyMediaName(LauncherIconId.Default) + "\"");
            appObject.SetProperty("alternateIcons", alternateIcons.ToString());
            return appObject.Source;
        }

        private static string GetAndroidResourceName(string iconId)
        {
            return "launcher_icon_" + iconId;
        }

        private static string GetHarmonyMediaName(string iconId)
        {
            return "launcher_icon_" + iconId;
        }

        private static string GetHarmonyMediaFileName(string iconId)
        {
            return GetHarmonyMediaName(iconId) + ".png";
        }

        private static void WriteResizedPng(string sourcePath, string destinationPath, int size)
        {
            var imageData = File.ReadAllBytes(sourcePath);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!ImageConversion.LoadImage(source, imageData, false))
                {
                    throw new BuildFailedException("[LauncherIcon] Could not read PNG: " + sourcePath);
                }

                var output = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
                try
                {
                    output.SetPixels32(ResizeBilinear(source.GetPixels32(), source.width, source.height, size, size));
                    output.Apply(false, false);
                    File.WriteAllBytes(destinationPath, output.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static Color32[] ResizeBilinear(Color32[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            var target = new Color32[targetWidth * targetHeight];
            for (var y = 0; y < targetHeight; y++)
            {
                var sourceY = (y + 0.5f) * sourceHeight / targetHeight - 0.5f;
                var y0 = Mathf.Clamp(Mathf.FloorToInt(sourceY), 0, sourceHeight - 1);
                var y1 = Mathf.Min(y0 + 1, sourceHeight - 1);
                var yLerp = Mathf.Clamp01(sourceY - y0);
                for (var x = 0; x < targetWidth; x++)
                {
                    var sourceX = (x + 0.5f) * sourceWidth / targetWidth - 0.5f;
                    var x0 = Mathf.Clamp(Mathf.FloorToInt(sourceX), 0, sourceWidth - 1);
                    var x1 = Mathf.Min(x0 + 1, sourceWidth - 1);
                    var xLerp = Mathf.Clamp01(sourceX - x0);
                    target[y * targetWidth + x] = Color32.Lerp(
                        Color32.Lerp(source[y0 * sourceWidth + x0], source[y0 * sourceWidth + x1], xLerp),
                        Color32.Lerp(source[y1 * sourceWidth + x0], source[y1 * sourceWidth + x1], xLerp),
                        yLerp);
                }
            }

            return target;
        }

        private readonly struct IconSize
        {
            internal readonly string DirectoryName;
            internal readonly int Size;

            internal IconSize(string directoryName, int size)
            {
                DirectoryName = directoryName;
                Size = size;
            }
        }
    }

    /// <summary>
    /// Minimal JSON5 object editor. It preserves comments and unrelated text and
    /// only replaces top-level properties inside the named object.
    /// </summary>
    internal sealed class Json5Object
    {
        private readonly int bodyStart;
        private int bodyEnd;

        internal string Source { get; private set; }

        private Json5Object(string source, int bodyStart, int bodyEnd)
        {
            Source = source;
            this.bodyStart = bodyStart;
            this.bodyEnd = bodyEnd;
        }

        internal static Json5Object FindNamedObject(string source, string propertyName)
        {
            var property = FindProperty(source, 0, source.Length, propertyName);
            if (property == null)
            {
                return null;
            }

            var valueStart = SkipWhitespaceAndComments(source, property.ValueStart, property.ValueEnd);
            if (valueStart >= source.Length || source[valueStart] != '{')
            {
                return null;
            }

            return new Json5Object(source, valueStart + 1, FindMatching(source, valueStart, '{', '}'));
        }

        internal void SetProperty(string propertyName, string value)
        {
            var property = FindProperty(Source, bodyStart, bodyEnd, propertyName);
            if (property != null)
            {
                Source = Source.Substring(0, property.ValueStart) + value + Source.Substring(property.ValueEnd);
                bodyEnd += value.Length - (property.ValueEnd - property.ValueStart);
                return;
            }

            var insertion = "\n    \"" + propertyName + "\": " + value;
            var content = Source.Substring(bodyStart, bodyEnd - bodyStart);
            if (content.Trim().Length > 0)
            {
                insertion = "," + insertion;
            }

            Source = Source.Substring(0, bodyEnd) + insertion + "\n  " + Source.Substring(bodyEnd);
            bodyEnd += insertion.Length + 3;
        }

        private sealed class PropertySpan
        {
            internal int ValueStart;
            internal int ValueEnd;
        }

        private static PropertySpan FindProperty(string source, int start, int end, string expectedName)
        {
            var index = start;
            while (index < end)
            {
                index = SkipWhitespaceAndComments(source, index, end);
                if (index >= end)
                {
                    break;
                }

                string propertyName;
                if (source[index] == '\"' || source[index] == '\'')
                {
                    var quote = source[index++];
                    var nameStart = index;
                    index = FindStringEnd(source, index, quote);
                    propertyName = source.Substring(nameStart, index - nameStart);
                    index++;
                }
                else
                {
                    var nameStart = index;
                    while (index < end && (char.IsLetterOrDigit(source[index]) || source[index] == '_' || source[index] == '$'))
                    {
                        index++;
                    }

                    propertyName = source.Substring(nameStart, index - nameStart);
                }

                index = SkipWhitespaceAndComments(source, index, end);
                if (index >= end || source[index] != ':')
                {
                    index++;
                    continue;
                }

                var valueStart = SkipWhitespaceAndComments(source, index + 1, end);
                var valueEnd = FindValueEnd(source, valueStart, end);
                if (propertyName == expectedName)
                {
                    return new PropertySpan { ValueStart = valueStart, ValueEnd = valueEnd };
                }

                index = valueEnd + 1;
            }

            return null;
        }

        private static int FindValueEnd(string source, int start, int end)
        {
            var index = start;
            var depth = 0;
            while (index < end)
            {
                var character = source[index];
                if (character == '\"' || character == '\'')
                {
                    index = FindStringEnd(source, index + 1, character) + 1;
                    continue;
                }

                if (character == '/' && index + 1 < end && source[index + 1] == '/')
                {
                    index = source.IndexOf('\n', index + 2);
                    if (index < 0) return end;
                    continue;
                }

                if (character == '/' && index + 1 < end && source[index + 1] == '*')
                {
                    index = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    if (index < 0) return end;
                    index += 2;
                    continue;
                }

                if (character == '{' || character == '[' || character == '(') depth++;
                if (character == '}' || character == ']' || character == ')')
                {
                    if (depth == 0) return index;
                    depth--;
                }

                if (character == ',' && depth == 0) return index;
                index++;
            }

            return end;
        }

        private static int FindMatching(string source, int start, char open, char close)
        {
            var depth = 0;
            for (var index = start; index < source.Length; index++)
            {
                var character = source[index];
                if (character == '\"' || character == '\'')
                {
                    index = FindStringEnd(source, index + 1, character);
                    continue;
                }

                if (character == open) depth++;
                if (character == close && --depth == 0) return index;
            }

            throw new BuildFailedException("[LauncherIcon] app.json5 contains an unterminated object.");
        }

        private static int FindStringEnd(string source, int index, char quote)
        {
            while (index < source.Length)
            {
                if (source[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (source[index] == quote) return index;
                index++;
            }

            throw new BuildFailedException("[LauncherIcon] app.json5 contains an unterminated string.");
        }

        private static int SkipWhitespaceAndComments(string source, int index, int end)
        {
            while (index < end)
            {
                if (char.IsWhiteSpace(source[index]))
                {
                    index++;
                    continue;
                }

                if (source[index] == '/' && index + 1 < end && source[index + 1] == '/')
                {
                    var newline = source.IndexOf('\n', index + 2);
                    index = newline < 0 ? end : newline + 1;
                    continue;
                }

                if (source[index] == '/' && index + 1 < end && source[index + 1] == '*')
                {
                    var commentEnd = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    index = commentEnd < 0 ? end : commentEnd + 2;
                    continue;
                }

                break;
            }

            return index;
        }
    }
}
