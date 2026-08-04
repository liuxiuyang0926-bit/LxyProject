require("Framework.Core.DefineClass")

local SceneLoadingView = DefineClass("SceneLoadingView")

function SceneLoadingView:ctor()
    self._isLoading = false
    self._progress = 0
    self._sceneName = nil
    self._message = nil
    self._listener = nil
end

---设置加载状态监听器。
---listener(eventName, state) 中的 state 就是当前 SceneLoadingView。
---@param listener function|nil
function SceneLoadingView:SetListener(listener)
    assert(listener == nil or type(listener) == "function",
        "SceneLoadingView listener must be a function or nil")
    self._listener = listener
end

function SceneLoadingView:StartLoading(sceneName)
    self._isLoading = true
    self._progress = 0
    self._sceneName = sceneName
    self._message = nil
    self:_Notify("start")
end

function SceneLoadingView:SetProgress(progress)
    local value = tonumber(progress) or 0
    self._progress = math.max(0, math.min(1, value))
    self:_Notify("progress")
end

function SceneLoadingView:SetMessage(message)
    self._message = message
    self:_Notify("message")
end

function SceneLoadingView:Finish(succeeded, errorMessage)
    if succeeded then
        self._progress = 1
    end

    self._isLoading = false
    self._message = errorMessage
    self:_Notify(succeeded and "complete" or "failed")
end

function SceneLoadingView:Reset()
    self._isLoading = false
    self._progress = 0
    self._sceneName = nil
    self._message = nil
end

function SceneLoadingView:IsLoading()
    return self._isLoading
end

function SceneLoadingView:GetProgress()
    return self._progress
end

function SceneLoadingView:GetSceneName()
    return self._sceneName
end

function SceneLoadingView:GetMessage()
    return self._message
end

function SceneLoadingView:_Notify(eventName)
    if not self._listener then
        return
    end

    local succeeded, errorMessage = xpcall(function()
        self._listener(eventName, self)
    end, debug.traceback)

    if not succeeded then
        CS.UnityEngine.Debug.LogError(
            "[LuaScene] loading listener error: " .. tostring(errorMessage))
    end
end

return SceneLoadingView.new()
