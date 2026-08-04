using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;
using LuaObjectBind;

public class ObjectBinderPrefabSaveChecker
{
    public static string GetLostBindMsg(ObjectBinder binder)
    {
        if (binder == null || binder.gameObject == null)
            return "";

        StringBuilder sb = new StringBuilder();
        string gameObjectName = binder.gameObject.name;
        bool hasLostBind = false;

        // 检查bindValues
        if (binder.bindValues != null && binder.bindValues.Binds != null)
        {
            for (int i = 0; i < binder.bindValues.Binds.Count; i++)
            {
                var bindValue = binder.bindValues.Binds[i];
                if (bindValue == null)
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【Bind】绑定值项为空: 第{i + 1}项");
                    continue;
                }

                // 检测Key是否为空
                if (string.IsNullOrEmpty(bindValue.Name))
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【Bind】Key为空: 第{i + 1}项");
                    continue;
                }

                // 检测绑定对象是否为空
                if (bindValue.GetValue<Object>() == null)
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【Bind】[{bindValue.Name}]绑定的对象丢失: 第{i + 1}项");
                }
            }
        }

        // 检查fieldBindValues
        if (binder.fieldBindValues != null && binder.fieldBindValues.Binds != null)
        {
            for (int i = 0; i < binder.fieldBindValues.Binds.Count; i++)
            {
                var fieldBindValue = binder.fieldBindValues.Binds[i];
                if (fieldBindValue == null)
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【FieldBind】字段绑定项为空: 第{i + 1}项");
                    continue;
                }

                // 检测Key是否为空
                if (string.IsNullOrEmpty(fieldBindValue.Name))
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【FieldBind】字段Key为空: 第{i + 1}项");
                    continue;
                }

                // 检测绑定对象是否为空
                if (fieldBindValue.GetValue<Object>() == null)
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【FieldBind】字段Key[{fieldBindValue.Name}]绑定的对象丢失: 第{i + 1}项");
                }
            }
        }

        // 检查pathBindValues
        if (binder.pathBindValues != null && binder.pathBindValues.Binds != null)
        {
            for (int i = 0; i < binder.pathBindValues.Binds.Count; i++)
            {
                var pathBindValue = binder.pathBindValues.Binds[i];
                if (pathBindValue == null)
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【PathBind】绑定项为空: 第{i + 1}项");
                    continue;
                }

                // 检测Key是否为空
                if (string.IsNullOrEmpty(pathBindValue.Name))
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【PathBind】Key为空: 第{i + 1}项");
                    continue;
                }

                // 检测路径是否为空
                if (string.IsNullOrEmpty(pathBindValue.GetValue<string>()))
                {
                    hasLostBind = true;
                    sb.AppendLine($"• 【PathBind】Key[{pathBindValue.Name}]绑定的路径丢失: 第{i + 1}项");
                }
            }
        }

        if (hasLostBind)
            return $"[{gameObjectName}]\n{sb.ToString()}";
        else
            return string.Empty;
    }
}