---@meta

---@generic T: LuaClass
---@class LuaClass<T>
---@field __class__ boolean
---@field protected __cname string
---@field logger table
---@field class T
---@field super LuaClass|nil
---@field destroyed boolean
---@field instanceId integer
local LuaClass = {}

---@protected
function LuaClass:ctor(...) end

---@protected
function LuaClass:dtor() end

function LuaClass:delete() end

---@return LuaClass
function LuaClass.new(...) return {} end

---@param class LuaClass
---@return boolean
function LuaClass:IsInstance(class) return false end

---@param baseClass LuaClass
---@return boolean
function LuaClass.IsSubClassOf(baseClass) return false end

---@param callback function
---@param interval number
---@param loop boolean|nil
---@param unscaledTime boolean|nil
---@return integer
function LuaClass:AddTimer(callback, interval, loop, unscaledTime)
    return 0
end

---@param timerId integer
function LuaClass:RemoveTimer(timerId) end

---@class LuaSingletonClass<T>: LuaClass<T>
---@field private __instanced__ boolean
local LuaSingletonClass = {}

---@return LuaSingletonClass
function LuaSingletonClass.new(...) return {} end

return nil
