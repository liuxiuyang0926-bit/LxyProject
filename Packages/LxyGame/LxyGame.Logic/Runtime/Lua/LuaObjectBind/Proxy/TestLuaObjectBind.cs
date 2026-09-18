using System;
using UnityEngine;
using XLua;

namespace LuaObjectBind
{
    public class TestLuaObjectBind : MonoBehaviour
    {
        /// <summary>
        /// 在组件启用时建立运行时关联。
        /// </summary>
        public void OnEnable()
        {
            FieldBindDispatcher dispatcher = new();
            dispatcher.Register();
            FieldBindDispatcher.Instance.Init();
            if (this.TryGetComponent(out ObjectBinder objectBinder))
            {
                LuaEnv luaEnv = LuaObjectBindProxy.GetLuaEnv();
                LuaTable table = luaEnv.NewTable();
                objectBinder.Init(table);
                Debug.Log("ObjectBinder initialized.");
                luaEnv.Global.Set("AAA", table);
                luaEnv.DoString(@"

local Util = {}
Util.dumpFunc = function(t)
    for k, v in pairs(t) do
        print(k, v)
        if type(v) == 'table' then
            Util.dumpFunc(v)
        end
    end
end
Util.dumpFunc(AAA)

local tbl = AAA
local __newindex = function(t, key, value)
            if t.__fieldMap ~= nil and t.__objectBinderId ~= nil and t.__fieldMap[key] ~= nil then
                local binderId = t.__objectBinderId
                local keyId = t.__fieldMap[key]
                local fieldBindTypeEnum = CS.LuaObjectBind.ObjectBinder.GetFieldBindTypeEnum(binderId, keyId)
                local Enum = CS.LuaObjectBind.FieldBindTypeEnum
                local ObjectBinder = CS.LuaObjectBind.ObjectBinder
                if fieldBindTypeEnum == Enum.String then
                    ObjectBinder.HandleString(binderId, keyId, value)
                    return true
                elseif fieldBindTypeEnum == Enum.Int then
                    ObjectBinder.HandleInt(binderId, keyId, value)
                    return true
                elseif fieldBindTypeEnum == Enum.Float then
                    ObjectBinder.HandleFloat(binderId, keyId, value)
                    return true
                elseif fieldBindTypeEnum == Enum.Bool then
                    ObjectBinder.HandleBool(binderId, keyId, value)
                    return true
                elseif fieldBindTypeEnum == Enum.Vector2 then
                    ObjectBinder.HandleVector2(binderId, keyId, value)
                    return true
                elseif fieldBindTypeEnum == Enum.Vector3 then
                    ObjectBinder.HandleVector3(binderId, keyId, value)
                    return true
                elseif fieldBindTypeEnum == Enum.LuaTable then
                    return ObjectBinder.HandleLuaTable(binderId, keyId, value)
                end
            end
            return false
        end

        local __index = function(t, key)
            if key == '__objectIdMap' or key == '__objectBinderId' or key == '__fieldMap' or key == '__pathMap' then
                return nil
            end
            if t.__pathMap ~= nil and t.__objectBinderId ~= nil and t.__pathMap[key] ~= nil then
                return t.__pathMap[key]
            end

            if t.__objectIdMap ~= nil and t.__objectBinderId ~= nil and t.__objectIdMap[key] ~= nil then
                return CS.LuaObjectBind.ObjectBinder.GetObject(t.__objectBinderId, t.__objectIdMap[key])
            end

            if t.__fieldMap ~= nil and t.__objectBinderId ~= nil and t.__fieldMap[key] ~= nil then
                local binderId = t.__objectBinderId
                local keyId = t.__fieldMap[key]
                local fieldBindTypeEnum = CS.LuaObjectBind.ObjectBinder.GetFieldBindTypeEnum(binderId, keyId)
                local Enum = CS.LuaObjectBind.FieldBindTypeEnum
                local ObjectBinder = CS.LuaObjectBind.ObjectBinder
                if fieldBindTypeEnum == Enum.None then
                    return nil
                else
                    if fieldBindTypeEnum == Enum.String then
                        return ObjectBinder.GetString(binderId, keyId)
                    elseif fieldBindTypeEnum == Enum.Int then
                        return ObjectBinder.GetInt(binderId, keyId)
                    elseif fieldBindTypeEnum == Enum.Float then
                        return ObjectBinder.GetFloat(binderId, keyId)
                    elseif fieldBindTypeEnum == Enum.Bool then
                        return ObjectBinder.GetBool(binderId, keyId)
                    elseif fieldBindTypeEnum == Enum.Vector2 then
                        return ObjectBinder.GetVector2(binderId, keyId)
                    elseif fieldBindTypeEnum == Enum.Vector3 then
                        return ObjectBinder.GetVector3(binderId, keyId)
                    end
                end
            end
        end
        local oldMeta = getmetatable(tbl)
        if oldMeta == nil then
            setmetatable(tbl, { __index = __index, __newindex = __newindex })
        else
            local newMeta = {}
            for k, v in pairs(oldMeta) do
                newMeta[k] = v
            end
            newMeta.__index = function(t, key)
                local res = __index(t, key)
                if res ~= nil then
                    return res
                end
                if oldMeta.__index then
                    if type(oldMeta.__index) == ""function"" then
                        return oldMeta.__index(t, key)
                    else
                        if type(oldMeta.__index) == ""table"" then
                            return oldMeta.__index[key]
                        end
                    end
                end
            end
            newMeta.__newindex = function(t, key, value)
                local res = __newindex(t, key, value)
                if res then
                    return
                end
                if oldMeta.__newindex then
                    if type(oldMeta.__newindex) == ""function"" then
                        oldMeta.__newindex(t, key, value)
                    else
                        if type(oldMeta.__newindex) == ""table"" then
                            oldMeta.__newindex[key] = value
                        end
                    end
                else
                    rawset(t, key, value)
                end
            end
            setmetatable(tbl, newMeta)
        end

print('Start')
print(AAA.PathA)
print(AAA.TextTest)
AAA.TextA = 'AAAAAAAA'
AAA.ActiveA = true
AAA.TxtC.text = 'CCCCCCC'
AAA.GoC.transform.localScale = CS.UnityEngine.Vector3(3,3,3)
AAA.ImgSpriteB = 'b'
AAA.ImgColorB = {r=0.5,g=0.5,b=0.5,a=0.5}


                ");
            }
            else
            {
                Debug.LogError("ObjectBinder component not found.");
            }
        }
    }
}