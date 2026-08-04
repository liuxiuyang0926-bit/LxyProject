require("Framework.Core.DefineClass")
local UIDefine = require("Framework.UI.UIDefine")
local UIPanelConfig = require("Framework.UI.UIPanelConfig")
local UIStackCoordinator =
    require("Framework.UI.UIStackCoordinator")
local UILogicFactory = require("Framework.UI.UILogicFactory")
local UILayerRoot = require("Framework.UI.UILayerRoot")
---@class UIManager: LuaClass
local UIManager = DefineClass("UIManager")

local PENDING_CLOSE_IMMEDIATE = {}

local function safeCall(callback, ...)
    if not callback then
        return
    end

    local ok, err = xpcall(
        callback,
        debug.traceback,
        ...)
    if not ok then
        CS.UnityEngine.Debug.LogError(
            "[LuaUI] callback failed:\n" .. tostring(err))
    end
end

local function copyArray(source)
    local result = {}
    for index, value in ipairs(source) do
        result[index] = value
    end
    return result
end

function UIManager:ctor(options)
    options = options or {}

    self.panelRecords = {}
    self.panelConfigs = {}
    self.stackRecords = {}
    self.savedPanelStacks = {}
    self.nextPanelRequestId = 0
    self.stackCoordinator = UIStackCoordinator.new()
    self.logicFactory = options.logicFactory
        or UILogicFactory.new()
    self.layerRoot = options.layerRoot
        and UILayerRoot.new(options.layerRoot)
        or nil
    self.customLoader = options.loadAsync
    self.customRelease = options.release
    self.closePanelsOnSceneChanged =
        options.closePanelsOnSceneChanged ~= false
    self.verboseLogging = options.verboseLogging == true
    self.shuttingDown = false
end

function UIManager:SetLayerRoot(csharpLayerRoot)
    self.layerRoot = UILayerRoot.new(csharpLayerRoot)
end

function UIManager:RegisterPanel(rawConfig)
    local config = rawConfig
    if type(rawConfig.GetEffectiveLayer) ~= "function" then
        config = UIPanelConfig.new(rawConfig)
    end
    config:Validate()

    local existing = self.panelRecords[config.Id]
    if existing and self:IsRecordAlive(existing) then
        error("cannot replace active panel config: " .. config.Id)
    end

    self.panelConfigs[config.Id] = config
    if existing then
        existing.config = config
    end
    return config
end

function UIManager:UnregisterPanel(configOrId)
    local config = self:ResolveConfig(configOrId, false)
    if not config then
        return false
    end

    local record = self.panelRecords[config.Id]
    if self:IsRecordAlive(record) then
        return false
    end

    self.panelConfigs[config.Id] = nil
    return true
end

function UIManager:ResolveConfig(configOrId, required)
    if type(configOrId) == "string" then
        local config = self.panelConfigs[configOrId]
        if required ~= false and not config then
            error("panel is not registered: " .. configOrId)
        end
        return config
    end

    if configOrId and configOrId.Id then
        return configOrId
    end

    if required ~= false then
        error("invalid panel config")
    end
    return nil
end

function UIManager:GetOrCreatePanelRecord(config)
    local record = self.panelRecords[config.Id]
    if record then
        record.config = config
        return record
    end

    record = {
        config = config,
        logic = nil,
        state = UIDefine.PanelState.None,
        requestId = 0,
        callbacks = {},
        userData = nil,
        showWhenLoaded = false,
        pendingCloseTarget = nil,
        closeForceDestroy = false,
        closeStopCloseAnim = false,
        backdrop = nil,
    }
    self.panelRecords[config.Id] = record
    return record
end

function UIManager:GetPanelRecord(configOrId)
    local config = self:ResolveConfig(configOrId, false)
    return config and self.panelRecords[config.Id] or nil
end

function UIManager:IsRecordAlive(record)
    return record ~= nil
        and record.logic ~= nil
        and not record.logic.destroyed
        and record.state ~= UIDefine.PanelState.Destroyed
        and record.state ~= UIDefine.PanelState.Failed
end

