local UIManager = require("Framework.UI.UIManager")
local UIPanelConfig = require("Framework.UI.UIPanelConfig")
local UIDefine = require("Framework.UI.UIDefine")

local Bootstrap = {
    Manager = nil,
}

local function requireManager()
    assert(Bootstrap.Manager ~= nil,
        "Lua UI Bootstrap is not initialized")
    return Bootstrap.Manager
end

local function normalizeUIDefineConfig(panelId, rawConfig)
    local config = rawConfig
    if type(config.GetEffectiveLayer) ~= "function" then
        config.Id = config.Id or panelId
        config = UIPanelConfig.new(config)
        UIDefine.PanelConfig[panelId] = config
    else
        config.Id = config.Id or panelId
    end
    config:Validate()
    return config
end

local function registerUIDefinePanels(manager)
    for panelId, rawConfig in pairs(UIDefine.PanelConfig) do
        local config =
            normalizeUIDefineConfig(panelId, rawConfig)
        if manager.panelConfigs[config.Id] == nil then
            manager:RegisterPanel(config)
        end
    end
end

local function loadPanelAsync(config, parent, complete)
    CS.LxyDemo.UIFramework.LuaUIRuntime.LoadPanelAsync(
        config.ResPath,
        parent,
        complete)
end

local function releasePanel(config, gameObject)
    CS.LxyDemo.UIFramework.LuaUIRuntime.ReleasePanel(
        gameObject)
end

function Bootstrap.Initialize(csharpLayerRoot)
    if Bootstrap.Manager == nil then
        Bootstrap.Manager = UIManager.new({
            layerRoot = csharpLayerRoot,
            loadAsync = loadPanelAsync,
            release = releasePanel,
        })
    else
        Bootstrap.Manager:SetLayerRoot(csharpLayerRoot)
    end
    UIManager.SetInstance(Bootstrap.Manager)
    registerUIDefinePanels(Bootstrap.Manager)
    return Bootstrap.Manager
end

function Bootstrap.RegisterPrefab(prefab, configuredPanelId)
    local manager = requireManager()
    assert(prefab ~= nil, "prefab cannot be nil")

    local objectBinder = prefab:GetComponent(
        typeof(CS.LuaObjectBind.ObjectBinder))
    assert(
        objectBinder ~= nil,
        "Lua UI Prefab requires ObjectBinder: "
        .. tostring(prefab.name))

    local panelId = configuredPanelId
    local rawConfig = panelId
        and UIDefine.GetPanelConfig(panelId)
        or nil
    if rawConfig == nil then
        rawConfig, panelId =
            UIDefine.ResolvePrefabConfig(prefab.name)
    end
    assert(
        rawConfig ~= nil,
        "UIDefine.PanelConfig is missing for Lua Prefab: "
        .. tostring(prefab.name))

    local config =
        normalizeUIDefineConfig(panelId, rawConfig)
    config.Prefab = prefab
    if manager.panelConfigs[config.Id] == nil then
        manager:RegisterPanel(config)
    end
    return config.Id
end

function Bootstrap.RegisterUIDefinePanels()
    registerUIDefinePanels(requireManager())
end

function Bootstrap.OpenPanel(panelId, userData)
    return requireManager():OpenUIPanel(panelId, userData)
end

function Bootstrap.ClosePanel(panelId, forceDestroy)
    return requireManager():CloseUIPanel(
        panelId, forceDestroy == true)
end

function Bootstrap.PreloadPanel(panelId)
    return requireManager():PreloadPanel(panelId)
end

function Bootstrap.IsPanelOpen(panelId)
    return requireManager():IsPanelOpen(panelId)
end

function Bootstrap.OnSceneChanged()
    if Bootstrap.Manager then
        Bootstrap.Manager:OnSceneChanged()
    end
end

function Bootstrap.GetSnapshots()
    return requireManager():GetRuntimeSnapshots()
end

function Bootstrap.Shutdown()
    local sceneManager = rawget(_G, "SceneMgr")
    if sceneManager and type(sceneManager.Shutdown) == "function" then
        sceneManager:Shutdown()
    end

    if Bootstrap.Manager then
        Bootstrap.Manager:Shutdown()
        Bootstrap.Manager = nil
    end
    UIManager.SetInstance(nil)
end

return Bootstrap
