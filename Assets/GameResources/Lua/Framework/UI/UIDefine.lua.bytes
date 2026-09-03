local UIPanelConfig = require("Framework.UI.UIPanelConfig")

uiDefine = {}

uiDefine.ScreenFitType = {
    InSafeArea = 1,
    IgnoreSafeArea = 2,
}

uiDefine.CloseType = {
    Hide = 1,
    Destroy = 2,
}

-- Values match LxyDemo.UIFramework.UILayer.
uiDefine.OrderLayer = {
    Auto = 0,
    BottomLayer = 10,
    StackLayer = 20,
    PopUpLayer = 30,
    GuideLayer = 40,
    TopLayer = 50,
    LoadingLayer = 60,
    TipsLayer = 70,
    DebugLayer = 80,
}

-- Compatibility aliases used by the framework internals.
uiDefine.OrderLayer.Bottom = uiDefine.OrderLayer.BottomLayer
uiDefine.OrderLayer.Stack = uiDefine.OrderLayer.StackLayer
uiDefine.OrderLayer.PopUp = uiDefine.OrderLayer.PopUpLayer
uiDefine.OrderLayer.Popup = uiDefine.OrderLayer.PopUpLayer
uiDefine.OrderLayer.Guide = uiDefine.OrderLayer.GuideLayer
uiDefine.OrderLayer.Top = uiDefine.OrderLayer.TopLayer
uiDefine.OrderLayer.Loading = uiDefine.OrderLayer.LoadingLayer
uiDefine.OrderLayer.Tips = uiDefine.OrderLayer.TipsLayer
uiDefine.OrderLayer.Debug = uiDefine.OrderLayer.DebugLayer

-- ==================== 面板配置常用别名 ====================
-- 与参考项目保持相同的配置书写形式，PanelConfig 内无需反复填写 uiDefine。
local CloseType = uiDefine.CloseType
local ScreenFitType = uiDefine.ScreenFitType
local OrderLayer = uiDefine.OrderLayer

-- 这里只记录 Lua 模块路径，不会在加载 uiDefine 时立即加载界面逻辑。
-- 真正打开界面时，UIManager 会根据该路径加载对应的 Lua 逻辑。
local require = function(module)
    return module
end

-- ==================== 主面板配置表 ====================
-- 面板配置统一维护在这里，表的键就是面板 Id。
-- 创建 Lua 类型 Prefab 后，在此处复制并修改一份配置即可。
uiDefine.PanelConfig = {
    UILogin = UIPanelConfig.new({
        ResPath = "Assets/GameResources/Prefabs/UIRes/Login/UILogin.prefab",
        ClassName = require("UI.Login.UILogin"),
        ScreenFitType = ScreenFitType.InSafeArea,
        CloseType = CloseType.Destroy,
        IgnoreStack = false,
        OrderLayer = OrderLayer.StackLayer,
    }),
    UIMainView = UIPanelConfig.new({
        ResPath = "Assets/GameResources/Prefabs/UIRes/Main/UIMainView.prefab",
        ClassName = require("UI.Main.UIMainView"),
        ScreenFitType = ScreenFitType.InSafeArea,
        CloseType = CloseType.Hide,
        IgnoreStack = false,
        OrderLayer = OrderLayer.StackLayer,
    }),
}

-- ==================== 运行时辅助接口 ====================
-- 一般无需修改。用于动态注册、查询配置，以及根据 Prefab 名称反查配置。
function uiDefine.RegisterPanel(panelId, rawConfig)
    assert(type(panelId) == "string" and panelId ~= "",
        "uiDefine.RegisterPanel requires a panel Id")
    assert(rawConfig ~= nil,
        "uiDefine.RegisterPanel requires a config: " .. panelId)

    local config = rawConfig
    if type(config.GetEffectiveLayer) ~= "function" then
        config.Id = config.Id or panelId
        config = UIPanelConfig.new(config)
    else
        config.Id = config.Id or panelId
    end
    config:Validate()
    uiDefine.PanelConfig[panelId] = config
    return config
end

function uiDefine.GetPanelConfig(panelId)
    return uiDefine.PanelConfig[panelId]
end

function uiDefine.ResolvePrefabConfig(prefabName)
    local direct = uiDefine.PanelConfig[prefabName]
    if direct ~= nil then
        return direct, prefabName
    end

    for panelId, config in pairs(uiDefine.PanelConfig) do
        local path = config.ResPath or config.resPath
        local fileName = type(path) == "string"
            and string.match(path, "([^/\\]+)%.prefab$")
            or nil
        if fileName == prefabName then
            return config, panelId
        end
    end
    return nil, nil
end

uiDefine.PanelState = {
    None = "None",
    Creating = "Creating",
    Loading = "Loading",
    Visible = "Visible",
    Hidden = "Hidden",
    Closing = "Closing",
    Destroyed = "Destroyed",
    Failed = "Failed",
}

uiDefine.AllowedTransitions = {
    None = { Creating = true, Destroyed = true },
    Creating = { Loading = true, Failed = true, Destroyed = true },
    Loading = {
        Visible = true,
        Hidden = true,
        Closing = true,
        Failed = true,
        Destroyed = true,
    },
    Visible = {
        Hidden = true,
        Closing = true,
        Destroyed = true,
        Failed = true,
    },
    Hidden = {
        Visible = true,
        Loading = true,
        Closing = true,
        Destroyed = true,
        Failed = true,
    },
    Closing = {
        Loading = true,
        Visible = true,
        Hidden = true,
        Destroyed = true,
        Failed = true,
    },
    Destroyed = { Creating = true },
    Failed = { Creating = true, Destroyed = true },
}

return uiDefine