function UIManager:TransitionPanel(record, nextState, force)
    local previous = record.state
    if previous == nextState then
        return true
    end

    local allowed = UIDefine.AllowedTransitions[previous]
    if not force and (not allowed or not allowed[nextState]) then
        CS.UnityEngine.Debug.LogError(string.format(
            "[LuaUI] illegal state transition %s: %s -> %s",
            tostring(record.config.Id),
            tostring(previous),
            tostring(nextState)))
        return false
    end

    record.state = nextState
    if self.verboseLogging then
        CS.UnityEngine.Debug.Log(string.format(
            "[LuaUI] %s: %s -> %s",
            record.config.Id,
            tostring(previous),
            tostring(nextState)))
    end
    return true
end

function UIManager:AppendCallback(record, userData, callback)
    record.userData = userData
    if callback then
        table.insert(record.callbacks, callback)
    end
end

function UIManager:FlushCallbacks(record, succeeded, errorMessage)
    local callbacks = record.callbacks
    record.callbacks = {}
    for _, callback in ipairs(callbacks) do
        safeCall(callback, succeeded, errorMessage, record.logic)
    end
end

function UIManager:OpenUIPanel(configOrId, userData, callback)
    return self:OpenInternal(
        configOrId, userData, callback, true)
end

function UIManager:OpenInternal(
    configOrId, userData, callback, wantsVisible)
    assert(not self.shuttingDown, "LuaUIManager is shutting down")

    local config = self:ResolveConfig(configOrId, true)
    local record = self:GetOrCreatePanelRecord(config)
    self:AppendCallback(record, userData, callback)
    record.showWhenLoaded = wantsVisible == true

    if record.pendingCloseTarget ~= nil and wantsVisible then
        self:ClearPendingClose(record)
    end

    if record.state == UIDefine.PanelState.Loading
        or (record.state == UIDefine.PanelState.Closing
            and record.logic
            and not record.logic.isLoaded) then
        if wantsVisible then
            self:HandlePanelPush(record, userData, false)
            self:TransitionPanel(
                record, UIDefine.PanelState.Loading, true)
        end
        return record.logic
    end

    if self:IsRecordAlive(record) and record.logic.isLoaded then
        if wantsVisible then
            self:HandlePanelPush(record, userData, true)
            self:TryShowPanel(record, userData)
        else
            self:HidePanel(record, true)
            self:FlushCallbacks(record, true, nil)
        end
        return record.logic
    end

    local ok, logicOrError = xpcall(function()
        local logic = self.logicFactory:CreatePanelLogic(config)
        logic:SetManager(self)
        logic:InitLogic(userData)
        return logic
    end, debug.traceback)

    if not ok then
        self:HandleLoadFailed(record, logicOrError)
        return nil
    end

    record.logic = logicOrError
    record.userData = userData
    self:TransitionPanel(
        record, UIDefine.PanelState.Creating, true)

    if wantsVisible then
        self:HandlePanelPush(record, userData, false)
    end
    self:BeginPanelLoad(record)
    return record.logic
end

function UIManager:HandlePanelPush(record, userData, currentReady)
    local previousTop = self.stackCoordinator:PushPanel(
        self.stackRecords, record.config, userData)
    if not previousTop
        or previousTop.LogicConfig.Id == record.config.Id then
        return
    end

    local previousRecord =
        self:GetOrCreatePanelRecord(previousTop.LogicConfig)
    if currentReady then
        self:ApplyClosePolicy(previousRecord, false, false)
    else
        self:SetPendingClose(previousRecord, record.config)
    end
end

function UIManager:BeginPanelLoad(record)
    assert(self.layerRoot ~= nil,
        "LuaUIManager requires UILayerRoot before loading panels")

    self.nextPanelRequestId = self.nextPanelRequestId + 1
    record.requestId = self.nextPanelRequestId
    local requestId = record.requestId
    self:TransitionPanel(record, UIDefine.PanelState.Loading)

    local parent = self.layerRoot:GetUIParentRoot(
        record.config:GetEffectiveLayer())
    local completed = false
    local function complete(gameObject, errorMessage)
        if completed then
            if gameObject ~= nil then
                self:ReleaseInstance(record.config, gameObject)
            end
            return
        end
        completed = true
        self:OnPanelLoaded(
            record, requestId, gameObject, errorMessage)
    end

    local loader = record.config.LoadAsync or self.customLoader
    if loader then
        local ok, err = xpcall(function()
            loader(record.config, parent, complete)
        end, debug.traceback)
        if not ok then
            complete(nil, err)
        end
        return
    end

    if record.config.Prefab == nil then
        complete(nil,
            "no loader or local Prefab for " .. record.config.Id)
        return
    end

    local ok, instanceOrError = xpcall(function()
        return CS.UnityEngine.Object.Instantiate(
            record.config.Prefab, parent, false)
    end, debug.traceback)
    if ok then
        complete(instanceOrError, nil)
    else
        complete(nil, instanceOrError)
    end
