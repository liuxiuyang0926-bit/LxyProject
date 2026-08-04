require("Framework.Core.DefineClass")
local SceneBase = require("Managers.Scene.SceneBase")
local SceneType = require("Managers.Scene.SceneType")

local SceneLogin = DefineClass("SceneLogin", SceneBase)

SceneLogin.SceneName = "Login"
SceneLogin.Type = SceneType.Login

function SceneLogin:OnEnterScene(enterData)
    CS.UnityEngine.Debug.Log("[LuaScene] enter Login")
    Game.UIManager:OpenUIPanel(uiDefine.PanelConfig.UILogin)
end

function SceneLogin:OnLoadingFinish()
    CS.UnityEngine.Debug.Log("[LuaScene] Login loading finished")
end

function SceneLogin:OnExitScene()
    CS.UnityEngine.Debug.Log("[LuaScene] exit Login")
end

return SceneLogin
