local table_pack = table.pack
local table_unpack = table.unpack

---将对象方法绑定为普通回调函数，并保留预绑定参数。
---@param target table
---@param method function
---@vararg any
---@return function
function Func(target, method, ...)
    assert(target ~= nil, "Func target is nil")
    assert(type(method) == "function", "Func method must be a function")

    local boundArguments = table_pack(...)
    return function(...)
        if target.destroyed then
            if target.logger then
                target.logger:Warning("call method after destroy")
            end
            return nil
        end

        local runtimeArguments = table_pack(...)
        local arguments = {}
        local argumentCount = 0
        for index = 1, boundArguments.n do
            argumentCount = argumentCount + 1
            arguments[argumentCount] = boundArguments[index]
        end
        for index = 1, runtimeArguments.n do
            argumentCount = argumentCount + 1
            arguments[argumentCount] = runtimeArguments[index]
        end

        local results = table_pack(xpcall(function()
            return method(
                target,
                table_unpack(arguments, 1, argumentCount))
        end, debug.traceback))
        if not results[1] then
            if target.logger then
                target.logger:Error("callback failed:\n%s", results[2])
            else
                CS.UnityEngine.Debug.LogError(
                    "[Lua] callback failed:\n" .. tostring(results[2]))
            end
            return nil
        end

        return table_unpack(results, 2, results.n)
    end
end

return Func