end

function UIManager:OnPanelLoaded(
    record, requestId, gameObject, errorMessage)
    if record.requestId ~= requestId then
        if gameObject ~= nil then
            self:ReleaseInstance(record.config, gameObject)
        end
        return
    end

    if gameObject == nil then
        self:HandleLoadFailed(
            record, errorMessage or "Prefab load returned nil")
        return
    end

    if not self:IsRecordAlive(record) then
        self:ReleaseInstance(record.config, gameObject)
        return
    end

    local parent = self.layerRoot:GetUIParentRoot(
        record.config:GetEffectiveLayer())
    gameObject.name = record.config.Id
    gameObject.transform:SetParent(parent, false)
    gameObject.transform.localScale = CS.UnityEngine.Vector3.one

    local ok, bindError = xpcall(function()
        record.logic:BindGameObject(gameObject)
    end, debug.traceback)
    if not ok then
        self:ReleaseInstance(record.config, gameObject)
        self:HandleLoadFailed(record, bindError)
        return
    end

    if not record.showWhenLoaded then
        gameObject:SetActive(false)
        self:TransitionPanel(record, UIDefine.PanelState.Hidden)
        self:FlushCallbacks(record, true, nil)
        return
    end

    if record.pendingCloseTarget ~= nil then
        self:FinishLoadedPendingClosePanel(record)
        return
    end

    if not record.config.IgnoreStack then
        local top = self.stackCoordinator:GetTop(
            self.stackRecords)
        if not top or top.LogicConfig.Id ~= record.config.Id then
            self:ApplyClosePolicy(record, false, true)
            return
        end
    end

    self:TryShowPanel(record, record.userData)
end

function UIManager:TryShowPanel(record, userData)
    local ok, errorMessage = xpcall(function()
        self:ShowPanel(record, userData)
    end, debug.traceback)
    if not ok then
        self:HandleLoadFailed(record, errorMessage)
        return false
    end
    return true
end

function UIManager:ShowPanel(record, userData)
    if not self:IsRecordAlive(record)
        or not record.logic.isLoaded then
        error("cannot show unloaded panel: " .. record.config.Id)
    end

    self:ClearPendingClose(record)
    record.showWhenLoaded = true
    self:EnsureBackdrop(record)
    record.logic:Show(userData)
    record.logic.uiGameObject.transform:SetAsLastSibling()
    self:TransitionPanel(record, UIDefine.PanelState.Visible, true)
    self:FlushCallbacks(record, true, nil)

    if not record.config.IgnoreStack
        and record.config.ClosePopupsWhenShown then
        self:CloseVisiblePopupsExceptDebug()
    end
    self:InternalClosePendingUI(record.config)
end

function UIManager:HidePanel(recordOrConfig, stopCloseAnim)
    local record = recordOrConfig.logic
        and recordOrConfig
        or self:GetPanelRecord(recordOrConfig)
    if not self:IsRecordAlive(record) then
        return false
    end

    record.logic:Hide(stopCloseAnim == true)
    self:ReleaseBackdrop(record)
    self:ClearPendingClose(record)
    self:TransitionPanel(record, UIDefine.PanelState.Hidden, true)
    return true
end

function UIManager:DestroyPanel(recordOrConfig)
    local record = recordOrConfig.logic
        and recordOrConfig
        or self:GetPanelRecord(recordOrConfig)
    if not record then
        return false
    end

    record.requestId = self.nextPanelRequestId + 1
    self.nextPanelRequestId = record.requestId
    local gameObject =
        record.logic and record.logic.uiGameObject or nil
    self:ReleaseBackdrop(record)

    if record.logic then
        record.logic:DisposeLogic()
    end
    if gameObject ~= nil then
        self:ReleaseInstance(record.config, gameObject)
    end

    record.logic = nil
    record.showWhenLoaded = false
    self:ClearPendingClose(record)
    self:TransitionPanel(
        record, UIDefine.PanelState.Destroyed, true)
    self:FlushCallbacks(record, false, "panel destroyed")
    return true
