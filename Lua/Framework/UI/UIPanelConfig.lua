require("Framework.Core.DefineClass")

local UIPanelConfig = DefineClass("UIPanelConfig")
local ORDER_LAYER_AUTO = 0
local ORDER_LAYER_STACK = 20
local ORDER_LAYER_POPUP = 30
local CLOSE_TYPE_DESTROY = 2
local SCREEN_FIT_SAFE_AREA = 1

local function valueOrDefault(value, defaultValue)
    if value == nil then
        return defaultValue
    end
    return value
end

function UIPanelConfig:ctor(raw)
    raw = raw or {}

    self.Id = raw.Id or raw.id
    self.ResPath = raw.ResPath or raw.resPath
    self.ClassName = raw.ClassName or raw.className
    self.Prefab = raw.Prefab or raw.prefab
    self.LogicClass = raw.LogicClass or raw.logicClass
    self.ScreenFitType = valueOrDefault(
        raw.ScreenFitType,
        valueOrDefault(raw.screenFitType, SCREEN_FIT_SAFE_AREA))
    self.ForceXGamma = valueOrDefault(
        raw.ForceXGamma,
        valueOrDefault(raw.forceXGamma, false))
    self.IgnoreStack = valueOrDefault(
        raw.IgnoreStack, valueOrDefault(raw.ignoreStack, false))
    self.OrderLayer = valueOrDefault(
        raw.OrderLayer, valueOrDefault(
            raw.orderLayer, ORDER_LAYER_AUTO))
    self.CloseType = valueOrDefault(
        raw.CloseType, valueOrDefault(
            raw.closeType, CLOSE_TYPE_DESTROY))
    self.ClosePopupsWhenShown = valueOrDefault(
        raw.ClosePopupsWhenShown,
        valueOrDefault(raw.closePopupsWhenShown, true))
    self.BlurMode = valueOrDefault(
        raw.BlurMode, valueOrDefault(raw.blurMode, false))
    self.BlurCloseOnClick = valueOrDefault(
        raw.BlurCloseOnClick,
        valueOrDefault(raw.blurCloseOnClick, true))
    self.BackdropColor = raw.BackdropColor
        or raw.backdropColor
        or CS.UnityEngine.Color(0, 0, 0, 0.65)
    self.AutoDestroyWhenChangeScene = valueOrDefault(
        raw.AutoDestroyWhenChangeScene,
        valueOrDefault(raw.autoDestroyWhenChangeScene, true))
    self.LoadAsync = raw.LoadAsync or raw.loadAsync
    self.Release = raw.Release or raw.release
    self.InAnimation = raw.InAnimation or raw.inAnimation
    self.OutAnimation = raw.OutAnimation or raw.outAnimation
end

function UIPanelConfig:GetEffectiveLayer()
    if self.OrderLayer ~= nil
        and self.OrderLayer ~= ORDER_LAYER_AUTO then
        return self.OrderLayer
    end

    if self.IgnoreStack then
        return ORDER_LAYER_POPUP
    end
    return ORDER_LAYER_STACK
end

function UIPanelConfig:Validate()
    assert(type(self.Id) == "string" and self.Id ~= "",
        "UIPanelConfig.Id cannot be empty")
    assert(
        (type(self.ClassName) == "string" and self.ClassName ~= "")
        or self.LogicClass ~= nil,
        "UIPanelConfig.ClassName or LogicClass is required: " .. self.Id)
    assert(
        self.Prefab ~= nil
        or (type(self.ResPath) == "string" and self.ResPath ~= "")
        or type(self.LoadAsync) == "function",
        "UIPanelConfig requires Prefab, ResPath, or LoadAsync: " .. self.Id)
end

return UIPanelConfig
