require("Framework.Core.DefineClass")

local UILogicFactory = DefineClass("UILogicFactory")

function UILogicFactory:CreatePanelLogic(config)
    local logicClass = config.LogicClass
    if logicClass == nil then
        logicClass = require(config.ClassName)
    end

    assert(logicClass ~= nil,
        "cannot load UI logic: " .. tostring(config.ClassName))
    assert(type(logicClass.new) == "function",
        "UI logic must expose .new(): " .. tostring(config.ClassName))

    local logic = logicClass.new(config)
    logic:SetPanelConfig(config)
    return logic
end

return UILogicFactory
