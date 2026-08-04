require("Framework.Core.DefineClass")
local SceneLoadingView = require("Managers.Scene.SceneLoadingView")

local SceneLoadManager = DefineClass("SceneLoadManager")

local function safeCall(callback, ...)
    if not callback then
        return
    end

    local arguments = table.pack(...)
    local succeeded, errorMessage = xpcall(function()
        callback(table.unpack(arguments, 1, arguments.n))
    end, debug.traceback)

    if not succeeded then
        CS.UnityEngine.Debug.LogError(
            "[LuaScene] callback error: " .. tostring(errorMessage))
    end
end

function SceneLoadManager:ctor()
    self._requestId = 0
    self._isLoading = false
    self._progress = 0
    self._targetScene = nil
end

---通过 C# GameSceneManager 异步加载场景。
---@param scene table SceneBase 实例
---@param enterData any
---@param callback function|nil callback(succeeded, errorMessage)
---@return boolean
function SceneLoadManager:Start(scene, enterData, callback)
    if not scene or type(scene.SceneName) ~= "string" or scene.SceneName == "" then
        safeCall(callback, false, "SceneName 不能为空。")
        return false
    end

    if self._isLoading then
        local message = string.format(
            "正在加载场景 %s，不能同时加载 %s。",
            tostring(self._targetScene and self._targetScene.SceneName),
            scene.SceneName)
        safeCall(callback, false, message)
        return false
    end

    self._requestId = self._requestId + 1
    local requestId = self._requestId
    self._isLoading = true
    self._progress = 0
    self._targetScene = scene
    SceneLoadingView:StartLoading(scene.SceneName)

    local function isCurrentRequest()
        return self._requestId == requestId and self._targetScene == scene
    end

    local function finish(succeeded, errorMessage)
        if not isCurrentRequest() then
            return
        end

        self._isLoading = false
        self._targetScene = nil
        SceneLoadingView:Finish(succeeded, errorMessage)
        safeCall(callback, succeeded, errorMessage)
    end

    local function enterScene()
        if not isCurrentRequest() then
            return
        end

        local succeeded, errorMessage = xpcall(function()
            scene:EnterScene(enterData)
            scene:LoadingFinish()
        end, debug.traceback)

        if not succeeded then
            finish(false, tostring(errorMessage))
            return
        end

        finish(true, nil)
    end

    local function preloadFinished(succeeded, errorMessage)
        if not isCurrentRequest() then
            return
        end

        if succeeded == false then
            finish(false, errorMessage or "场景预加载失败。")
            return
        end

        enterScene()
    end

    local function sceneLoadCompleted(succeeded, errorMessage)
        if not isCurrentRequest() then
            return
        end

        if not succeeded then
            finish(false, errorMessage or "C# 场景加载失败。")
            return
        end

        local preloadSucceeded, preloadError = xpcall(function()
            scene:PreloadAssets(preloadFinished)
        end, debug.traceback)

        if not preloadSucceeded then
            finish(false, tostring(preloadError))
        end
    end

    local function sceneLoadProgress(progress)
        if not isCurrentRequest() then
            return
        end

        self._progress = math.max(0, math.min(1, tonumber(progress) or 0))
        SceneLoadingView:SetProgress(self._progress)
    end

    local invoked, acceptedOrError = xpcall(function()
        return CS.LxyDemo.SceneManagement.GameSceneManager.Instance:
            LoadSceneWithCallbacks(
                scene.SceneName,
                sceneLoadProgress,
                sceneLoadCompleted)
    end, debug.traceback)

    if not invoked then
        finish(false, tostring(acceptedOrError))
        return false
    end

    if acceptedOrError == false and self._isLoading then
        local csharpError = CS.LxyDemo.SceneManagement.GameSceneManager.Instance.LastError
        finish(false, csharpError or "C# 场景管理器拒绝了加载请求。")
        return false
    end

    return acceptedOrError ~= false
end

---使当前 Lua 请求失效。C# 已经开始的 Single 场景加载不会被强制中断。
function SceneLoadManager:Stop(scene)
    if scene and self._targetScene ~= scene then
        return
    end

    self._requestId = self._requestId + 1
    self._isLoading = false
    self._progress = 0
    self._targetScene = nil
    SceneLoadingView:Reset()
end

function SceneLoadManager:IsLoading()
    return self._isLoading
end

function SceneLoadManager:GetProgress()
    return self._progress
end

function SceneLoadManager:GetTargetScene()
    return self._targetScene
end

return SceneLoadManager.new()
