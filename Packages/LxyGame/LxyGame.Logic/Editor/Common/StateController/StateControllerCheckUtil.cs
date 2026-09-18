using System.Collections.Generic;
using System.Text;
using StateControl.Runtime;

namespace StateControl.Editor
{
    /// <summary>
    /// StateController 检查工具类，提供 ModifierRecordGroup 的丢失引用检测等共享方法
    /// </summary>
    public static class StateControllerCheckUtil
    {
        /// <summary>
        /// 检查一个 ModifierRecordGroup 是否有问题（null、Target丢失、TargetObject丢失、Records条目丢失、DefaultRecord丢失）
        /// </summary>
        public static bool HasRecordGroupProblem(ModifierRecordGroup recordGroup)
        {
            if (recordGroup == null)
                return true;
            if (recordGroup.Target == null || recordGroup.Target.TargetObject == null)
                return true;
            if (recordGroup.Records != null)
            {
                foreach (var record in recordGroup.Records)
                {
                    if (record == null)
                        return true;
                }
            }
            if (recordGroup.DefaultRecord == null)
                return true;
            return false;
        }

        /// <summary>
        /// 检查一个 StateController 是否存在任何有问题的 ModifierRecordGroup
        /// </summary>
        public static bool HasAnyProblem(StateController controller)
        {
            if (controller == null) return false;
            foreach (var recordGroup in controller.ModifierRecordGroups)
            {
                if (HasRecordGroupProblem(recordGroup))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取与有问题的 ModifierRecordGroup 关联的所有状态组名称
        /// </summary>
        public static HashSet<string> GetProblematicStateGroupNames(StateController controller)
        {
            var result = new HashSet<string>();
            if (controller == null) return result;
            foreach (var recordGroup in controller.ModifierRecordGroups)
            {
                if (!HasRecordGroupProblem(recordGroup))
                    continue;
                foreach (var groupName in recordGroup.GetAllStateGroup())
                {
                    result.Add(groupName);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取所有被至少一个 ModifierRecordGroup 绑定的状态组名称
        /// </summary>
        public static HashSet<string> GetBoundStateGroupNames(StateController controller)
        {
            var result = new HashSet<string>();
            if (controller == null) return result;
            foreach (var recordGroup in controller.ModifierRecordGroups)
            {
                if (recordGroup == null) continue;
                foreach (var groupName in recordGroup.GetAllStateGroup())
                {
                    result.Add(groupName);
                }
            }
            return result;
        }

        /// <summary>
        /// 检查 StateController 的所有 ModifierRecordGroup，返回详细错误信息列表
        /// </summary>
        public static List<string> GetDetailedErrors(StateController controller)
        {
            var errors = new List<string>();
            if (controller == null) return errors;
            var groups = controller.ModifierRecordGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                string groupLabel = $"ModifierRecordGroups[{i}]";

                if (group == null)
                {
                    errors.Add($"{groupLabel} 整个Group为null");
                    continue;
                }

                if (group.Target == null)
                {
                    errors.Add($"{groupLabel} 的 Target 为null");
                }
                else if (group.Target.TargetObject == null)
                {
                    errors.Add($"{groupLabel} 的 Target.TargetObject 丢失 (ModifierType={group.Target.ModifierType})");
                }

                if (group.DefaultRecord == null)
                {
                    errors.Add($"{groupLabel} 的 DefaultRecord 为null");
                }

                if (group.Records != null)
                {
                    for (int r = 0; r < group.Records.Count; r++)
                    {
                        if (group.Records[r] == null)
                        {
                            errors.Add($"{groupLabel}.Records[{r}] 为null");
                        }
                    }
                }
            }
            return errors;
        }
    }
}