end

function UIManager:EnsureBackdrop(record)
    if not record.config.BlurMode
        or record.logic == nil
        or record.logic.uiGameObject == nil then
        self:ReleaseBackdrop(record)
        return nil
    end

    if record.backdrop ~= nil then
        record.backdrop:SetActive(true)
        record.backdrop.transform:SetAsLastSibling()
        return record.backdrop
    end

    local parent = self.layerRoot:GetUIParentRoot(
        record.config:GetEffectiveLayer())
    record.backdrop =
        CS.LxyDemo.UIFramework.LuaUIRuntime.CreateBackdrop(
            parent,
            record.config.Id,
            record.config.BackdropColor,
            record.config.BlurCloseOnClick)
    record.backdrop.transform:SetAsLastSibling()
    return record.backdrop
end

function UIManager:ReleaseBackdrop(record)
    if record and record.backdrop ~= nil then
        CS.UnityEngine.Object.Destroy(record.backdrop)
        record.backdrop = nil
    end
end

function UIManager:ReleaseInstance(config, gameObject)
    local release = config.Release or self.customRelease
    if release then
        safeCall(release, config, gameObject)
    elseif gameObject ~= nil then
        CS.UnityEngine.Object.Destroy(gameObject)
    end
end

function UIManager:HandleLoadFailed(record, errorMessage)
    local _, wasTop = self.stackCoordinator:PopPanel(
        self.stackRecords, record.config, false)
    local gameObject =
        record.logic and record.logic.uiGameObject or nil
    self:ReleaseBackdrop(record)
    if record.logic then
        record.logic:DisposeLogic()
    end
    if gameObject ~= nil then
        self:ReleaseInstance(record.config, gameObject)
    end

    record.logic = nil
    record.showWhenLoaded = false
    self:ClearPendingClose(record)
    self:TransitionPanel(record, UIDefine.PanelState.Failed, true)
    self:FlushCallbacks(record, false, tostring(errorMessage))

    CS.UnityEngine.Debug.LogError(string.format(
        "[LuaUI] failed to load %s:\n%s",
        record.config.Id, tostring(errorMessage)))

    if wasTop then
        local top = self.stackCoordinator:GetTop(self.stackRecords)
        if top then
            self:OpenUIPanel(
                top.LogicConfig, top.preUserData)
        end
    end
end

function UIManager:CloseUIPanel(
    configOrId, forceDestroy, stopCloseAnim)
    local config = self:ResolveConfig(configOrId, false)
    if not config then
        return false
    end

    local record = self.panelRecords[config.Id]
    if not self:IsRecordAlive(record) then
        return false
    end

    local newTop, wasTop =
        self.stackCoordinator:PopPanel(
            self.stackRecords, config, false)
    if wasTop and newTop then
        self:SetPendingClose(record, newTop.LogicConfig)
        record.closeForceDestroy = forceDestroy == true
        record.closeStopCloseAnim = stopCloseAnim == true
        self:OpenUIPanel(
            newTop.LogicConfig, newTop.preUserData)
        return true
    end

    if record.state == UIDefine.PanelState.Loading
        or (record.logic and not record.logic.isLoaded) then
        self:SetPendingClose(record, PENDING_CLOSE_IMMEDIATE)
        record.closeForceDestroy = forceDestroy == true
        record.closeStopCloseAnim = stopCloseAnim == true
        self:TransitionPanel(record, UIDefine.PanelState.Closing, true)
        self:FlushCallbacks(
            record, false, "panel closed while loading")
        return true
    end

    self:ApplyClosePolicy(
        record, forceDestroy == true, stopCloseAnim == true)
    return true
end

function UIManager:CloseUIPanelForceDestroy(configOrId)
    return self:CloseUIPanel(configOrId, true, true)
end

function UIManager:ApplyClosePolicy(
    record, forceDestroy, stopCloseAnim)
    if not self:IsRecordAlive(record) then
        return
    end

    if forceDestroy
        or record.config.CloseType == UIDefine.CloseType.Destroy then
        self:DestroyPanel(record)
    else
        self:HidePanel(record, stopCloseAnim)
    end
