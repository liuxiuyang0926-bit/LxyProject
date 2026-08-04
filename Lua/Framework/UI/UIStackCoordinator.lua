require("Framework.Core.DefineClass")

local UIStackCoordinator = DefineClass("UIStackCoordinator")

local function findIndex(stackRecords, config)
    if not config then
        return nil
    end

    for index, entry in ipairs(stackRecords) do
        if entry.LogicConfig == config
            or entry.LogicConfig.Id == config.Id then
            return index
        end
    end
    return nil
end

function UIStackCoordinator:PushPanel(
    stackRecords, config, userData)
    if not config or config.IgnoreStack then
        return nil, false
    end

    local previousTop = stackRecords[#stackRecords]
    local existingIndex = findIndex(stackRecords, config)
    if existingIndex == #stackRecords then
        stackRecords[existingIndex].preUserData = userData
        return nil, false
    end

    if existingIndex then
        table.remove(stackRecords, existingIndex)
    end

    table.insert(stackRecords, {
        LogicConfig = config,
        preUserData = userData,
    })

    return previousTop, true
end

function UIStackCoordinator:PopPanel(
    stackRecords, config, isClear)
    if not config or config.IgnoreStack or isClear then
        return nil, false
    end

    local index = findIndex(stackRecords, config)
    if not index then
        return nil, false
    end

    local wasTop = index == #stackRecords
    table.remove(stackRecords, index)
    return wasTop and stackRecords[#stackRecords] or nil, wasTop
end

function UIStackCoordinator:RemovePanel(stackRecords, config)
    local index = findIndex(stackRecords, config)
    if not index then
        return false
    end
    table.remove(stackRecords, index)
    return true
end

function UIStackCoordinator:GetTop(stackRecords)
    return stackRecords[#stackRecords]
end

function UIStackCoordinator:Contains(stackRecords, config)
    return findIndex(stackRecords, config) ~= nil
end

return UIStackCoordinator
