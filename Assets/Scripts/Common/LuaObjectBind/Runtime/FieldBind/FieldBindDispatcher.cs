using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using XLua;
using Object = UnityEngine.Object;

namespace LuaObjectBind
{
    public class FieldBindHandlerAttribute: Attribute
    {
    }
    
    public interface IFieldBindHandler
    {
        //todo 以后有时间写个代码生成器
        #region 各种类型的Handle和Get
        void InternalHandleString(Object obj, FieldBindEnum type, string value);
        void InternalHandleBool(Object obj, FieldBindEnum type, bool value);
        void InternalHandleInt(Object obj, FieldBindEnum type, int value);
        void InternalHandleFloat(Object obj, FieldBindEnum type, float value);
        void InternalHandleVector2(Object obj, FieldBindEnum type, Vector2 value);
        void InternalHandleVector3(Object obj, FieldBindEnum type, Vector3 value);
        void InternalHandleLuaTable(Object obj, FieldBindEnum type, LuaTable value);
        
        string InternalGetString(Object obj, FieldBindEnum type);
        bool InternalGetBool(Object obj, FieldBindEnum type);
        int InternalGetInt(Object obj, FieldBindEnum type);
        float InternalGetFloat(Object obj, FieldBindEnum type);
        Vector2 InternalGetVector2(Object obj, FieldBindEnum type);
        Vector3 InternalGetVector3(Object obj, FieldBindEnum type);
        LuaTable InternalGetLuaTable(Object obj, FieldBindEnum type);
        #endregion
        Type GetType();
    }
    
    [FieldBindHandler]
    public abstract class AFieldBindHandler<T>: IFieldBindHandler where T : Object
    {
        //todo 以后有时间写个代码生成器
        #region 各种类型的Handle和Get
        protected virtual void HandleString(T obj, FieldBindEnum type, string value)
        {
        }

        public void InternalHandleString(Object obj, FieldBindEnum type, string value)
        {
            HandleString(obj as T, type, value);
        }
        
        protected virtual string GetString(T obj, FieldBindEnum type)
        {
            return string.Empty;
        }

        public string InternalGetString(Object obj, FieldBindEnum type)
        {
            return GetString(obj as T, type);
        }

        protected virtual void HandleBool(T obj, FieldBindEnum type, bool value)
        {
        }
        public void InternalHandleBool(Object obj, FieldBindEnum type, bool value)
        {
            HandleBool(obj as T, type, value);
        }
        
        protected virtual bool GetBool(T obj, FieldBindEnum type)
        {
            return false;
        }
        public bool InternalGetBool(Object obj, FieldBindEnum type)
        {
            return GetBool(obj as T, type);
        }

        protected virtual void HandleInt(T obj, FieldBindEnum type, int value)
        {
        }
        public void InternalHandleInt(Object obj, FieldBindEnum type, int value)
        {
            HandleInt(obj as T, type, value);
        }
        protected virtual int GetInt(T obj, FieldBindEnum type)
        {
            return 0;
        }
        public int InternalGetInt(Object obj, FieldBindEnum type)
        {
            return GetInt(obj as T, type);
        }
        
        protected virtual void HandleFloat(T obj, FieldBindEnum type, float value)
        {
        }
        public void InternalHandleFloat(Object obj, FieldBindEnum type, float value)
        {
            HandleFloat(obj as T, type, value);
        }
        protected virtual float GetFloat(T obj, FieldBindEnum type)
        {
            return 0;
        }
        public float InternalGetFloat(Object obj, FieldBindEnum type)
        {
            return GetFloat(obj as T, type);
        }
        protected virtual void HandleVector2(T obj, FieldBindEnum type, Vector2 value)
        {
        }
        public void InternalHandleVector2(Object obj, FieldBindEnum type, Vector2 value)
        {
            HandleVector2(obj as T, type, value);
        }
        protected virtual Vector2 GetVector2(T obj, FieldBindEnum type)
        {
            return Vector2.zero;
        }
        public Vector2 InternalGetVector2(Object obj, FieldBindEnum type)
        {
            return GetVector2(obj as T, type);
        }
        protected virtual void HandleVector3(T obj, FieldBindEnum type, Vector3 value)
        {
        }
        public void InternalHandleVector3(Object obj, FieldBindEnum type, Vector3 value)
        {
            HandleVector3(obj as T, type, value);
        }
        protected virtual Vector3 GetVector3(T obj, FieldBindEnum type)
        {
            return Vector3.zero;
        }
        public Vector3 InternalGetVector3(Object obj, FieldBindEnum type)
        {
            return GetVector3(obj as T, type);
        }
        
