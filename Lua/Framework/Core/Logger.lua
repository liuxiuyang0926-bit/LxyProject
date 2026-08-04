local string_format = string.format
local select = select
local tostring = tostring

local Logger = {}

Logger.Levels = {
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
    Exception = 4,
    Fatal = 5,
    Close = 255,
}

local globalLevel = Logger.Levels.Debug
local tagLoggers = {}

local function formatMessage(message, ...)
    local text = tostring(message)
    if select("#", ...) == 0 then
        return text
    end

    local succeeded, formatted = pcall(string_format, text, ...)
    if succeeded then
        return formatted
    end
    return text .. " [format error: " .. tostring(formatted) .. "]"
end

local function write(methodName, prefix, message, ...)
    local text = prefix .. formatMessage(message, ...)
    local unityDebug = CS and CS.UnityEngine and CS.UnityEngine.Debug
    if unityDebug and unityDebug[methodName] then
        unityDebug[methodName](text)
        return
    end

    print(text)
end

function Logger.SetLevel(level)
    globalLevel = tonumber(level) or Logger.Levels.Debug
end

function Logger.Debug(message, ...)
    if globalLevel <= Logger.Levels.Debug then
        write("Log", "[Lua] ", message, ...)
    end
end

function Logger.Info(message, ...)
    if globalLevel <= Logger.Levels.Info then
        write("Log", "[Lua] ", message, ...)
    end
end

function Logger.Warning(message, ...)
    if globalLevel <= Logger.Levels.Warn then
        write("LogWarning", "[Lua] ", message, ...)
    end
end

function Logger.Error(message, ...)
    if globalLevel <= Logger.Levels.Error then
        write("LogError", "[Lua] ", message, ...)
    end
end

Logger.Exception = Logger.Error
Logger.Fatal = Logger.Error

local TagLogger = {}
TagLogger.__index = TagLogger

function TagLogger:SetLevel(level)
    self.level = tonumber(level) or globalLevel
end

function TagLogger:Debug(message, ...)
    if self.level <= Logger.Levels.Debug then
        write("Log", self.prefix, message, ...)
    end
end

function TagLogger:Info(message, ...)
    if self.level <= Logger.Levels.Info then
        write("Log", self.prefix, message, ...)
    end
end

function TagLogger:Warning(message, ...)
    if self.level <= Logger.Levels.Warn then
        write("LogWarning", self.prefix, message, ...)
    end
end

function TagLogger:Error(message, ...)
    if self.level <= Logger.Levels.Error then
        write("LogError", self.prefix, message, ...)
    end
end

TagLogger.Exception = TagLogger.Error
TagLogger.Fatal = TagLogger.Error

function Logger.GetTagLogger(tag)
    local normalizedTag = tostring(tag or "Unknown")
    local logger = tagLoggers[normalizedTag]
    if logger then
        return logger
    end

    logger = setmetatable({
        level = globalLevel,
        prefix = "[Lua][" .. normalizedTag .. "] ",
    }, TagLogger)
    tagLoggers[normalizedTag] = logger
    return logger
end

_G.Logger = Logger

return Logger