end

function UIManager:SetPendingClose(record, targetConfig)
    if targetConfig ~= PENDING_CLOSE_IMMEDIATE
        and targetConfig
        and targetConfig.Id == record.config.Id then
        self:ClearPendingClose(record)
        return
    end

    if targetConfig and targetConfig ~= PENDING_CLOSE_IMMEDIATE then
        for _, other in pairs(self.panelRecords) do
            if other ~= record
                and other.pendingCloseTarget
                and other.pendingCloseTarget ~= PENDING_CLOSE_IMMEDIATE
                and other.pendingCloseTarget.Id == record.config.Id then
                other.pendingCloseTarget = targetConfig
            end
        end
    end

    record.pendingCloseTarget = targetConfig
end

function UIManager:ClearPendingClose(record)
    record.pendingCloseTarget = nil
    record.closeForceDestroy = false
    record.closeStopCloseAnim = false
end

function UIManager:InternalClosePendingUI(shownConfig)
    local waiting = {}
    for _, record in pairs(self.panelRecords) do
        local target = record.pendingCloseTarget
        if target ~= nil
            and target ~= PENDING_CLOSE_IMMEDIATE
            and target.Id == shownConfig.Id then
            table.insert(waiting, record)
        end
    end

    for _, record in ipairs(waiting) do
        local forceDestroy = record.closeForceDestroy
        local stopCloseAnim = record.closeStopCloseAnim
        self:ClearPendingClose(record)
        self:ApplyClosePolicy(
            record, forceDestroy, stopCloseAnim)
    end
end

function UIManager:FinishLoadedPendingClosePanel(record)
    local forceDestroy = record.closeForceDestroy
    local stopCloseAnim = record.closeStopCloseAnim
    self:ClearPendingClose(record)
    self:ApplyClosePolicy(
        record, forceDestroy, stopCloseAnim)
end

function UIManager:CloseVisiblePopupsExceptDebug()
    local targets = {}
    for _, record in pairs(self.panelRecords) do
        if self:IsRecordAlive(record)
            and record.config.IgnoreStack
            and record.config:GetEffectiveLayer()
                ~= UIDefine.OrderLayer.Debug
            and record.logic:IsUIVisible() then
            table.insert(targets, record.config)
        end
    end

    for _, config in ipairs(targets) do
        self:CloseUIPanel(config)
    end
end

function UIManager:PreloadPanel(configOrId, callback)
    return self:OpenInternal(
        configOrId,
        nil,
        function(succeeded, errorMessage, logic)
            safeCall(callback, succeeded, errorMessage, logic)
        end,
        false)
end

function UIManager:PreloadPanels(configs, callback)
    configs = configs or {}
    local index = 0
    local function nextPanel()
        index = index + 1
        local config = configs[index]
        if not config then
            safeCall(callback, true, nil)
            return
        end

        self:PreloadPanel(config, function(
            succeeded, errorMessage)
            if not succeeded then
                safeCall(callback, false, errorMessage)
                return
            end
            nextPanel()
        end)
    end
    nextPanel()
end

function UIManager:GetPanelLogic(configOrId)
    local record = self:GetPanelRecord(configOrId)
    return self:IsRecordAlive(record) and record.logic or nil
end

function UIManager:IsPanelOpen(configOrId)
    local logic = self:GetPanelLogic(configOrId)
    return logic ~= nil and logic:IsUIVisible()
end

function UIManager:IsPanelLoaded(configOrId)
    local logic = self:GetPanelLogic(configOrId)
    return logic ~= nil and logic.isLoaded
end

function UIManager:GetTopStackEntry()
    return self.stackCoordinator:GetTop(self.stackRecords)
end

function UIManager:IsTopStackPanel(configOrId)
    local config = self:ResolveConfig(configOrId, false)
    local top = self:GetTopStackEntry()
    return config ~= nil
        and top ~= nil
        and top.LogicConfig.Id == config.Id
end

function UIManager:SetStackPanelUserData(configOrId, userData)
    local config = self:ResolveConfig(configOrId, false)
    if not config then
        return false
    end

    for _, entry in ipairs(self.stackRecords) do
        if entry.LogicConfig.Id == config.Id then
            entry.preUserData = userData
            return true
        end
    end
    return false
