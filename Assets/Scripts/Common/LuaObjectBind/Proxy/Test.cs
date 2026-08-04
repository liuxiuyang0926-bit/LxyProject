using System;
using UnityEngine;
using XLua;

namespace LuaObjectBind
{
    public class Test : MonoBehaviour
    {
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

local meta = {
    __newindex = function(t, key, value)
print('CheckAAAAAAAAA', key, value)
        if t.__fieldMap ~= nil and t.__objectBinderId ~= nil and t.__fieldMap[key] ~= nil then
print('CheckBBBBBBBBBBBBBBBBB')
            local binderId = t.__objectBinderId
            local keyId = t.__fieldMap[key]
            local fieldBindTypeEnum = CS.LuaObjectBind.ObjectBinder.GetFieldBindTypeEnum(binderId, keyId)
            local Enum = CS.LuaObjectBind.FieldBindTypeEnum
            local ObjectBinder = CS.LuaObjectBind.ObjectBinder
            if fieldBindTypeEnum == Enum.None then
                return nil
            else
print('ChecCCCCCCCCCCC')
                if fieldBindTypeEnum == Enum.String then
print('CheckDDDDDDDDDDDDDDDDDDDDDDDD')
                    return ObjectBinder.HandleString(binderId, keyId, value)
                elseif fieldBindTypeEnum == Enum.Int then
                    return ObjectBinder.HandleInt(binderId, keyId, value)
                elseif fieldBindTypeEnum == Enum.Float then
                    return ObjectBinder.HandleFloat(binderId, keyId, value)
                elseif fieldBindTypeEnum == Enum.Bool then
                    return ObjectBinder.HandleBool(binderId, keyId, value)
                elseif fieldBindTypeEnum == Enum.Vector2 then
                    return ObjectBinder.HandleVector2(binderId, keyId, value)
                elseif fieldBindTypeEnum == Enum.Vector3 then
                    return ObjectBinder.HandleVector3(binderId, keyId, value)
                end
            end
        end
    end,

    __index = function(t, key)
        if key == '__objectIdMap' or key == '__objectBinderId' or key == 'fieldMap' then
            return nil
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
}

setmetatable(AAA, meta)
--AAA.goC:SetActive(false)
--AAA.txtB.text = 'AAAA'
print('ReSSSSSSSSS')
print(AAA.TxtNew)
print('ReSSSSSSSSS')
AAA.TxtNew = 'CCC'


                ");
            }
            else
            {
                Debug.LogError("ObjectBinder component not found.");
            }
        }
    }
}