require("Framework.Core.DefineClass")
local SceneType = require("Managers.Scene.SceneType")
local SceneLoadManager = require("Managers.Scene.SceneLoadManager")
local SceneLoadingView = require("Managers.Scene.SceneLoadingView")

local SceneMgr = DefineClass("SceneMgr")

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

function SceneMgr:ctor()
    self._initialized = false
    self._sceneClasses = {}
    self._currentScene = nil
end

function SceneMgr:Initialize()
    if self._initialized then
        return
    end

    self:RegisterScene(
        SceneType.Login,
        require("Managers.Scene.SceneLogin"))
    self._initialized = true
end

function SceneMgr:GameStart()
    self:ChangeScene(SceneType.Login)
end

function SceneMgr:RegisterScene(sceneType, sceneClass)
    assert(type(sceneType) == "string" and sceneType ~= "",
        "sceneType must be a non-empty string")
    assert(type(sceneClass) == "table" and type(sceneClass.new) == "function",
        "sceneClass must be created by DefineClass")
    self._sceneClasses[sceneType] = sceneClass
end

function SceneMgr:UnregisterScene(sceneType)
    self._sceneClasses[sceneType] = nil
end

---Lua VM 在 Login 场景加载完成后启动，因此首次只接管当前场景，不重复加载。
---@return boolean, table|string
function SceneMgr:AttachCurrentScene(enterData)
    self:Initialize()

    local activeSceneName =
        CS.LxyDemo.SceneManagement.GameSceneManager.Instance.ActiveSceneName
    local sceneType, sceneClass = self:_FindBySceneName(activeSceneName)
    if not sceneClass then
        return false, "Lua 未注册当前场景：" .. tostring(activeSceneName)
    end

    if self._currentScene and self._currentScene.Type == sceneType then
        return true, self._currentScene
    end

    if self._currentScene then
        self._currentScene:Destroy()
    end

    local scene = sceneClass.new()
    self._currentScene = scene

    local succeeded, errorMessage = xpcall(function()
        scene:EnterScene(enterData)
        scene:LoadingFinish()
    end, debug.traceback)

    if not succeeded then
        scene:Destroy()
        self._currentScene = nil
        return false, tostring(errorMessage)
    end

    CS.UnityEngine.Debug.Log(
        "[LuaScene] attached active scene: " .. activeSceneName)
    return true, scene
end

---@param sceneType string SceneType 中注册的类型
---@param enterData any
---@param callback function|nil callback(succeeded, errorMessage, scene)
---@param forceReload boolean|nil
---@return boolean
function SceneMgr:ChangeScene(sceneType, enterData, callback, forceReload)
    self:Initialize()

    local sceneClass = self._sceneClasses[sceneType]
    if not sceneClass then
        local message = "未注册的 Lua 场景类型：" .. tostring(sceneType)
        safeCall(callback, false, message, nil)
        return false
    end

    if SceneLoadManager:IsLoading() then
        local message = "已有场景正在加载。"
        safeCall(callback, false, message, nil)
        return false
    end

    if not forceReload and self._currentScene and
        self._currentScene.Type == sceneType then
        safeCall(callback, true, nil, self._currentScene)
        return true
    end

    local previousScene = self._currentScene
    if previousScene then
        previousScene:Destroy()
    end

    local nextScene = sceneClass.new()
    self._currentScene = nextScene

    local accepted = nextScene:StartLoad(enterData,
        function(succeeded, errorMessage)
            if not succeeded then
                if self._currentScene == nextScene then
                    nextScene:Destroy()
                    self._currentScene = nil
                end
                safeCall(callback, false, errorMessage, nil)
                return
            end

            safeCall(callback, true, nil, nextScene)
        end)

    if not accepted and self._currentScene == nextScene and
        not SceneLoadManager:IsLoading() then
        nextScene:Destroy()
        self._currentScene = nil
    end

    return accepted
end

function SceneMgr:ChangeSceneByName(
    sceneName,
    enterData,
    callback,
    forceReload)
    self:Initialize()
    local sceneType = self:_FindBySceneName(sceneName)
    if not sceneType then
        local message = "未注册的 Unity 场景：" .. tostring(sceneName)
        safeCall(callback, false, message, nil)
        return false
    end

    return self:ChangeScene(
        sceneType,
        enterData,
        callback,
        forceReload)
end

function SceneMgr:GetCurrentScene()
    return self._currentScene
end

function SceneMgr:GetCurrentSceneType()
    return self._currentScene and self._currentScene.Type or nil
end

function SceneMgr:IsCurrentScene(sceneType)
    return self:GetCurrentSceneType() == sceneType
end

function SceneMgr:IsLoading()
    return SceneLoadManager:IsLoading()
end

function SceneMgr:GetLoadingProgress()
    return SceneLoadManager:GetProgress()
end

function SceneMgr:GetLoadingView()
    return SceneLoadingView
end

function SceneMgr:Shutdown()
    if self._currentScene then
        self._currentScene:Destroy()
        self._currentScene = nil
    end

    SceneLoadManager:Stop()
end

function SceneMgr:_FindBySceneName(sceneName)
    for sceneType, sceneClass in pairs(self._sceneClasses) do
        if sceneClass.SceneName == sceneName then
            return sceneType, sceneClass
        end
    end

    return nil, nil
end

local instance = SceneMgr.new()

-- 保留参考项目中常用的全局访问方式，同时推荐业务代码使用 require。
_G.SceneMgr = instance
_G.SceneType = SceneType

return instance
