---
--- Created by liuxiuyang.
--- DateTime: 2026/08/03 17:32
--- ResPath: UIRes/Prefabs/Assets/GameResources/Prefabs/UIRes/Login/UITest.prefab
--- ClassName: UI.Login.UITest
---

---@class UITest : UITestAuto
local UITest = require("UI.Login.UITest_Auto")

function UITest:ctor()
end

--function UITest:OnInitLogic(userData)
--    -- 逻辑层初始化
--end

function UITest:OnBindGameObject(gameObject)
    -- UI绑定完成
    -- gameObject: 绑定的GameObject对象
end

function UITest:OnShow(userData)
    -- UI显示
    -- userData: 显示时传入的用户数据
end

--function UITest:OnHide()
--    -- UI隐藏
--end

function UITest:OnDisposeLogic()
    -- 销毁逻辑对象（彻底释放）
end

return UITest

