local rawget = rawget
local rawset = rawset
local pairs = pairs
local ipairs = ipairs
local type = type
local table_insert = table.insert
local table_pack = table.pack
local table_unpack = table.unpack
local setmetatable = setmetatable
local string_format = string.format
local Logger = rawget(_G, "Logger")
    or require("Framework.Core.Logger")

local instanceId = 0

local function GetCtorList(class)
    local ctorList = rawget(class, "__ctor_list__")
    if ctorList then
        return ctorList
    end

    ctorList = {}
    rawset(class, "__ctor_list__", ctorList)
    while class do
        local ctor = rawget(class, "ctor")
        if ctor then
            table_insert(ctorList, ctor)
        end
        class = class.super
    end
    return ctorList
end

local function GetDtorList(class)
    local dtorList = rawget(class, "__dtor_list__")
    if dtorList then
        return dtorList
    end

    dtorList = {}
    rawset(class, "__dtor_list__", dtorList)
    while class do
        local dtor = rawget(class, "dtor")
        if dtor then
            table_insert(dtorList, 1, dtor)
        end
        class = class.super
    end
    return dtorList
end

local function ToString(instance)
    return string_format(
        "%s(instanceId: %d)",
        tostring(instance.__cname),
        tonumber(instance.instanceId) or -1)
end

local destroyedInstanceMt = {
    __tostring = ToString,
    __index = function(instance, key)
        if key == "destroyed" then
            return true
        end
        return rawget(instance, key)
    end,
}

local function delete(instance)
    if instance.destroyed then
        local logger = instance.logger
        if logger then
            logger:Warning(
                "delete destroyed instance: %s",
                tostring(instance))
        end
        return
    end

    local class = instance.class
    local id = instance.instanceId
    for _, dtor in ipairs(GetDtorList(class)) do
        dtor(instance)
    end

    for key in pairs(instance) do
        rawset(instance, key, nil)
    end
    instance.instanceId = id
    instance.__cname = class.__cname
    instance.logger = class.logger
    setmetatable(instance, destroyedInstanceMt)
end

local function IsInstance(instance, class)
    if instance == nil or class == nil then
        return false
    end

    local currentClass = instance.class
    while currentClass do
        if currentClass == class then
            return true
        end
        currentClass = currentClass.super
    end
    return false
end

local coroutineModule
local function GetCoroutineModule()
    if coroutineModule then
        return coroutineModule
    end

    local succeeded, moduleOrError = pcall(
        require,
        "Framework.Coroutine.LuaCoroutine")
    if not succeeded then
        error(
            "Framework.Coroutine.LuaCoroutine 尚未接入：" ..
            tostring(moduleOrError),
            3)
    end
    coroutineModule = moduleOrError
    return coroutineModule
end

local function StartCoroutine(instance, ...)
    return GetCoroutineModule().StartCoroutine(instance, ...)
end

local function InstantiateCoroutine(instance, ...)
    return GetCoroutineModule().InstantiatePrefabCoroutine(instance, ...)
end

local function LoadAssetCoroutine(instance, ...)
    return GetCoroutineModule().LoadAssetCoroutine(instance, ...)
end

local function StopCoroutine(_, coroutineId)
    return GetCoroutineModule().StopCoroutine(coroutineId)
end

local function GetTimerManager()
    local game = rawget(_G, "Game")
    if not game or not game.TimerManager then
        error("Game.TimerManager 尚未接入。", 3)
    end
    return game.TimerManager
end

local function AddTimer(instance, callback, interval, loop, unscaledTime)
    return GetTimerManager():AddTimer(
        callback,
        instance,
        interval,
        loop,
        unscaledTime)
end

local function RemoveTimer(_, timerId)
    return GetTimerManager():RemoveTimer(timerId)
end

local function AddRealtimeTimer(instance, callback, interval, loop)
    return GetTimerManager():AddRealtimeTimer(
        callback,
        instance,
        interval,
        loop)
end

local function AddSecondTimer(instance, callback)
    return GetTimerManager():AddSecondTimer(callback, instance)
end

local function RemoveSecondTimer(_, timerId)
    return GetTimerManager():RemoveSecondTimer(timerId)
end

local function IsProfilerEnabled()
    local const = rawget(_G, "Const")
    return const
        and const.Switch
        and const.Switch.DefineClassProfiler == true
        and rawget(_G, "LuaProfiler") ~= nil
end

