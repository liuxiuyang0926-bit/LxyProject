local ObjectBinder = CS.LuaObjectBind.ObjectBinder
local FieldBindType = CS.LuaObjectBind.FieldBindTypeEnum

local UIObjectBinderProxy = {}

local internalKeys = {
    __objectBinderId = true,
    __objectIdMap = true,
    __elementMap = true,
    __fieldMap = true,
    __pathMap = true,
    __stateControlMap = true,
}

local function readField(binderId, keyId)
    local fieldType = ObjectBinder.GetFieldBindTypeEnum(
        binderId,
        keyId)
    if fieldType == FieldBindType.String then
        return ObjectBinder.GetString(binderId, keyId)
    elseif fieldType == FieldBindType.Int then
        return ObjectBinder.GetInt(binderId, keyId)
    elseif fieldType == FieldBindType.Float then
        return ObjectBinder.GetFloat(binderId, keyId)
    elseif fieldType == FieldBindType.Bool then
        return ObjectBinder.GetBool(binderId, keyId)
    elseif fieldType == FieldBindType.Vector2 then
        return ObjectBinder.GetVector2(binderId, keyId)
    elseif fieldType == FieldBindType.Vector3 then
        return ObjectBinder.GetVector3(binderId, keyId)
    elseif fieldType == FieldBindType.LuaTable then
        return ObjectBinder.GetLuaTable(binderId, keyId)
    end
    return nil
end

local function writeField(binderId, keyId, value)
    local fieldType = ObjectBinder.GetFieldBindTypeEnum(
        binderId,
        keyId)
    if fieldType == FieldBindType.String then
        ObjectBinder.HandleString(binderId, keyId, value)
    elseif fieldType == FieldBindType.Int then
        ObjectBinder.HandleInt(binderId, keyId, value)
    elseif fieldType == FieldBindType.Float then
        ObjectBinder.HandleFloat(binderId, keyId, value)
    elseif fieldType == FieldBindType.Bool then
        ObjectBinder.HandleBool(binderId, keyId, value)
    elseif fieldType == FieldBindType.Vector2 then
        ObjectBinder.HandleVector2(binderId, keyId, value)
    elseif fieldType == FieldBindType.Vector3 then
        ObjectBinder.HandleVector3(binderId, keyId, value)
    elseif fieldType == FieldBindType.LuaTable then
        ObjectBinder.HandleLuaTable(binderId, keyId, value)
    else
        return false
    end
    return true
end

local function resolveWidgetValue(widget, key)
    if internalKeys[key] then
        return nil
    end

    local binderId = rawget(widget, "__objectBinderId")
    if binderId == nil then
        return nil
    end

    local objectMap = rawget(widget, "__objectIdMap")
    local objectId = objectMap and objectMap[key] or nil
    if objectId ~= nil then
        return ObjectBinder.GetObject(binderId, objectId)
    end

    local elementMap = rawget(widget, "__elementMap")
    local elementId = elementMap and elementMap[key] or nil
    if elementId ~= nil then
        local binder = ObjectBinder.GetBinderLogicElement(
            binderId,
            elementId)
        return binder and binder.BindedTable or nil
    end

    local pathMap = rawget(widget, "__pathMap")
    local path = pathMap and pathMap[key] or nil
    if path ~= nil then
        return path
    end

    local fieldMap = rawget(widget, "__fieldMap")
    local fieldId = fieldMap and fieldMap[key] or nil
    if fieldId ~= nil then
        return readField(binderId, fieldId)
    end

    return nil
end

function UIObjectBinderProxy.Attach(widget)
    assert(type(widget) == "table", "ObjectBinder Widget must be a table")

    local oldMeta = getmetatable(widget)
    local oldIndex = oldMeta and oldMeta.__index or nil
    local oldNewIndex = oldMeta and oldMeta.__newindex or nil
    local newMeta = {}
    if oldMeta then
        for key, value in pairs(oldMeta) do
            newMeta[key] = value
        end
    end

    newMeta.__index = function(target, key)
        local value = resolveWidgetValue(target, key)
        if value ~= nil then
            return value
        end

        if type(oldIndex) == "function" then
            return oldIndex(target, key)
        elseif type(oldIndex) == "table" then
            return oldIndex[key]
        end
        return nil
    end

    newMeta.__newindex = function(target, key, value)
        local binderId = rawget(target, "__objectBinderId")
        local fieldMap = rawget(target, "__fieldMap")
        local fieldId = fieldMap and fieldMap[key] or nil
        if binderId ~= nil and fieldId ~= nil and
            writeField(binderId, fieldId, value) then
            return
        end

        if type(oldNewIndex) == "function" then
            oldNewIndex(target, key, value)
        elseif type(oldNewIndex) == "table" then
            oldNewIndex[key] = value
        else
            rawset(target, key, value)
        end
    end

    setmetatable(widget, newMeta)
    return widget
end

return UIObjectBinderProxy
