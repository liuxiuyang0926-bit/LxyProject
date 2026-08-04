---
--- Created by liuxiuyang.
--- DateTime: 2026/08/03 17:19
--- ResPath: UIRes/Prefabs/Assets/GameResources/Prefabs/UIRes/Login/UILogin.prefab
--- ClassName: UI.Login.UILogin
---

---@class UILogin : UILoginAuto
local UILogin = require("UI.Login.UILogin_Auto")

function UILogin:ctor()
end

--function UILogin:OnInitLogic(userData)
--    -- 逻辑层初始化
--end

function UILogin:OnBindGameObject(gameObject)
    -- UI绑定完成
    -- gameObject: 绑定的GameObject对象
end

function UILogin:OnShow(userData)
    -- UI显示
    -- userData: 显示时传入的用户数据
end

--function UILogin:OnHide()
--    -- UI隐藏
--end

function UILogin:onClick_btn_login()
    Game.UIManager:OpenUIPanel(uiDefine.PanelConfig.UIMainView)
end

function UILogin:OnDisposeLogic()
    -- 销毁逻辑对象（彻底释放）
end

return UILogin

