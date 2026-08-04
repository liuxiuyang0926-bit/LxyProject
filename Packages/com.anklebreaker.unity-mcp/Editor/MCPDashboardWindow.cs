using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Editor window providing an overview of AB Unity MCP status, feature categories,
    /// server controls, queue monitoring, settings, and active agent sessions.
    /// Accessible via Window > AB Unity MCP.
    /// </summary>
    public class MCPDashboardWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private bool _settingsFoldout = false;
        private bool _agentsFoldout = true;
        private bool _categoriesFoldout = true;
        private bool _queueFoldout = true;
        private bool _contextFoldout = true;
        private bool _recentActionsFoldout = true;
        private string _expandedTestCategory = null;

        private static readonly Color ColorGreen  = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color ColorRed    = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color ColorYellow = new Color(0.9f, 0.8f, 0.1f);
        private static readonly Color ColorGrey   = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color ColorBlue   = new Color(0.4f, 0.7f, 1.0f);

        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _dotStyle;
        private bool _stylesInitialized;

        [MenuItem("Window/AB Unity MCP/控制面板")]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPDashboardWindow>("AB Unity MCP");
            window.minSize = new Vector2(340, 500);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };

            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
            };

            _dotStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = 22,
            };

            _stylesInitialized = true;
        }

        private void OnInspectorUpdate()
        {
            // Repaint periodically for live status
            Repaint();
        }

        private void OnGUI()
        {
            InitStyles();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(6);
            DrawConnectionStatus();
            EditorGUILayout.Space(4);
            DrawServerControls();
            EditorGUILayout.Space(8);
            DrawQueueStatus();
            EditorGUILayout.Space(8);
            DrawProjectContext();
            EditorGUILayout.Space(8);
            DrawAgentSessions();
            EditorGUILayout.Space(8);
            DrawRecentActions();
            EditorGUILayout.Space(8);
            DrawCategoryStatus();
            EditorGUILayout.Space(8);
            DrawSettings();
            EditorGUILayout.Space(8);
            DrawVersionInfo();

            EditorGUILayout.EndScrollView();
        }

        // ─── Header ───

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("AnkleBreaker Unity MCP 控制面板", _headerStyle, GUILayout.Height(28));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ─── Connection Status ───

        private void DrawConnectionStatus()
        {
            bool running = MCPBridgeServer.IsRunning;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Status dot
            var prevColor = GUI.color;
            GUI.color = running ? ColorGreen : ColorRed;
            GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
            GUI.color = prevColor;

            EditorGUILayout.LabelField(
                running ? "服务器运行中" : "服务器已停止",
                EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // Show actual active port when running, settings port when stopped
            int displayPort = running ? MCPBridgeServer.ActivePort : MCPSettingsManager.Port;
            string portLabel = running && !MCPSettingsManager.UseManualPort
                ? $"端口 {displayPort}（自动）"
                : $"端口 {displayPort}";
            EditorGUILayout.LabelField(portLabel, GUILayout.Width(100));

            // Cache values once per event to prevent Layout/Repaint mismatch.
            // Using local bools ensures the same controls exist in both passes.
            int agents = MCPRequestQueue.ActiveSessionCount;
            int queued = MCPRequestQueue.TotalQueuedCount;
            bool showAgents = agents > 0;
            bool showQueued = queued > 0;

            // Always draw the same number of controls regardless of state —
            // hide them with alpha when inactive to avoid IMGUI control count mismatch.
            var savedAlpha = GUI.color.a;

            // Agent count indicator
            GUI.color = showAgents ? ColorGreen : new Color(0, 0, 0, 0);
            GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
            GUI.color = showAgents ? new Color(prevColor.r, prevColor.g, prevColor.b, savedAlpha) : new Color(0, 0, 0, 0);
            EditorGUILayout.LabelField(showAgents ? $"{agents} 个代理" : "", GUILayout.Width(75));

            // Queue count indicator
            GUI.color = showQueued ? ColorYellow : new Color(0, 0, 0, 0);
            GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
            GUI.color = showQueued ? new Color(prevColor.r, prevColor.g, prevColor.b, savedAlpha) : new Color(0, 0, 0, 0);
            EditorGUILayout.LabelField(showQueued ? $"{queued} 个排队" : "", GUILayout.Width(75));

            GUI.color = prevColor;

            EditorGUILayout.EndHorizontal();

            // ParrelSync clone indicator (shown below the main status bar)
            if (MCPInstanceRegistry.IsParrelSyncClone())
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(24);
                var cloneStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = ColorBlue },
                    fontStyle = FontStyle.Italic,
                };
                int cloneIdx = MCPInstanceRegistry.GetParrelSyncCloneIndex();
                EditorGUILayout.LabelField(
                    $"\u2937 ParrelSync 克隆 #{cloneIdx}",
                    cloneStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        // ─── Server Controls ───

        private void DrawServerControls()
        {
            EditorGUILayout.BeginHorizontal();

            bool running = MCPBridgeServer.IsRunning;

            GUI.enabled = !running;
            if (GUILayout.Button("启动", GUILayout.Height(24)))
                MCPBridgeServer.Start();

            GUI.enabled = running;
            if (GUILayout.Button("停止", GUILayout.Height(24)))
                MCPBridgeServer.Stop();

            GUI.enabled = true;
            if (GUILayout.Button("重启", GUILayout.Height(24)))
            {
                MCPBridgeServer.Stop();
                EditorApplication.delayCall += () => MCPBridgeServer.Start();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─── Queue Status (Multi-Agent) ───

        private void DrawQueueStatus()
        {
            _queueFoldout = EditorGUILayout.Foldout(_queueFoldout, "请求队列", true, EditorStyles.foldoutHeader);
            if (!_queueFoldout) return;

            var queueInfo = MCPRequestQueue.GetQueueInfo();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Summary row
            EditorGUILayout.BeginHorizontal();

            int totalQueued = 0;
            if (queueInfo.ContainsKey("totalQueued"))
                int.TryParse(queueInfo["totalQueued"].ToString(), out totalQueued);

            int activeAgents = 0;
            if (queueInfo.ContainsKey("activeAgents"))
                int.TryParse(queueInfo["activeAgents"].ToString(), out activeAgents);

            int cacheSize = 0;
            if (queueInfo.ContainsKey("completedCacheSize"))
                int.TryParse(queueInfo["completedCacheSize"].ToString(), out cacheSize);

            var prevColor = GUI.color;
            GUI.color = totalQueued > 0 ? ColorYellow : ColorGreen;
            GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
            GUI.color = prevColor;

            string statusText = totalQueued > 0
                ? $"{totalQueued} 个等待  |  {activeAgents} 个代理  |  {cacheSize} 个缓存"
                : $"空闲  |  {activeAgents} 个代理  |  {cacheSize} 个缓存";
            EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            // Per-agent breakdown (if any queued)
            if (queueInfo.ContainsKey("perAgentQueued") && queueInfo["perAgentQueued"] is Dictionary<string, object> perAgent)
            {
                if (perAgent.Count > 0)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("各代理队列深度：", EditorStyles.miniLabel);

                    foreach (var kvp in perAgent)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(24);

                        int depth = 0;
                        int.TryParse(kvp.Value.ToString(), out depth);

                        var agentColor = depth > 0 ? ColorYellow : ColorGreen;
                        GUI.color = agentColor;
                        GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
                        GUI.color = prevColor;

                        EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(160));
                        EditorGUILayout.LabelField($"{depth} 个等待", GUILayout.Width(80));

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ─── Project Context ───

        private void DrawProjectContext()
        {
            _contextFoldout = EditorGUILayout.Foldout(_contextFoldout, "项目上下文", true, EditorStyles.foldoutHeader);
            if (!_contextFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Enabled toggle
            EditorGUILayout.BeginHorizontal();
            bool enabled = EditorGUILayout.Toggle("启用上下文", MCPSettingsManager.ContextEnabled);
            if (enabled != MCPSettingsManager.ContextEnabled)
                MCPSettingsManager.ContextEnabled = enabled;
            GUILayout.FlexibleSpace();

            // Buttons
            if (GUILayout.Button("创建模板", GUILayout.Width(110), GUILayout.Height(18)))
            {
                int created = MCPContextManager.CreateDefaultTemplates();
                if (created > 0)
                    EditorUtility.DisplayDialog("模板创建完成",
                        $"已在以下目录创建 {created} 个模板文件：\n{MCPSettingsManager.ContextPath}", "确定");
                else
                    EditorUtility.DisplayDialog("模板已存在",
                        "所有模板文件都已存在。", "确定");
            }

            if (GUILayout.Button("打开文件夹", GUILayout.Width(90), GUILayout.Height(18)))
            {
                string folderPath = MCPContextManager.GetContextFolderPath();
                if (System.IO.Directory.Exists(folderPath))
                    EditorUtility.RevealInFinder(folderPath);
                else
                    EditorUtility.DisplayDialog("未找到文件夹",
                        $"上下文文件夹尚不存在。\n请点击“创建模板”进行初始化。\n\n{folderPath}", "确定");
            }

            EditorGUILayout.EndHorizontal();

            if (!enabled)
            {
                EditorGUILayout.HelpBox("项目上下文已禁用，代理将无法获取项目说明文档。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // Path display
            EditorGUILayout.LabelField("路径：", MCPSettingsManager.ContextPath, EditorStyles.miniLabel);

            // File list
            var files = MCPContextManager.GetContextFileList();
            bool anyFiles = false;

            foreach (var file in files)
            {
                if (!file.IsStandard && !file.Exists) continue; // Don't show missing custom files

                anyFiles = true;
                EditorGUILayout.BeginHorizontal();

                var prevColor = GUI.color;
                if (file.Exists && file.SizeBytes > 0)
                    GUI.color = ColorGreen;
                else if (file.Exists)
                    GUI.color = ColorYellow;
                else
                    GUI.color = ColorGrey;

                GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
                GUI.color = prevColor;

                string displayName = file.Category;
                EditorGUILayout.LabelField(displayName, GUILayout.MinWidth(140));

                if (file.Exists)
                {
                    string sizeLabel = file.SizeBytes > 1024
                        ? $"{file.SizeBytes / 1024f:0.#} KB"
                        : $"{file.SizeBytes} B";
                    EditorGUILayout.LabelField(
                        file.SizeBytes == 0 ? "空文件" : sizeLabel,
                        EditorStyles.miniLabel, GUILayout.Width(60));
                }
                else
                {
                    EditorGUILayout.LabelField("未创建", EditorStyles.miniLabel, GUILayout.Width(60));
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (!anyFiles)
            {
                EditorGUILayout.HelpBox(
                    "未找到上下文文件，请点击“创建模板”开始使用。",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        // ─── Recent Actions ───

        private void DrawRecentActions()
        {
            _recentActionsFoldout = EditorGUILayout.Foldout(_recentActionsFoldout, "最近操作", true, EditorStyles.foldoutHeader);
            if (!_recentActionsFoldout) return;

            var recent = MCPActionHistory.GetRecent(8);

            if (recent.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无操作记录。", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Show newest first
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                var r = recent[i];
                EditorGUILayout.BeginHorizontal();

                // Status dot
                var prevColor = GUI.color;
                Color dotColor;
                switch (r.Status)
                {
                    case "Completed": dotColor = ColorGreen; break;
                    case "Failed":    dotColor = ColorRed;   break;
                    default:          dotColor = ColorYellow; break;
                }
                GUI.color = dotColor;
                GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
                GUI.color = prevColor;

                // Timestamp
                EditorGUILayout.LabelField(r.Timestamp.ToString("HH:mm:ss"),
                    EditorStyles.miniLabel, GUILayout.Width(55));

                // Agent (short)
                string agent = r.AgentId ?? "?";
                if (agent.Length > 10) agent = agent.Substring(0, 8) + "..";
                prevColor = GUI.color;
                GUI.color = ColorBlue;
                EditorGUILayout.LabelField(agent, EditorStyles.miniLabel, GUILayout.Width(65));
                GUI.color = prevColor;

                // Action command
                string cmd = MCPActionRecord.ExtractCommand(r.ActionName);
                EditorGUILayout.LabelField(cmd, EditorStyles.miniLabel, GUILayout.Width(100));

                // Target (truncated)
                string target = r.TargetPath ?? "";
                if (target.Length > 25)
                    target = ".." + target.Substring(target.Length - 23);
                EditorGUILayout.LabelField(target, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            // Open full history button
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            string btnLabel = MCPActionHistory.Count > 8
                ? $"打开完整历史（{MCPActionHistory.Count} 条操作）"
                : "打开完整历史";
            if (GUILayout.Button(btnLabel, GUILayout.Width(200), GUILayout.Height(20)))
            {
                MCPActionHistoryWindow.ShowWindow();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ─── Feature Categories + Test Status ───

        private void DrawCategoryStatus()
        {
            _categoriesFoldout = EditorGUILayout.Foldout(_categoriesFoldout, "功能分类", true, EditorStyles.foldoutHeader);
            if (!_categoriesFoldout) return;

            // Test controls bar
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Summary
            int passed = MCPSelfTest.PassedCount;
            int failed = MCPSelfTest.FailedCount;
            int warnings = MCPSelfTest.WarningCount;
            int total = MCPSettingsManager.GetAllCategoryNames().Length;

            if (MCPSelfTest.IsRunning)
            {
                EditorGUILayout.LabelField(
                    $"正在测试：{GetCategoryDisplayName(MCPSelfTest.CurrentCategory)}……",
                    EditorStyles.miniLabel);
                var rect = GUILayoutUtility.GetRect(100, 16, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, MCPSelfTest.Progress, $"{(int)(MCPSelfTest.Progress * 100)}%");
            }
            else if (MCPSelfTest.LastRunTime > System.DateTime.MinValue)
            {
                string summary = "";
                if (failed > 0)
                    summary += $"<color=#E63333>{failed} 个失败</color>  ";
                if (warnings > 0)
                    summary += $"<color=#E6CC11>{warnings} 个警告</color>  ";
                summary += $"<color=#33CC33>{passed}/{total} 个通过</color>";

                var richStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                EditorGUILayout.LabelField(summary, richStyle, GUILayout.ExpandWidth(true));
            }
            else
            {
                EditorGUILayout.LabelField("尚未运行测试", EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = !MCPSelfTest.IsRunning && MCPBridgeServer.IsRunning;
            if (GUILayout.Button("运行测试", GUILayout.Width(80), GUILayout.Height(20)))
            {
                MCPSelfTest.RunAllAsync();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Category rows
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string[] categories = MCPSettingsManager.GetAllCategoryNames();
            foreach (var cat in categories)
            {
                bool enabled = MCPSettingsManager.IsCategoryEnabled(cat);
                var testResult = MCPSelfTest.GetResult(cat);

                EditorGUILayout.BeginHorizontal();

                // Status dot — reflects test status when available, else enabled/disabled
                var prevColor = GUI.color;
                Color dotColor = GetCategoryDotColor(enabled, testResult);
                GUI.color = dotColor;
                GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
                GUI.color = prevColor;

                // Pretty name
                string displayName = GetCategoryDisplayName(cat);
                EditorGUILayout.LabelField(displayName, GUILayout.Width(100));

                // Test status label — always draw both controls to avoid IMGUI control count mismatch
                bool hasTested = testResult != null && testResult.Status != MCPTestResult.TestStatus.Untested;
                bool hasDetails = hasTested && (testResult.Status == MCPTestResult.TestStatus.Failed ||
                    testResult.Status == MCPTestResult.TestStatus.Warning);

                if (hasTested)
                {
                    string statusLabel = GetTestStatusText(testResult);
                    var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = dotColor },
                    };
                    EditorGUILayout.LabelField(statusLabel, statusStyle, GUILayout.Width(90));
                }
                else
                {
                    EditorGUILayout.LabelField("\u2014", EditorStyles.miniLabel, GUILayout.Width(90));
                }

                // Always draw the details button to keep control count stable
                if (hasDetails)
                {
                    if (GUILayout.Button("?", GUILayout.Width(20), GUILayout.Height(16)))
                    {
                        _expandedTestCategory = _expandedTestCategory == cat ? null : cat;
                    }
                }
                else
                {
                    // Invisible placeholder — same control, no visual
                    var transparent = GUI.color;
                    GUI.color = new Color(0, 0, 0, 0);
                    GUILayout.Button("", GUILayout.Width(20), GUILayout.Height(16));
                    GUI.color = transparent;
                }

                GUILayout.FlexibleSpace();

                bool newEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(30));
                if (newEnabled != enabled)
                    MCPSettingsManager.SetCategoryEnabled(cat, newEnabled);

                EditorGUILayout.EndHorizontal();

                // Expanded error details
                if (_expandedTestCategory == cat && testResult != null &&
                    !string.IsNullOrEmpty(testResult.Details))
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.SelectableLabel(
                        testResult.Details,
                        EditorStyles.wordWrappedMiniLabel,
                        GUILayout.MinHeight(36));
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private string GetCategoryDisplayName(string category)
        {
            if (string.IsNullOrEmpty(category)) return "未知";

            switch (category.ToLowerInvariant())
            {
                case "amplify": return "Amplify Shader";
                case "animation": return "动画";
                case "asmdef": return "程序集定义";
                case "asset": return "资源";
                case "assets": return "资源";
                case "audio": return "音频";
                case "build": return "构建";
                case "component": return "组件";
                case "console": return "控制台";
                case "editor": return "编辑器";
                case "gameobject": return "游戏对象";
                case "hierarchy": return "层级";
                case "lighting": return "灯光";
                case "material": return "材质";
                case "package": return "包管理";
                case "physics": return "物理";
                case "prefab": return "预制体";
                case "project": return "项目";
                case "scene": return "场景";
                case "script": return "脚本";
                case "shader": return "着色器";
                case "sprite": return "精灵图";
                case "terrain": return "地形";
                case "texture": return "纹理";
                case "ui": return "用户界面";
                default: return char.ToUpper(category[0]) + category.Substring(1);
            }
        }

        private Color GetCategoryDotColor(bool enabled, MCPTestResult result)
        {
            if (!enabled) return ColorGrey;
            if (result == null || result.Status == MCPTestResult.TestStatus.Untested)
                return enabled ? ColorGreen : ColorGrey;

            switch (result.Status)
            {
                case MCPTestResult.TestStatus.Passed:  return ColorGreen;
                case MCPTestResult.TestStatus.Warning: return ColorYellow;
                case MCPTestResult.TestStatus.Failed:  return ColorRed;
                default: return ColorGrey;
            }
        }

        private string GetTestStatusText(MCPTestResult result)
        {
            switch (result.Status)
            {
                case MCPTestResult.TestStatus.Passed:
                    return $"\u2713 {result.DurationMs:0}ms";
                case MCPTestResult.TestStatus.Warning:
                    return $"\u26A0 {result.Message}";
                case MCPTestResult.TestStatus.Failed:
                    return $"\u2717 {result.Message}";
                default:
                    return "\u2014";
            }
        }

        // ─── Agent Sessions ───

        private void DrawAgentSessions()
        {
            _agentsFoldout = EditorGUILayout.Foldout(_agentsFoldout, "活动代理会话", true, EditorStyles.foldoutHeader);
            if (!_agentsFoldout) return;

            var sessions = MCPRequestQueue.GetActiveSessions();

            if (sessions.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有活动代理会话。", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (var session in sessions)
            {
                EditorGUILayout.BeginHorizontal();

                var prevColor = GUI.color;
                GUI.color = ColorGreen;
                GUILayout.Label("\u25CF", _dotStyle, GUILayout.Width(22));
                GUI.color = prevColor;

                string agentId = session.ContainsKey("agentId") ? session["agentId"].ToString() : "?";
                string action = session.ContainsKey("currentAction") ? session["currentAction"].ToString() : "空闲";
                object totalObj = session.ContainsKey("totalActions") ? session["totalActions"] : 0;
                object queuedObj = session.ContainsKey("queuedRequests") ? session["queuedRequests"] : 0;
                object completedObj = session.ContainsKey("completedRequests") ? session["completedRequests"] : 0;
                object avgMs = session.ContainsKey("averageResponseTimeMs") ? session["averageResponseTimeMs"] : 0;

                EditorGUILayout.LabelField(agentId, EditorStyles.boldLabel, GUILayout.Width(160));
                EditorGUILayout.LabelField(action, GUILayout.MinWidth(80));
                GUILayout.FlexibleSpace();

                // Queue + completed stats
                int queuedInt = 0;
                int.TryParse(queuedObj.ToString(), out queuedInt);

                var richStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                string stats = $"共 {totalObj} 次";
                if (queuedInt > 0)
                    stats += $"  <color=#E6CC11>{queuedInt} 排队</color>";
                stats += $"  <color=#33CC33>{completedObj} 完成</color>";
                stats += $"  {avgMs}ms";

                EditorGUILayout.LabelField(stats, richStyle, GUILayout.Width(170));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // ─── Settings ───

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "设置", true, EditorStyles.foldoutHeader);
            if (!_settingsFoldout) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ─── General ───
            EditorGUILayout.LabelField("常规", EditorStyles.boldLabel);

            bool autoStart = EditorGUILayout.Toggle("Unity 编辑器启动时自动运行", MCPSettingsManager.AutoStart);
            if (autoStart != MCPSettingsManager.AutoStart)
                MCPSettingsManager.AutoStart = autoStart;

            EditorGUILayout.Space(6);

            // ─── Port ───
            EditorGUILayout.LabelField("端口", EditorStyles.boldLabel);

            bool useManual = EditorGUILayout.Toggle("使用手动端口", MCPSettingsManager.UseManualPort);
            if (useManual != MCPSettingsManager.UseManualPort)
                MCPSettingsManager.UseManualPort = useManual;

            if (useManual)
            {
                // Manual port entry
                EditorGUILayout.BeginHorizontal();
                int port = EditorGUILayout.IntField("服务器端口", MCPSettingsManager.Port);
                if (port != MCPSettingsManager.Port && port > 1024 && port < 65536)
                {
                    MCPSettingsManager.Port = port;
                }
                EditorGUILayout.EndHorizontal();

                if (MCPBridgeServer.IsRunning && MCPBridgeServer.ActivePort != MCPSettingsManager.Port)
                    EditorGUILayout.HelpBox("请重启服务器以应用端口更改。", MessageType.Info);
            }
            else
            {
                // Auto-select info
                string autoInfo = MCPBridgeServer.IsRunning
                    ? $"已自动选择端口 {MCPBridgeServer.ActivePort}（范围：{MCPInstanceRegistry.PortRangeStart}-{MCPInstanceRegistry.PortRangeEnd}）"
                    : $"将从 {MCPInstanceRegistry.PortRangeStart}-{MCPInstanceRegistry.PortRangeEnd} 范围内自动选择端口";
                EditorGUILayout.HelpBox(autoInfo, MessageType.None);
            }

            EditorGUILayout.Space(6);

            // ─── Multiplayer Play Mode (MPPM) ───
            EditorGUILayout.LabelField("多人游戏模式（MPPM）", EditorStyles.boldLabel);

            // Start on MPPM Virtual Players
            bool startOnVP = EditorGUILayout.Toggle(
                new GUIContent("在虚拟玩家中启动",
                    "关闭后，MCP Bridge 不会在多人游戏模式的虚拟玩家中自动启动，" +
                    "只会在主编辑器中自动启动；仍可手动启动。"),
                MCPSettingsManager.StartOnVirtualPlayers);
            if (startOnVP != MCPSettingsManager.StartOnVirtualPlayers)
                MCPSettingsManager.StartOnVirtualPlayers = startOnVP;

            EditorGUILayout.Space(4);

            // Reset button
            if (GUILayout.Button("将所有设置恢复为默认值"))
            {
                if (EditorUtility.DisplayDialog("重置设置",
                    "确定将所有 MCP 设置恢复为默认值吗？", "重置", "取消"))
                {
                    MCPSettingsManager.ResetToDefaults();
                }
            }

            EditorGUILayout.EndVertical();
        }

        // ─── Version Info ───

        private void DrawVersionInfo()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"插件版本：{MCPUpdateChecker.CurrentVersion}", GUILayout.Width(165));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("检查更新", GUILayout.Width(130)))
            {
                MCPUpdateChecker.CheckForUpdates((hasUpdate, latestVersion) =>
                {
                    if (hasUpdate)
                    {
                        EditorUtility.DisplayDialog("发现新版本",
                            $"发现新版本（{latestVersion}）。\n" +
                            "请通过 Unity Package Manager 更新。",
                            "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("已是最新版本",
                            "当前使用的已经是最新版本。", "确定");
                    }
                });
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
