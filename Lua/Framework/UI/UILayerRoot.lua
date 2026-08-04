require("Framework.Core.DefineClass")
local UIDefine = require("Framework.UI.UIDefine")

local UILayerRoot = DefineClass("LuaUILayerRoot")

local layerEnums = {
    [UIDefine.OrderLayer.Bottom] = CS.LxyDemo.UIFramework.UILayer.Bottom,
    [UIDefine.OrderLayer.Stack] = CS.LxyDemo.UIFramework.UILayer.Stack,
    [UIDefine.OrderLayer.PopUp] = CS.LxyDemo.UIFramework.UILayer.Popup,
    [UIDefine.OrderLayer.Guide] = CS.LxyDemo.UIFramework.UILayer.Guide,
    [UIDefine.OrderLayer.Top] = CS.LxyDemo.UIFramework.UILayer.Top,
    [UIDefine.OrderLayer.Loading] = CS.LxyDemo.UIFramework.UILayer.Loading,
    [UIDefine.OrderLayer.Tips] = CS.LxyDemo.UIFramework.UILayer.Tips,
    [UIDefine.OrderLayer.Debug] = CS.LxyDemo.UIFramework.UILayer.Debug,
}

function UILayerRoot:ctor(csharpLayerRoot)
    assert(csharpLayerRoot ~= nil, "UILayerRoot requires C# UILayerRoot")
    self.csharpLayerRoot = csharpLayerRoot
end

function UILayerRoot:GetUIParentRoot(orderLayer)
    local numericLayer = tonumber(orderLayer)
    if numericLayer == nil then
        numericLayer = UIDefine.OrderLayer.Stack
    end
    if numericLayer == UIDefine.OrderLayer.Auto then
        numericLayer = UIDefine.OrderLayer.Stack
    end

    local enumValue = layerEnums[numericLayer]
        or CS.LxyDemo.UIFramework.UILayer.Stack
    return self.csharpLayerRoot:GetLayer(enumValue)
end

return UILayerRoot
