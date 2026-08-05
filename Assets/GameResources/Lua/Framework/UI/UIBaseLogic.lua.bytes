require("Framework.Core.DefineClass")
local UIObjectBinderProxy =
    require("Framework.UI.UIObjectBinderProxy")

local UIBaseLogic = DefineClass("UIBaseLogic")

-- XLua userdata is still non-nil after its Unity native object is destroyed.
-- UnityEngine.Object:Equals(nil) preserves Unity's destroyed-object semantics.
local function isUnityObjectAlive(unityObject)
    return unityObject ~= nil and not unityObject:Equals(nil)
end

function UIBaseLogic:ctor(panelConfig)
    self.PanelConfig = panelConfig
    self.Config = panelConfig
    self.manager = nil
    self.uiGameObject = nil
    self.isLoaded = false
    self.isLogicVisible = false
    self.logicUserData = nil
    self.destroyed = false
    self.Widget = {}
    self.Logic = {}
    self.elementsLogic = {}
    self.__generatedEvents = {}
    self.objectBinder = nil
end

function UIBaseLogic:SetManager(manager)
    self.manager = manager
end

function UIBaseLogic:SetPanelConfig(config)
    self.PanelConfig = config
    self.Config = config
end

function UIBaseLogic:InitLogic(userData)
    self.logicUserData = userData
    if self.OnInitLogic then
        self:OnInitLogic(userData)
    end
end

function UIBaseLogic:BindGameObject(gameObject)
    assert(not self.destroyed, "cannot bind a destroyed UI logic")
    assert(isUnityObjectAlive(gameObject),
        "gameObject cannot be nil or destroyed")

    self.uiGameObject = gameObject
    self.isLoaded = true
    self.Widget = self.Widget or {}
    self.Logic = self.Logic or {}

    self.objectBinder = gameObject:GetComponent(
        typeof(CS.LuaObjectBind.ObjectBinder))
    if self.objectBinder ~= nil then
        self.objectBinder:Init(self)
        UIObjectBinderProxy.Attach(self.Widget)
    end

    if self.BindAll then
        self:BindAll()
    end
    if self.OnBindGameObject then
        self:OnBindGameObject(gameObject)
    end
end

function UIBaseLogic:UnBindGameObject()
    if not self.isLoaded then
        return
    end

    -- Unity may destroy the UI hierarchy before LuaUIRuntime.OnDestroy runs.
    -- In that case all binding callbacks can reference invalid components, so
    -- only clear the Lua-side bookkeeping and continue disposing the logic.
    if isUnityObjectAlive(self.uiGameObject) then
        self:ReleaseGeneratedEvents()
        if self.ReleaseBind then
            self:ReleaseBind()
        end
        if self.OnUnBindGameObject then
            self:OnUnBindGameObject()
        end

        if isUnityObjectAlive(self.objectBinder) then
            self.objectBinder:Release()
        end
    else
        self.__generatedEvents = {}
    end
    self.Widget = {}
    self.Logic = {}
    self.objectBinder = nil
    self.uiGameObject = nil
    self.isLoaded = false
end

function UIBaseLogic:Show(userData)
    assert(self.isLoaded and isUnityObjectAlive(self.uiGameObject),
        "UI logic is not bound: " .. tostring(self.PanelConfig.Id))

    self.logicUserData = userData
    self.uiGameObject:SetActive(true)
    self.isLogicVisible = true
    if self.OnShow then
        self:OnShow(userData)
    end
end

function UIBaseLogic:Hide(stopCloseAnim)
    if not self.isLoaded or not self.isLogicVisible then
        return
    end

    local gameObjectAlive = isUnityObjectAlive(self.uiGameObject)
    if gameObjectAlive and self.OnHide then
        self:OnHide(stopCloseAnim == true)
    end
    self.isLogicVisible = false
    if gameObjectAlive and isUnityObjectAlive(self.uiGameObject) then
        self.uiGameObject:SetActive(false)
    end
end

function UIBaseLogic:DisposeLogic()
    if self.destroyed then
        return
    end

    self:Hide(true)
    self:UnBindGameObject()
    if self.OnDisposeLogic then
        self:OnDisposeLogic()
    end

    self.elementsLogic = {}
    self.manager = nil
    self.destroyed = true
end

function UIBaseLogic:IsUIVisible()
    return self.isLoaded
        and self.isLogicVisible
        and isUnityObjectAlive(self.uiGameObject)
        and self.uiGameObject.activeSelf
end

function UIBaseLogic:FindTransform(path, required, fieldName)
    if not isUnityObjectAlive(self.uiGameObject) then
        if required then
            error("UI root is nil or destroyed: "
                .. tostring(self.PanelConfig.Id))
        end
        return nil
    end

    local transform = self.uiGameObject.transform
    local target = transform
    if path and path ~= "" and path ~= "." then
        target = transform:Find(path)
    end

    if target == nil and required then
        error(string.format(
            "Panel %s cannot find binding %s at path %s",
            tostring(self.PanelConfig.Id),
            tostring(fieldName),
            tostring(path)))
    end
    return target
end

function UIBaseLogic:FindGameObject(path, required, fieldName)
    local target = self:FindTransform(path, required, fieldName)
    return target and target.gameObject or nil
end

function UIBaseLogic:FindComponent(
    path, componentType, required, fieldName)
    local target = self:FindTransform(path, required, fieldName)
    if target == nil then
        return nil
    end

    local component = target:GetComponent(componentType)
    if component == nil and required then
        error(string.format(
            "Panel %s binding %s is missing component %s",
            tostring(self.PanelConfig.Id),
            tostring(fieldName),
            tostring(componentType)))
    end
    return component
end

function UIBaseLogic:BindGeneratedEvent(
    key, unityEvent, callback)
    if unityEvent == nil or callback == nil then
        return
    end

    unityEvent:AddListener(callback)
    self.__generatedEvents[key] = {
        event = unityEvent,
        callback = callback,
    }
end

function UIBaseLogic:BindGeneratedClick(key, component)
    if component == nil then
        return
    end

    local callback = function()
        if self.OnGeneratedClick then
            self:OnGeneratedClick(key)
        end
    end
    self:BindGeneratedEvent(key, component.onClick, callback)
end

function UIBaseLogic:BindGeneratedValueChanged(key, component)
    if component == nil then
        return
    end

    local callback = function(value)
        if self.OnGeneratedValueChanged then
            self:OnGeneratedValueChanged(key, value)
        end
    end
    self:BindGeneratedEvent(
        key, component.onValueChanged, callback)
end

function UIBaseLogic:ReleaseGeneratedEvents()
    for key, binding in pairs(self.__generatedEvents) do
        if binding.event ~= nil and binding.callback ~= nil then
            binding.event:RemoveListener(binding.callback)
        end
        self.__generatedEvents[key] = nil
    end
end

function UIBaseLogic:CloseSelf(forceDestroy)
    if self.manager then
        self.manager:CloseUIPanel(
            self.PanelConfig, forceDestroy == true)
    end
end

return UIBaseLogic
