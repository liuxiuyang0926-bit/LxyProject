using System;
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace VGame.GameLogic.Editor.LuaUI
{
    [CreateAssetMenu(fileName = "BindTemplateList", menuName = "Tools/LuaUI/Create Bind Template List", order = 1)]
    public class BindTemplateList : ScriptableObject
    {

        [Serializable]
        public class FuncInfo
        {
            [Serializable]
            public struct Parameter
            {
                [ShowInInspector, ReadOnly]
                /// <summary>
                /// 公开的类型数据。
                /// </summary>
                public Type Type;
                
                [LabelText("参数类型"), HorizontalGroup]
                /// <summary>
                /// 公开的类型名称数据。
                /// </summary>
                public string typeName;
                
                [LabelText("参数名"), HorizontalGroup]
                /// <summary>
                /// 公开的param名称数据。
                /// </summary>
                public string paramName;
            }
            
            [LabelText("绑定函数方法")]
            [ValueDropdown("GetFunNameList")]
            /// <summary>
            /// 公开的fun名称数据。
            /// </summary>
            public string funName = "OnClick";

            [HideInInspector]
            /// <summary>
            /// 公开的widget类型数据。
            /// </summary>
            public string widgetType;

            [LabelText("参数列表"), ListDrawerSettings(ShowFoldout = true, HideAddButton = true, HideRemoveButton = true)]
            /// <summary>
            /// 公开的parameters数据。
            /// </summary>
            public List<Parameter> parameters = new List<Parameter>();

            // FunName的规则
            // 所有Public类型的成员变量或者属性，类型为继承自UnityEvent或者是UnityEvent
            /// <summary>
            /// 执行判断是否Unity事件类型相关逻辑。
            /// </summary>
            private bool IsUnityEventType(Type type)
            {
                if (type == null) return false;
                if (type == typeof(UnityEngine.Events.UnityEvent)) return true;
                while (type != null && type != typeof(object))
                {
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UnityEngine.Events.UnityEvent<>))
                        return true;
                    type = type.BaseType;
                }
                return false;
            }

            /// <summary>
            /// 获取Fun名称列表。
            /// </summary>
            public IEnumerable<string> GetFunNameList()
            {
                if (string.IsNullOrEmpty(widgetType))
                    yield break;

                var type = Type.GetType(widgetType);
                if (type == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(widgetType);
                        if (type != null)
                            break;
                    }
                }
                if (type == null)
                    yield break;

                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (IsUnityEventType(field.FieldType) || typeof(UnityEngine.Events.UnityEvent).IsAssignableFrom(field.FieldType))
                        yield return field.Name;
                }

                var properties = type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var prop in properties)
                {
                    if (IsUnityEventType(prop.PropertyType) || typeof(UnityEngine.Events.UnityEvent).IsAssignableFrom(prop.PropertyType))
                        yield return prop.Name;
                }
            }

            /// <summary>
            /// 尝试获取Unity事件GenericArgs，并返回是否成功。
            /// </summary>
            private bool TryGetUnityEventGenericArgs(Type type, out Type[] genericArgs)
            {
                genericArgs = null;
                while (type != null && type != typeof(object))
                {
                    if (type.IsGenericType && type.GetGenericTypeDefinition().FullName.StartsWith("UnityEngine.Events.UnityEvent"))
                    {
                        genericArgs = type.GetGenericArguments();
                        return true;
                    }
                    type = type.BaseType;
                }
                return false;
            }

            /// <summary>
            /// 执行AutoGenFunctionParams相关逻辑。
            /// </summary>
            public void AutoGenFunctionParams()
            {
                if (string.IsNullOrEmpty(widgetType) || string.IsNullOrEmpty(funName))
                    return;

                var type = Type.GetType(widgetType);
                if (type == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(widgetType);
                        if (type != null)
                            break;
                    }
                }
                if (type == null)
                    return;

                // 查找字段（忽略大小写）
                var field = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    .FirstOrDefault(f => string.Equals(f.Name, funName, StringComparison.OrdinalIgnoreCase));
                Type eventType = field?.FieldType;

                // 查找属性（忽略大小写）
                if (eventType == null)
                {
                    var prop = type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                        .FirstOrDefault(p => string.Equals(p.Name, funName, StringComparison.OrdinalIgnoreCase));
                    eventType = prop?.PropertyType;
                }

                if (eventType == null)
                    return;

                if (TryGetUnityEventGenericArgs(eventType, out var genericArgs))
                {
                    if (genericArgs.Length != parameters.Count)
                    {
                        parameters.Clear();
                        foreach (var argType in genericArgs)
                        {
                            parameters.Add(GenerateParameter(argType));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < genericArgs.Length; i++)
                        {
                            var oldParam = parameters[i];
                            var newParam = GenerateParameter(genericArgs[i]);
                            newParam.paramName = oldParam.paramName;
                            parameters[i] = newParam;
                        }
                    }
                }
                else
                {
                    parameters.Clear();
                }
            }

            private static Dictionary<Type, string> cs2LuaType = new()
            {
                {typeof(bool), "boolean"},
                {typeof(string), "string"},
                {typeof(byte), "byte"},
                {typeof(sbyte), "sbyte"},
                {typeof(short), "short"},
                {typeof(ushort), "ushort"},
                {typeof(int), "int"},
                {typeof(uint), "uint"},
                {typeof(float), "float"},
                {typeof(double), "double"},
            };
            /// <summary>
            /// 执行Generate参数相关逻辑。
            /// </summary>
            private static Parameter GenerateParameter(Type type)
            {
                if (!cs2LuaType.TryGetValue(type, out var typeName))
                    typeName = type.Name;
                
                return new Parameter()
                {
                    Type = type,
                    paramName = typeName,
                    typeName = typeName,
                };
            }
        }

        [Serializable]
        public class BindTemplate
        {
            // 组件类型
            [LabelText("组件类型")]
            [ValueDropdown("GetFunTypeNameList")]
            [OnValueChanged("OnWidgetTypeChanged")]
            /// <summary>
            /// 公开的widget类型数据。
            /// </summary>
            public string widgetType;

            [LabelText("绑定函数组")]
            /// <summary>
            /// 公开的funcInfos数据。
            /// </summary>
            public List<FuncInfo> funcInfos = new List<FuncInfo>();

            // 当组件类型改变时调用
            /// <summary>
            /// 响应Widget类型Changed事件。
            /// </summary>
            private void OnWidgetTypeChanged()
            {
                RefreshFunctionNames();
            }

            // 刷新函数方法列表
            /// <summary>
            /// 刷新Function名称。
            /// </summary>
            public void RefreshFunctionNames()
            {
                foreach (var func in funcInfos)
                {
                    func.widgetType = widgetType;
                    
                    // 获取可用的函数名列表
                    var availableFunctions = func.GetFunNameList().ToList();
                    
                    // 如果列表为空，设置为"Null"
                    if (availableFunctions.Count == 0)
                    {
                        func.funName = "Null";
                    }
                    else
                    {
                        // 如果当前函数名不在可用列表中，设置为第一个可用函数名
                        if (!availableFunctions.Contains(func.funName))
                        {
                            func.funName = availableFunctions[0];
                        }
                    }
                    func.AutoGenFunctionParams();
                }
            }

            /// <summary>
            /// 获取Fun类型名称列表。
            /// </summary>
            public IEnumerable<string> GetFunTypeNameList()
            {
                List<Type> types = new List<Type> {
                 typeof(Button),
                 typeof(Toggle),
                 typeof(InputField),
                 //typeof(ButtonEx),
                 typeof(InputField),
                 //typeof(ScrollViewEx),
                 typeof(Toggle),
                 typeof(Image),
                 typeof(Text),
                 typeof(Slider),
                 //typeof(DropDown),
                 typeof(ToggleGroup),
            };
                foreach (var type in types)
                {
                    yield return type.FullName;
                }
            }
        }

        /// <summary>
        /// 公开的绑定Templates数据。
        /// </summary>
        public List<BindTemplate> bindTemplates = new List<BindTemplate>();

        void OnValidate()
        {
            RefreshBindTemplateList();
        }

        /// <summary>
        /// 刷新绑定Template列表。
        /// </summary>
        private void RefreshBindTemplateList()
        {
            UnityEngine.Debug.Log("### RefreshBindTemplateList ###");
            foreach (var template in bindTemplates)
                foreach (var func in template.funcInfos)
                {
                    func.widgetType = template.widgetType;
                    func.AutoGenFunctionParams();
                }
        }
    }
}