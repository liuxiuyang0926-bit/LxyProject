local Main = {}

require("InitGame")

function Main.Start()
    CS.UnityEngine.Debug.Log("[Lua] Main.lua Start 执行成功")
    Game.SceneMgr:GameStart()
end

return Main