local function ProfileFunction(classname, functionName, callback)
    return function(...)
        local profiler = rawget(_G, "LuaProfiler")
        if not profiler or not profiler.Start then
            return callback(...)
        end

        local scope = profiler.Start(
            "[LUA] " .. classname .. "." .. functionName)
        local results = table_pack(xpcall(
            callback,
            debug.traceback,
            ...))

        if scope then
            pcall(function()
                if scope.Dispose then
                    scope:Dispose()
                elseif scope.Close then
                    scope:Close()
                end
            end)
        end

        if not results[1] then
            error(results[2], 0)
        end
        return table_unpack(results, 2, results.n)
    end
end

---创建一个类，不支持多继承。
---@param classname string
---@param super table|nil
---@return table
function DefineClass(classname, super)
    assert(
        type(classname) == "string" and classname ~= "",
        "classname must be a non-empty string")
    assert(
        super == nil or type(super) == "table",
        "super must be a class or nil")

    local class = {
        __class__ = true,
        __cname = classname,
        logger = Logger.GetTagLogger(classname),
    }
    class.__index = class

    if super then
        class.super = super
        setmetatable(class, { __index = super })
    end

    if IsProfilerEnabled() then
        local hookFunctions = {}
        local classMeta = {}
        classMeta.__newindex = function(target, key, value)
            if type(value) ~= "function"
                or key == "ctor"
                or key == "dtor" then
                rawset(target, key, value)
                return
            end
            hookFunctions[key] = ProfileFunction(
                classname,
                key,
                value)
        end
        classMeta.__index = function(_, key)
            return hookFunctions[key] or (super and super[key])
        end
        setmetatable(class, classMeta)
    end

    local instanceMeta = {
        __index = class,
        __tostring = ToString,
    }

    function class.new(...)
        instanceId = instanceId + 1
        local instance = {
            class = class,
            instanceId = instanceId,
            destroyed = false,
        }
        instanceMeta.__gc = class.__gc
        setmetatable(instance, instanceMeta)
        for _, ctor in ipairs(GetCtorList(class)) do
            ctor(instance, ...)
        end
        return instance
    end

    class.on_ctor_or_dtor_redefine = function()
        rawset(class, "__ctor_list__", nil)
        rawset(class, "__dtor_list__", nil)
    end
    class.delete = delete
    class.IsInstance = IsInstance
    class.StartCoroutine = StartCoroutine
    class.InstantiateCoroutine = InstantiateCoroutine
    class.LoadAssetCoroutine = LoadAssetCoroutine
    class.StopCoroutine = StopCoroutine
    class.AddTimer = AddTimer
    class.RemoveTimer = RemoveTimer
    class.AddRealtimeTimer = AddRealtimeTimer
    class.AddSecondTimer = AddSecondTimer
    class.RemoveSecondTimer = RemoveSecondTimer
    class.IsSubClassOf = function(baseClass)
        local currentClass = class
        while currentClass do
            if currentClass == baseClass then
                return true
            end
            currentClass = currentClass.super
        end
        return false
    end

    return class
end

if rawget(_G, "UNITY_EDITOR") == true then
    local instanceCounter = setmetatable({}, {
        __index = function(target, key)
            rawset(target, key, 0)
            return 0
        end,
    })

    function DumpInstanceCounter()
        local items = {}
        for className, count in pairs(instanceCounter) do
            table_insert(items, { className, count })
        end
        table.sort(items, function(left, right)
            return left[2] > right[2]
        end)

        Logger.Debug("============= 实例计数器 =============")
        for _, item in ipairs(items) do
            if item[2] < 10 then
                break
            end
            Logger.Debug("%s: %d", item[1], item[2])
        end
        Logger.Debug("============= 实例计数器 =============")
    end

    local RawDefineClass = DefineClass
    function DefineClass(classname, super)
        local class = RawDefineClass(classname, super)
        local new = class.new
        class.new = function(...)
            instanceCounter[classname] = instanceCounter[classname] + 1
            return new(...)
        end
        class.delete = function(instance)
            if not instance.destroyed then
                instanceCounter[classname] =
                    instanceCounter[classname] - 1
            end
            return delete(instance)
        end
        return class
    end
end

---创建一个单例类，不支持多继承。
---@param classname string
---@param super table|nil
---@return table
function DefineSingletonClass(classname, super)
    local class = DefineClass(classname, super)

    function class.new(...)
        if rawget(class, "__instanced__") then
            return class
        end

        for _, ctor in ipairs(GetCtorList(class)) do
            ctor(class, ...)
        end
        class.__instanced__ = true
        class.instanceId = -1
        return class
    end
    class.delete = nil
    return class
end

return DefineClass