end

function UIManager:GetVisiblePopupLogics()
    local result = {}
    for _, record in pairs(self.panelRecords) do
        if self:IsRecordAlive(record)
            and record.config.IgnoreStack
            and record.logic:IsUIVisible() then
            table.insert(result, record.logic)
        end
    end
    return result
end

function UIManager:GetAllPanelLogics()
    local result = {}
    for _, record in pairs(self.panelRecords) do
        if self:IsRecordAlive(record) then
            table.insert(result, record.logic)
        end
    end
    return result
end

function UIManager:CaptureNavigationStack()
    return copyArray(self.stackRecords)
end

function UIManager:SaveNavigationStack(key)
    assert(type(key) == "string" and key ~= "",
        "navigation stack key cannot be empty")
    self.savedPanelStacks[key] =
        self:CaptureNavigationStack()
end

function UIManager:RestoreNavigationStack(key, callback)
    local saved = self.savedPanelStacks[key]
    if not saved or #saved == 0 then
        safeCall(callback, false, "saved stack not found: " .. key)
        return
    end

    self.stackRecords = copyArray(saved)
    local top = self:GetTopStackEntry()
    self:OpenUIPanel(
        top.LogicConfig,
        top.preUserData,
        callback)
end

function UIManager:CloseAllLoadedUI(forceDestroy)
    local records = {}
    for _, record in pairs(self.panelRecords) do
        if self:IsRecordAlive(record) then
            table.insert(records, record)
        end
    end

    self.stackRecords = {}
    for _, record in ipairs(records) do
        self:ApplyClosePolicy(
            record, forceDestroy ~= false, true)
    end
end

function UIManager:OnSceneChanged()
    if not self.closePanelsOnSceneChanged then
        return
    end

    local targets = {}
    for _, record in pairs(self.panelRecords) do
        if self:IsRecordAlive(record)
            and record.config.AutoDestroyWhenChangeScene then
            table.insert(targets, record)
        end
    end

    for _, record in ipairs(targets) do
        self.stackCoordinator:RemovePanel(
            self.stackRecords, record.config)
        self:DestroyPanel(record)
    end
end

function UIManager:GetRuntimeSnapshots()
    local snapshots = {}
    for id, record in pairs(self.panelRecords) do
        table.insert(snapshots, {
            Id = id,
            State = record.state,
            IsLoaded = record.logic ~= nil
                and record.logic.isLoaded,
            IsVisible = record.logic ~= nil
                and record.logic:IsUIVisible(),
            RequestId = record.requestId,
            PendingClose = record.pendingCloseTarget ~= nil,
        })
    end
    table.sort(snapshots, function(left, right)
        return left.Id < right.Id
    end)
    return snapshots
end

function UIManager:Shutdown()
    if self.shuttingDown then
        return
    end

    self.shuttingDown = true
    self:CloseAllLoadedUI(true)
    self.panelRecords = {}
    self.panelConfigs = {}
    self.stackRecords = {}
    self.savedPanelStacks = {}
end

-- UIManager 内部仍然使用实例承载状态；模块对业务层暴露单例代理。
-- LuaUIRuntime/Bootstrap 完成初始化后，可以直接这样调用：
-- local UIManager = require("Framework.UI.UIManager")
-- UIManager:OpenUIPanel("UILogin", userData)
local UIManagerModule = {
    Class = UIManager,
    new = UIManager.new,
}

local runtimeInstance = nil

function UIManagerModule.SetInstance(instance)
    if instance ~= nil then
        assert(
            UIManager.IsInstance(instance, UIManager),
            "UIManager.SetInstance requires a LuaUIManager instance")
    end
    runtimeInstance = instance
end

function UIManagerModule.GetInstance()
    assert(
        runtimeInstance ~= nil,
        "UIManager is not initialized; initialize LuaUIRuntime first")
    return runtimeInstance
end

setmetatable(UIManagerModule, {
    __index = function(_, methodName)
        local classMember = UIManager[methodName]
        if type(classMember) ~= "function" then
            return classMember
        end

        return function(_, ...)
            local instance = UIManagerModule.GetInstance()
            return classMember(instance, ...)
        end
    end,
})

return UIManagerModule
