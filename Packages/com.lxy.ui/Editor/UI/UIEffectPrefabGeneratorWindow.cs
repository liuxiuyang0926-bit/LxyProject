#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace LxyDemo.UIFramework.Editor
{
    public sealed class UIEffectPrefabGeneratorWindow : EditorWindow
    {
        private const string FigmaTokenEditorPrefsKey =
            "LxyDemo.UIEffectPrefabGenerator.FigmaToken";
        private const string CodexCommandEditorPrefsKey =
            "LxyDemo.UIEffectPrefabGenerator.CodexCommand";
        private const string CodexAutoGenerateEditorPrefsKey =
            "LxyDemo.UIEffectPrefabGenerator.CodexAutoGenerate";
        private const string ReferenceFolder =
            "Assets/Editor/UIReferences";
        private const string SchemaFolder =
            "Assets/Editor/UISchemas";

        private enum ReferenceSource
        {
            LocalImage,
            FigmaNodeUrl,
            DirectImageUrl,
        }

        private ReferenceSource source = ReferenceSource.LocalImage;
        private Texture2D referenceImage;
        private string sourceUrl = string.Empty;
        private string figmaToken = string.Empty;
        private bool rememberFigmaToken;
        private int figmaScale = 2;
        private TextAsset schemaAsset;
        private string panelId = "UIExample";
        private string prefabFolder =
            "Assets/GameResources/Prefabs/UIRes";
        private UIScriptType scriptType = UIScriptType.CSharp;
        private string codeNamespace = "LxyDemo.GameUI";
        private string logicClassName = "UIExample";
        private string scriptFolder = "Assets/Scripts/GameUI";
        private UILayer uiLayer = UILayer.Auto;
        private string resourceSearchRoots = "Assets/GameResources";
        private Vector2 scrollPosition;
        private bool requestInProgress;
        private string requestStatus = string.Empty;
        private string codexCommand = "codex";
        private bool autoGenerateAfterCodex = true;
        private bool showCodexSettings;
        private UIEffectCodexRunner codexRunner;
        private string pendingSchemaAssetPath = string.Empty;
        private string pendingReferenceAssetPath = string.Empty;
        private string pendingCodexOutputPath = string.Empty;
        private int pendingReferenceWidth;
        private int pendingReferenceHeight;
        private bool pendingSchemaExisted;
        private bool pendingAutoGenerate;
        private UIEffectCodexUsage lastCodexUsage;

        [Serializable]
        private sealed class UIEffectCodexResponse
        {
            public string schemaJson = string.Empty;
            public string summary = string.Empty;
        }

        private bool IsCodexRunning =>
            codexRunner != null && codexRunner.IsRunning;

        private bool IsBusy => requestInProgress || IsCodexRunning;

        [MenuItem("工具/UI工具/根据效果图生成Prefab")]
        private static void Open()
        {
            var window = GetWindow<UIEffectPrefabGeneratorWindow>();
            window.titleContent = new GUIContent("效果图生成 UI");
            window.minSize = new Vector2(510f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            rememberFigmaToken =
                EditorPrefs.HasKey(FigmaTokenEditorPrefsKey);
            string environmentToken =
                Environment.GetEnvironmentVariable(
                    "FIGMA_ACCESS_TOKEN");
            figmaToken = !string.IsNullOrWhiteSpace(environmentToken)
                ? environmentToken
                : EditorPrefs.GetString(
                    FigmaTokenEditorPrefsKey,
                    string.Empty);
            codexCommand = EditorPrefs.GetString(
                CodexCommandEditorPrefsKey,
                "codex");
            autoGenerateAfterCodex = EditorPrefs.GetBool(
                CodexAutoGenerateEditorPrefsKey,
                true);
            codexRunner = new UIEffectCodexRunner();
            EditorApplication.update += PollCodexRunner;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollCodexRunner;
            codexRunner?.Dispose();
            codexRunner = null;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition);
            DrawIntroduction();
            EditorGUILayout.Space(8f);
            DrawReferenceSection();
            EditorGUILayout.Space(12f);
            DrawSchemaSection();
            EditorGUILayout.Space(12f);
            DrawGenerationSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawIntroduction()
        {
            EditorGUILayout.HelpBox(
                "推荐流程：导入效果图后，在此窗口后台调用 " +
                "$unity-ui-generator 生成可审查的 UISchema，" +
                "再由项目构建器确定性生成 Prefab。Codex 使用本机已有登录，" +
                "不会打开终端窗口。",
                MessageType.Info);
        }

        private void DrawReferenceSection()
        {
            EditorGUILayout.LabelField(
                "1. 设计来源",
                EditorStyles.boldLabel);
            source = (ReferenceSource)EditorGUILayout.EnumPopup(
                "来源",
                source);

            switch (source)
            {
                case ReferenceSource.LocalImage:
                    referenceImage = (Texture2D)EditorGUILayout.ObjectField(
                        "效果图",
                        referenceImage,
                        typeof(Texture2D),
                        false);
                    using (new EditorGUI.DisabledScope(IsBusy))
                    {
                        if (GUILayout.Button("从磁盘导入 PNG/JPG"))
                        {
                            ImportLocalReference();
                        }
                    }
                    break;
                case ReferenceSource.FigmaNodeUrl:
                    DrawFigmaFields();
                    break;
                case ReferenceSource.DirectImageUrl:
                    DrawDirectImageFields();
                    break;
            }

            if (!string.IsNullOrWhiteSpace(requestStatus))
            {
                EditorGUILayout.HelpBox(
                    requestStatus,
                    IsBusy
                        ? MessageType.Info
                        : MessageType.None);
            }
        }

        private void DrawFigmaFields()
        {
            sourceUrl = EditorGUILayout.TextField(
                "Figma 节点链接",
                sourceUrl);
            figmaToken = EditorGUILayout.PasswordField(
                "Personal access token",
                figmaToken);
            rememberFigmaToken = EditorGUILayout.Toggle(
                "保存在本机 EditorPrefs",
                rememberFigmaToken);
            figmaScale = EditorGUILayout.IntSlider(
                "渲染倍率",
                figmaScale,
                1,
                4);

            EditorGUILayout.HelpBox(
                "Token 只通过 X-Figma-Token 请求头发送，" +
                "不会写入项目或 UISchema。也可使用环境变量 " +
                "FIGMA_ACCESS_TOKEN。",
                MessageType.None);

            using (new EditorGUI.DisabledScope(IsBusy))
            {
                if (GUILayout.Button("下载 Figma 节点效果图"))
                {
                    DownloadFigmaReference();
                }
            }
        }

        private void DrawDirectImageFields()
        {
            sourceUrl = EditorGUILayout.TextField(
                "原图 URL",
                sourceUrl);
            EditorGUILayout.HelpBox(
                "蓝湖首版支持：蓝湖导出的本地 PNG/JPG，或无需登录即可" +
                "直接返回图片内容的原图 URL。普通蓝湖分享页不会被抓取，" +
                "请先导出图片。",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(IsBusy))
            {
                if (GUILayout.Button("下载原图"))
                {
                    DownloadDirectReference();
                }
            }
        }

        private void DrawSchemaSection()
        {
            EditorGUILayout.LabelField(
                "2. UISchema",
                EditorStyles.boldLabel);
            schemaAsset = (TextAsset)EditorGUILayout.ObjectField(
                "Schema JSON",
                schemaAsset,
                typeof(TextAsset),
                false);
            string previousPanelId = panelId;
            panelId = EditorGUILayout.TextField("Panel ID", panelId);
            if (!string.Equals(
                    previousPanelId,
                    panelId,
                    StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(logicClassName) ||
                 string.Equals(
                     logicClassName,
                     CSharpUIGenerator.SanitizeTypeName(previousPanelId),
                     StringComparison.Ordinal)))
            {
                logicClassName = GetSafePanelId();
            }

            using (new EditorGUI.DisabledScope(
                       referenceImage == null || IsBusy))
            {
                if (GUILayout.Button("创建基础 UISchema"))
                {
                    CreateStarterSchema();
                }
            }

            EditorGUILayout.HelpBox(
                "“创建基础 UISchema”只建立画布、背景和标题样例。" +
                "让 Codex 根据效果图补齐真实层级、布局、文字和资源语义后，" +
                "再生成 Prefab。",
                MessageType.None);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Codex AI 分析",
                EditorStyles.boldLabel);
            autoGenerateAfterCodex = EditorGUILayout.Toggle(
                "完成后自动生成 Prefab",
                autoGenerateAfterCodex);
            showCodexSettings = EditorGUILayout.Foldout(
                showCodexSettings,
                "Codex 设置",
                true);
            if (showCodexSettings)
            {
                EditorGUI.indentLevel++;
                codexCommand = EditorGUILayout.TextField(
                    "Codex 命令",
                    codexCommand);
                EditorGUILayout.HelpBox(
                    "默认填写 codex。找不到命令时，可填写 codex.cmd、" +
                    "codex.exe 或 codex.ps1 的完整路径。后台任务只获得" +
                    "项目只读权限；Unity 验证结果后才写入 UISchema。",
                    MessageType.None);
                EditorGUI.indentLevel--;
            }

            if (lastCodexUsage != null)
            {
                EditorGUILayout.HelpBox(
                    "上次 AI 分析 Token：" +
                    $"输入 {lastCodexUsage.input_tokens:N0}（缓存 " +
                    $"{lastCodexUsage.cached_input_tokens:N0}），" +
                    $"输出 {lastCodexUsage.output_tokens:N0}，" +
                    $"其中推理 {lastCodexUsage.reasoning_output_tokens:N0}，" +
                    $"合计 {lastCodexUsage.TotalTokens:N0}。",
                    MessageType.None);
            }

            if (IsCodexRunning)
            {
                if (GUILayout.Button("取消 AI 分析"))
                {
                    codexRunner.Cancel();
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(
                           referenceImage == null || requestInProgress))
                {
                    string buttonLabel = autoGenerateAfterCodex
                        ? "AI 分析效果图并生成 Prefab"
                        : "AI 分析效果图并生成 UISchema";
                    if (GUILayout.Button(
                            buttonLabel,
                            GUILayout.Height(34f)))
                    {
                        BeginCodexAnalysis();
                    }
                }
            }
        }

        private void DrawGenerationSection()
        {
            EditorGUILayout.LabelField(
                "3. 生成 Prefab",
                EditorStyles.boldLabel);
            prefabFolder = EditorGUILayout.TextField(
                "Prefab 目录",
                prefabFolder);
            scriptType = (UIScriptType)EditorGUILayout.EnumPopup(
                "脚本类型",
                scriptType);
            using (new EditorGUI.DisabledScope(
                       scriptType != UIScriptType.CSharp))
            {
                codeNamespace = EditorGUILayout.TextField(
                    "C# 命名空间",
                    codeNamespace);
                logicClassName = EditorGUILayout.TextField(
                    "逻辑类名",
                    logicClassName);
                scriptFolder = EditorGUILayout.TextField(
                    "脚本目录",
                    scriptFolder);
            }
        }

        private void BeginCodexAnalysis()
        {
            if (referenceImage == null)
            {
                requestStatus = "请先导入或选择效果图。";
                return;
            }

            string referenceAssetPath =
                AssetDatabase.GetAssetPath(referenceImage);
            if (string.IsNullOrWhiteSpace(referenceAssetPath))
            {
                requestStatus = "效果图必须是当前项目中的资源。";
                return;
            }

            string safePanelId = GetSafePanelId();
            EnsureAssetFolder(SchemaFolder);
            string schemaAssetPath =
                $"{SchemaFolder}/{safePanelId}.json";
            string schemaAbsolutePath =
                CSharpUIGenerator.ToAbsolutePath(schemaAssetPath);
            bool schemaExists = File.Exists(schemaAbsolutePath);
            if (schemaExists &&
                !EditorUtility.DisplayDialog(
                    "更新 UISchema",
                    schemaAssetPath +
                    " 已存在。是否允许本次 AI 分析更新它？",
                    "允许更新",
                    "取消"))
            {
                requestStatus = "已取消 AI 分析。";
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            string referenceAbsolutePath =
                CSharpUIGenerator.ToAbsolutePath(referenceAssetPath);
            string temporaryFolder = Path.Combine(
                projectRoot,
                "Library",
                "LxyDemo",
                "UIEffectCodex");
            string outputSchemaPath = Path.Combine(
                temporaryFolder,
                "UISchemaResponse.schema.json");
            string outputPath = Path.Combine(
                temporaryFolder,
                safePanelId + "_" +
                Guid.NewGuid().ToString("N") + ".json");

            try
            {
                Directory.CreateDirectory(temporaryFolder);
                File.WriteAllText(
                    outputSchemaPath,
                    BuildCodexOutputSchema(),
                    new System.Text.UTF8Encoding(false));

                codexCommand = string.IsNullOrWhiteSpace(codexCommand)
                    ? "codex"
                    : codexCommand.Trim();
                EditorPrefs.SetString(
                    CodexCommandEditorPrefsKey,
                    codexCommand);
                EditorPrefs.SetBool(
                    CodexAutoGenerateEditorPrefsKey,
                    autoGenerateAfterCodex);

                pendingSchemaAssetPath = schemaAssetPath;
                pendingReferenceAssetPath = referenceAssetPath;
                pendingCodexOutputPath = outputPath;
                pendingReferenceWidth = referenceImage.width;
                pendingReferenceHeight = referenceImage.height;
                pendingSchemaExisted = schemaExists;
                pendingAutoGenerate = autoGenerateAfterCodex;
                lastCodexUsage = null;

                codexRunner ??= new UIEffectCodexRunner();
                codexRunner.Start(new UIEffectCodexRequest
                {
                    codexCommand = codexCommand,
                    projectRoot = projectRoot,
                    imagePath = referenceAbsolutePath,
                    outputSchemaPath = outputSchemaPath,
                    outputPath = outputPath,
                    prompt = BuildCodexPrompt(
                        safePanelId,
                        referenceAssetPath,
                        schemaAssetPath),
                });
                requestStatus =
                    "Codex 已在后台启动，正在分析效果图…";
            }
            catch (Exception exception)
            {
                requestStatus =
                    "无法启动 Codex：" + exception.Message;
                Debug.LogException(exception);
                DeleteCodexOutputFile();
            }
        }

        private void PollCodexRunner()
        {
            if (codexRunner == null)
            {
                return;
            }

            bool changed = false;
            while (codexRunner.TryDequeueProgress(out string progress))
            {
                requestStatus = progress;
                changed = true;
            }

            if (codexRunner.TryTakeResult(
                    out UIEffectCodexRunResult result))
            {
                changed = true;
                HandleCodexResult(result);
            }

            if (changed)
            {
                Repaint();
            }
        }

        private void HandleCodexResult(UIEffectCodexRunResult result)
        {
            try
            {
                if (result == null)
                {
                    throw new InvalidOperationException(
                        "Codex 没有返回运行结果。");
                }

                lastCodexUsage = result.usage;

                if (result.canceled)
                {
                    requestStatus = "已取消 AI 分析。";
                    return;
                }

                if (!result.succeeded)
                {
                    string detail = GetShortError(result.error);
                    requestStatus =
                        $"Codex 分析失败（退出码 {result.exitCode}）" +
                        (detail.Length == 0 ? "。" : "：" + detail);
                    Debug.LogError(requestStatus);
                    return;
                }

                UIEffectCodexResponse response =
                    JsonUtility.FromJson<UIEffectCodexResponse>(
                        result.outputJson);
                if (response == null ||
                    string.IsNullOrWhiteSpace(response.schemaJson))
                {
                    throw new InvalidOperationException(
                        "Codex 返回结果中缺少 schemaJson。");
                }

                UIEffectSchema schema =
                    UIEffectSchemaUtility.Parse(response.schemaJson);
                int collapsedNodes =
                    UIEffectSchemaUtility.OptimizeRepeatedNodes(schema);
                string expectedPanelId = Path.GetFileNameWithoutExtension(
                    pendingSchemaAssetPath);
                if (!string.Equals(
                        schema.name,
                        expectedPanelId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"UISchema.name 应为 {expectedPanelId}，" +
                        $"实际为 {schema.name}。");
                }

                if (!Mathf.Approximately(
                        schema.designWidth,
                        pendingReferenceWidth) ||
                    !Mathf.Approximately(
                        schema.designHeight,
                        pendingReferenceHeight))
                {
                    throw new InvalidOperationException(
                        "AI 返回的设计尺寸与效果图不一致：" +
                        $"期望 {pendingReferenceWidth}x" +
                        $"{pendingReferenceHeight}，实际 " +
                        $"{schema.designWidth}x{schema.designHeight}。");
                }

                schema.referenceImage = pendingReferenceAssetPath;
                string schemaAbsolutePath =
                    CSharpUIGenerator.ToAbsolutePath(
                        pendingSchemaAssetPath);
                if (!pendingSchemaExisted &&
                    File.Exists(schemaAbsolutePath) &&
                    !EditorUtility.DisplayDialog(
                        "UISchema 已出现",
                        pendingSchemaAssetPath +
                        " 在分析期间被创建。是否覆盖？",
                        "覆盖",
                        "取消"))
                {
                    requestStatus =
                        "AI 分析完成，但没有覆盖新出现的 UISchema。";
                    return;
                }

                File.WriteAllText(
                    schemaAbsolutePath,
                    UIEffectSchemaUtility.ToCompactJson(schema),
                    new System.Text.UTF8Encoding(false));
                AssetDatabase.ImportAsset(
                    pendingSchemaAssetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                schemaAsset =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(
                        pendingSchemaAssetPath);
                Selection.activeObject = schemaAsset;
                EditorGUIUtility.PingObject(schemaAsset);

                string summary = string.IsNullOrWhiteSpace(response.summary)
                    ? "无附加说明"
                    : response.summary.Trim();
                requestStatus =
                    "AI 已生成 UISchema：" +
                    pendingSchemaAssetPath;
                Debug.Log(
                    "[UI Effect Generator/Codex] UISchema：" +
                    pendingSchemaAssetPath +
                    $"\n自动合并重复节点：{collapsedNodes}" +
                    "\n说明：" + summary,
                    schemaAsset);

                if (pendingAutoGenerate)
                {
                    GeneratePrefab();
                }
            }
            catch (Exception exception)
            {
                requestStatus =
                    "处理 Codex 结果失败：" + exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                DeleteCodexOutputFile();
            }
        }

        private string BuildCodexPrompt(
            string safePanelId,
            string referenceAssetPath,
            string schemaAssetPath)
        {
            return
                "$unity-ui-generator\n\n" +
                "Unity Editor 低 Token 模式：只做视觉推理并返回精简的 " +
                "UISchema 2.0，不执行项目搜索或文件操作。\n\n" +
                $"Panel ID：{safePanelId}\n" +
                $"效果图项目路径：{referenceAssetPath}\n" +
                $"目标 Schema 路径：{schemaAssetPath}\n" +
                $"效果图尺寸：{referenceImage.width}x" +
                $"{referenceImage.height}\n\n" +
                "只读取 Skill 的 references/compact-blueprint.md。" +
                "输出时省略所有默认字段并使用单行 JSON。重复列表、页签" +
                "或卡片只定义一次，用 repeatCount/repeatOffsetX/" +
                "repeatOffsetY 和 variants 表达差异；重复节点名不要自行加" +
                "数字后缀。referenceImage 可省略，Unity 会注入。" +
                "资源搜索、锚点推导、Sprite 匹配、占位、Schema 压缩、" +
                "Prefab 和代码生成全部由 C# 完成。summary 只报告看不清的" +
                "文字和无法确认的交互，不要列资源搜索结果。";
        }

        private static string BuildCodexOutputSchema()
        {
            return
                "{\n" +
                "  \"type\": \"object\",\n" +
                "  \"properties\": {\n" +
                "    \"schemaJson\": {\n" +
                "      \"type\": \"string\",\n" +
                "      \"description\": \"单行精简 UISchema 2.0 JSON 字符串\"\n" +
                "    },\n" +
                "    \"summary\": {\n" +
                "      \"type\": \"string\",\n" +
                "      \"description\": \"中文生成摘要\"\n" +
                "    }\n" +
                "  },\n" +
                "  \"required\": [\"schemaJson\", \"summary\"],\n" +
                "  \"additionalProperties\": false\n" +
                "}\n";
        }

        private static string GetShortError(string value)
        {
            string text = (value ?? string.Empty).Trim();
            const int maximumLength = 1200;
            return text.Length <= maximumLength
                ? text
                : "…" + text.Substring(text.Length - maximumLength);
        }

        private void DeleteCodexOutputFile()
        {
            string path = pendingCodexOutputPath;
            pendingCodexOutputPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    ".."));
                string temporaryRoot = Path.GetFullPath(Path.Combine(
                    projectRoot,
                    "Library",
                    "LxyDemo",
                    "UIEffectCodex"));
                string fullPath = Path.GetFullPath(path);
                if (!fullPath.StartsWith(
                        temporaryRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                File.Delete(fullPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "无法清理 Codex 临时输出：" + exception.Message);
            }
        }

        private void DownloadFigmaReference()
        {
            if (!TryParseFigmaUrl(
                    sourceUrl,
                    out string fileKey,
                    out string nodeId,
                    out string parseError))
            {
                requestStatus = parseError;
                return;
            }

            if (string.IsNullOrWhiteSpace(figmaToken))
            {
                requestStatus =
                    "缺少 Figma token。请填写 token，或设置 " +
                    "FIGMA_ACCESS_TOKEN。";
                return;
            }

            if (rememberFigmaToken)
            {
                EditorPrefs.SetString(
                    FigmaTokenEditorPrefsKey,
                    figmaToken.Trim());
            }
            else
            {
                EditorPrefs.DeleteKey(FigmaTokenEditorPrefsKey);
            }

            string requestUrl = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.figma.com/v1/images/{0}" +
                "?ids={1}&format=png&scale={2}",
                Uri.EscapeDataString(fileKey),
                Uri.EscapeDataString(nodeId),
                figmaScale);
            UnityWebRequest request = UnityWebRequest.Get(requestUrl);
            request.SetRequestHeader(
                "X-Figma-Token",
                figmaToken.Trim());
            requestStatus = "正在请求 Figma 渲染地址…";
            BeginRequest(
                request,
                bytes => HandleFigmaRenderResponse(bytes, nodeId));
        }

        private void ImportLocalReference()
        {
            string path = EditorUtility.OpenFilePanel(
                "选择 UI 效果图",
                string.Empty,
                "png,jpg,jpeg");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                SaveReferenceImage(File.ReadAllBytes(path), "Local");
            }
            catch (Exception exception)
            {
                requestStatus = "导入效果图失败：" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void HandleFigmaRenderResponse(
            byte[] bytes,
            string nodeId)
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            string imageUrl = ExtractFigmaImageUrl(json, nodeId);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                requestStatus =
                    "Figma 已响应，但没有返回该节点的图片地址。" +
                    "请确认链接包含 node-id 且 token 有文件读取权限。";
                Repaint();
                return;
            }

            requestStatus = "正在下载 Figma 节点图片…";
            BeginRequest(
                UnityWebRequest.Get(imageUrl),
                imageBytes => SaveReferenceImage(
                    imageBytes,
                    "Figma"));
        }

        private void DownloadDirectReference()
        {
            if (!Uri.TryCreate(
                    sourceUrl?.Trim(),
                    UriKind.Absolute,
                    out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                requestStatus = "请输入有效的 HTTP/HTTPS 原图 URL。";
                return;
            }

            requestStatus = "正在下载原图…";
            BeginRequest(
                UnityWebRequest.Get(uri.AbsoluteUri),
                bytes => SaveReferenceImage(bytes, "Remote"));
        }

        private void BeginRequest(
            UnityWebRequest request,
            Action<byte[]> onSuccess)
        {
            requestInProgress = true;
            UnityWebRequestAsyncOperation operation =
                request.SendWebRequest();

            void Poll()
            {
                if (!operation.isDone)
                {
                    return;
                }

                EditorApplication.update -= Poll;
                requestInProgress = false;
                try
                {
                    if (request.result !=
                        UnityWebRequest.Result.Success)
                    {
                        requestStatus =
                            $"下载失败（HTTP {request.responseCode}）：" +
                            request.error;
                        return;
                    }

                    byte[] bytes = request.downloadHandler.data;
                    if (bytes == null || bytes.Length == 0)
                    {
                        requestStatus = "下载结果为空。";
                        return;
                    }

                    onSuccess(bytes);
                }
                catch (Exception exception)
                {
                    requestStatus = "处理下载结果失败：" +
                                    exception.Message;
                    Debug.LogException(exception);
                }
                finally
                {
                    request.Dispose();
                    Repaint();
                }
            }

            EditorApplication.update += Poll;
        }

        private void SaveReferenceImage(
            byte[] sourceBytes,
            string sourceName)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(sourceBytes, false))
                {
                    throw new InvalidOperationException(
                        "返回内容不是可识别的 PNG/JPG 图片。" +
                        "如果这是蓝湖链接，请传入导出的图片或原图直链，" +
                        "不要传分享页面地址。");
                }

                EnsureAssetFolder(ReferenceFolder);
                string safePanelId = GetSafePanelId();
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{ReferenceFolder}/{safePanelId}_{sourceName}.png");
                string absolutePath =
                    CSharpUIGenerator.ToAbsolutePath(assetPath);
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                ConfigureReferenceImageImporter(assetPath);
                referenceImage =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                requestStatus = "效果图已保存：" + assetPath;
                Selection.activeObject = referenceImage;
            }
            finally
            {
                DestroyImmediate(texture);
            }
        }

        private void CreateStarterSchema()
        {
            if (referenceImage == null)
            {
                return;
            }

            string safePanelId = GetSafePanelId();
            string imagePath =
                AssetDatabase.GetAssetPath(referenceImage);
            var schema = new UIEffectSchema
            {
                name = safePanelId,
                designWidth = referenceImage.width,
                designHeight = referenceImage.height,
                referenceImage = imagePath,
            };
            schema.children.Add(new UIEffectNode
            {
                name = "Background",
                type = "Image",
                semantic = "background",
                anchor = "StretchAll",
                x = 0f,
                y = 0f,
                width = referenceImage.width,
                height = referenceImage.height,
                color = "#303744FF",
                intentionalColor = true,
            });
            schema.children.Add(new UIEffectNode
            {
                name = "Title",
                type = "Text",
                semantic = "title",
                anchor = "TopCenter",
                x = referenceImage.width * 0.25f,
                y = referenceImage.height * 0.06f,
                width = referenceImage.width * 0.5f,
                height = referenceImage.height * 0.08f,
                text = "请让 $unity-ui-generator 按效果图补全 UISchema",
                fontSize = Mathf.Max(24f, referenceImage.width * 0.03f),
                alignment = "Center",
                color = "#FFFFFFFF",
                bold = true,
            });

            EnsureAssetFolder(SchemaFolder);
            string assetPath =
                $"{SchemaFolder}/{safePanelId}.json";
            if (File.Exists(CSharpUIGenerator.ToAbsolutePath(assetPath)) &&
                !EditorUtility.DisplayDialog(
                    "UISchema 已存在",
                    assetPath + " 已存在，是否覆盖？",
                    "覆盖",
                    "取消"))
            {
                return;
            }

            File.WriteAllText(
                CSharpUIGenerator.ToAbsolutePath(assetPath),
                UIEffectSchemaUtility.ToCompactJson(schema),
                new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport);
            schemaAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            Selection.activeObject = schemaAsset;
            requestStatus = "基础 UISchema 已创建：" + assetPath;
        }

        private void GeneratePrefab()
        {
            try
            {
                var options = new UIEffectPrefabGenerationOptions
                {
                    panelId = GetSafePanelId(),
                    prefabFolder = prefabFolder,
                    scriptType = scriptType,
                    codeNamespace = codeNamespace,
                    logicClassName = string.IsNullOrWhiteSpace(
                        logicClassName)
                        ? GetSafePanelId()
                        : logicClassName,
                    scriptFolder = scriptFolder,
                    uiLayer = uiLayer,
                    resourceSearchRoots = ParseSearchRoots(
                        resourceSearchRoots),
                };
                UIEffectPrefabGenerationResult result =
                    UIEffectPrefabBuilder.Generate(
                        schemaAsset.text,
                        options,
                        true);
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        result.PrefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);

                string used = result.UsedResources.Count == 0
                    ? "无"
                    : string.Join("\n", result.UsedResources);
                string missing = result.MissingResources.Count == 0
                    ? "无"
                    : string.Join("\n", result.MissingResources);
                Debug.Log(
                    $"[UI Effect Generator] Prefab：{result.PrefabPath}\n" +
                    $"已匹配资源：\n{used}\n" +
                    $"缺失资源：\n{missing}",
                    prefab);
                requestStatus =
                    $"生成完成：{result.PrefabPath}；" +
                    $"已匹配 {result.UsedResources.Count} 个资源，" +
                    $"缺失 {result.MissingResources.Count} 个资源。";
            }
            catch (OperationCanceledException)
            {
                requestStatus = "已取消生成。";
            }
            catch (Exception exception)
            {
                requestStatus = "生成失败：" + exception.Message;
                Debug.LogException(exception);
            }
        }

        [MenuItem(
            "Assets/UI工具/根据选中的UISchema生成Prefab",
            true)]
        private static bool ValidateGenerateSelectedSchema()
        {
            return Selection.activeObject is TextAsset &&
                   AssetDatabase.GetAssetPath(
                           Selection.activeObject)
                       .EndsWith(
                           ".json",
                           StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem("Assets/UI工具/根据选中的UISchema生成Prefab")]
        private static void GenerateSelectedSchema()
        {
            string schemaPath =
                AssetDatabase.GetAssetPath(Selection.activeObject);
            UIEffectSchema schema = UIEffectSchemaUtility.Parse(
                ((TextAsset)Selection.activeObject).text);
            UIEffectPrefabGenerationResult result =
                UIEffectPrefabBuilder.GenerateFromSchemaPath(
                    schemaPath,
                    new UIEffectPrefabGenerationOptions
                    {
                        panelId = schema.name,
                        logicClassName =
                            CSharpUIGenerator.SanitizeTypeName(
                                schema.name),
                    },
                    true);
            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    result.PrefabPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Assets/UI工具/压缩选中的UISchema（低Token）", true)]
        private static bool ValidateOptimizeSelectedSchema()
        {
            return ValidateGenerateSelectedSchema();
        }

        [MenuItem("Assets/UI工具/压缩选中的UISchema（低Token）")]
        private static void OptimizeSelectedSchema()
        {
            string assetPath =
                AssetDatabase.GetAssetPath(Selection.activeObject);
            OptimizeSchemaAsset(assetPath, out int collapsedNodes);
            Debug.Log(
                $"[UI Effect Generator] 已压缩 {assetPath}，" +
                $"合并重复节点 {collapsedNodes} 个。",
                Selection.activeObject);
        }

        [MenuItem("工具/UI工具/压缩所有UISchema（低Token）")]
        private static void OptimizeProjectSchemasMenu()
        {
            if (EditorUtility.DisplayDialog(
                    "压缩 UISchema",
                    "将省略默认字段，并自动合并可识别的重复节点。继续吗？",
                    "压缩",
                    "取消"))
            {
                OptimizeProjectSchemas();
            }
        }

        public static void OptimizeProjectSchemas()
        {
            int optimizedFiles = 0;
            int collapsedNodes = 0;
            string[] guids = AssetDatabase.FindAssets(
                "t:TextAsset",
                new[] { SchemaFolder });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(
                        ".json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                OptimizeSchemaAsset(assetPath, out int collapsed);
                optimizedFiles++;
                collapsedNodes += collapsed;
            }

            AssetDatabase.Refresh();
            Debug.Log(
                $"[UI Effect Generator] 已压缩 {optimizedFiles} 个 " +
                $"UISchema，共合并重复节点 {collapsedNodes} 个。");
        }

        private static void OptimizeSchemaAsset(
            string assetPath,
            out int collapsedNodes)
        {
            TextAsset asset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"找不到 UISchema：{assetPath}");
            }

            UIEffectSchema schema =
                UIEffectSchemaUtility.Parse(asset.text);
            collapsedNodes =
                UIEffectSchemaUtility.OptimizeRepeatedNodes(schema);
            File.WriteAllText(
                CSharpUIGenerator.ToAbsolutePath(assetPath),
                UIEffectSchemaUtility.ToCompactJson(schema),
                new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private string GetSafePanelId()
        {
            string value = string.IsNullOrWhiteSpace(panelId)
                ? "UIEffectPanel"
                : panelId.Trim();
            return CSharpUIGenerator.SanitizeTypeName(value);
        }

        private static string[] ParseSearchRoots(string value)
        {
            return (value ?? string.Empty)
                .Split(
                    new[] { '\r', '\n', ';', ',' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
        }

        private static bool TryParseFigmaUrl(
            string url,
            out string fileKey,
            out string nodeId,
            out string error)
        {
            fileKey = string.Empty;
            nodeId = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                error = "请输入带 node-id 的 Figma 节点链接。";
                return false;
            }

            Match fileMatch = Regex.Match(
                url,
                @"figma\.com/(?:file|design|proto|board)/([^/?#]+)",
                RegexOptions.IgnoreCase);
            Match nodeMatch = Regex.Match(
                url,
                @"[?&]node-id=([^&#]+)",
                RegexOptions.IgnoreCase);
            if (!fileMatch.Success || !nodeMatch.Success)
            {
                error =
                    "无法从链接提取 Figma file key 或 node-id。" +
                    "请在 Figma 中复制目标 Frame/Component 的链接。";
                return false;
            }

            fileKey = Uri.UnescapeDataString(
                fileMatch.Groups[1].Value);
            nodeId = Uri.UnescapeDataString(
                    nodeMatch.Groups[1].Value.Replace("+", " "))
                .Replace('-', ':');
            return true;
        }

        private static string ExtractFigmaImageUrl(
            string json,
            string nodeId)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            MatchCollection matches = Regex.Matches(
                json,
                "\\\"(?<key>[^\\\"]+)\\\"\\s*:\\s*" +
                "\\\"(?<url>https?:[^\\\"]+)\\\"");
            foreach (Match match in matches)
            {
                string key = DecodeJsonString(
                    match.Groups["key"].Value);
                if (string.Equals(
                        key,
                        nodeId,
                        StringComparison.Ordinal))
                {
                    return DecodeJsonString(
                        match.Groups["url"].Value);
                }
            }

            return string.Empty;
        }

        private static string DecodeJsonString(string value)
        {
            return Regex.Unescape(value.Replace("\\/", "/"));
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = assetFolder
                .Replace('\\', '/')
                .TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] segments = normalized.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        segments[index]);
                }

                current = next;
            }
        }

        private static void ConfigureReferenceImageImporter(
            string assetPath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 8192;
            importer.SaveAndReimport();
        }
    }
}
#endif