        protected virtual void HandleLuaTable(T obj, FieldBindEnum type, LuaTable value)
        {
        }
        public void InternalHandleLuaTable(Object obj, FieldBindEnum type, LuaTable value)
        {
            HandleLuaTable(obj as T, type, value);
        }
        protected virtual LuaTable GetLuaTable(T obj, FieldBindEnum type)
        {
            return null;
        }
        public LuaTable InternalGetLuaTable(Object obj, FieldBindEnum type)
        {
            return GetLuaTable(obj as T, type);
        }
        #endregion
        
        public virtual Type GetType()
        {
            return typeof (T);
        }
    }
    
    public class FieldBindDispatcher: Singleton<FieldBindDispatcher>
    {
        private readonly Dictionary<Type, IFieldBindHandler> FieldBindHandlers = new();
        private readonly Dictionary<FieldBindEnum, FieldBindTypeEnum> BindTypeMap = new();

        public void Init()
        {
            Assembly assembly = typeof(FieldBindDispatcher).Assembly;
            var types = assembly.GetTypes();
            
            foreach (Type type in types)
            {
                if (type.IsAbstract)
                {
                    continue;
                }
                
                object[] objects = type.GetCustomAttributes(typeof(FieldBindHandlerAttribute), true);

                foreach (object o in objects)
                {
                    Register(type);
                    break;
                }
            }
            
            InitFieldTypeMap();
        }
        
        private void Register(Type type)
        {
            object obj = Activator.CreateInstance(type);

            IFieldBindHandler iFieldBindHandler = obj as IFieldBindHandler;
            if (iFieldBindHandler == null)
            {
                throw new Exception($"FieldBind handler not inherit IMActorHandler abstract class: {obj.GetType().FullName}");
            }

            Type objType = iFieldBindHandler.GetType();
            FieldBindHandlers.TryAdd(objType, iFieldBindHandler);
        }

        private void InitFieldTypeMap()
        {
            //遍历所有的FiledBindEnum和它的FieldBindType属性
            foreach (FieldBindEnum fieldBindEnum in Enum.GetValues(typeof(FieldBindEnum)))
            {
                FieldInfo fieldInfo = fieldBindEnum.GetType().GetField(fieldBindEnum.ToString());
                if (fieldInfo == null)
                {
                    continue;
                }
                
                object[] attributes = fieldInfo.GetCustomAttributes(typeof(FieldBindTypeAttribute), true);
                if (attributes.Length > 0)
                {
                    FieldBindTypeAttribute filedBindTypeAttribute = attributes[0] as FieldBindTypeAttribute;
                    if (filedBindTypeAttribute != null)
                    {
                        BindTypeMap.Add(fieldBindEnum, filedBindTypeAttribute.FieldBindType);
                    }
                }
            }
        }
        
        public FieldBindTypeEnum GetFieldBindType(FieldBindEnum fieldBindEnum)
        {
            if (BindTypeMap.TryGetValue(fieldBindEnum, out FieldBindTypeEnum fieldBindType))
            {
                return fieldBindType;
            }
            return FieldBindTypeEnum.None;
        }
        
        #region 设置FieldBind值
        public void HandleString(Object obj, FieldBindEnum type, string value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }
            iFieldBindHandler.InternalHandleString(obj, type, value);
        }

        public void HandleBool(Object obj, FieldBindEnum type, bool value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleBool(obj, type, value);
        }
        
        public void HandleInt(Object obj, FieldBindEnum type, int value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleInt(obj, type, value);
        }
        
        public void HandleFloat(Object obj, FieldBindEnum type, float value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleFloat(obj, type, value);
        }
        
        public void HandleVector2(Object obj, FieldBindEnum type, Vector2 value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleVector2(obj, type, value);
        }
        
        public void HandleVector2(Object obj, FieldBindEnum type, float x, float y)
        {
            HandleVector2(obj, type, new Vector2(x, y));
        }
        
        public void HandleVector3(Object obj, FieldBindEnum type, Vector3 value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleVector3(obj, type, value);
        }
        
        public void HandleVector3(Object obj, FieldBindEnum type, float x, float y, float z)
        {
            HandleVector3(obj, type, new Vector3(x, y, z));
        }
        
        public void HandleLuaTable(Object obj, FieldBindEnum type, LuaTable value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleLuaTable(obj, type, value);
        }
        #endregion
        
        #region 获取FieldBind值
        public string GetString(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetString(obj, type);
        }
        
        public bool GetBool(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetBool(obj, type);
        }
        
        public int GetInt(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetInt(obj, type);
        }
        
        public float GetFloat(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetFloat(obj, type);
        }
        
        public Vector2 GetVector2(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetVector2(obj, type);
        }
        
        public Vector3 GetVector3(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetVector3(obj, type);
        }
        
        public object GetLuaTable(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetLuaTable(obj, type);
        }
        #endregion

        // Domain Reload Support
#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        static void OnEnterPlayMode(UnityEditor.EnterPlayModeOptions options)
        {
            ResetHandler(options);
        }
#endif
    }
}