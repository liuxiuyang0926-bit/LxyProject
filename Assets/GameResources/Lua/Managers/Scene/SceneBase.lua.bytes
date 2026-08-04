require("Framework.Core.DefineClass")
local SceneLoadManager = require("Managers.Scene.SceneLoadManager")

local SceneBase = DefineClass("SceneBase")

SceneBase.SceneName = ""
SceneBase.Type = nil

function SceneBase:ctor()
    self._isEntered = false
    self._isDestroyed = false
end

function SceneBase:StartLoad(enterData, callback)
    self._isDestroyed = false
    return SceneLoadManager:Start(self, enterData, callback)
end

---场景资源加载完成后的扩展点。
---需要预加载 YooAsset 资源时，在子类覆盖并在结束时调用 callback。
function SceneBase:PreloadAssets(callback)
    callback(true, nil)
end

function SceneBase:EnterScene(enterData)
    if self._isDestroyed then
        return
    end

    self._isEntered = true
    self:OnEnterScene(enterData)
end

function SceneBase:LoadingFinish()
    if self._isDestroyed then
        return
    end

    self:OnLoadingFinish()
end

function SceneBase:ExitScene()
    if not self._isEntered then
        return
    end

    self._isEntered = false
    self:OnExitScene()
end

function SceneBase:Destroy()
    if self._isDestroyed then
        return
    end

    SceneLoadManager:Stop(self)
    self:ExitScene()
    self._isDestroyed = true
end

function SceneBase:IsEntered()
    return self._isEntered
end

function SceneBase:OnEnterScene(enterData)
end

function SceneBase:OnLoadingFinish()
end

function SceneBase:OnExitScene()
end

return SceneBase
