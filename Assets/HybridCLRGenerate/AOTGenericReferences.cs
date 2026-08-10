using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"System.Core.dll",
		"UnityEngine.CoreModule.dll",
		"XLua.Runtime.dll",
		"YooAsset.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// System.Action<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Action<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Action<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Action<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Action<System.IntPtr,System.Decimal>
	// System.Action<System.IntPtr,System.IntPtr>
	// System.Action<System.IntPtr,byte>
	// System.Action<System.IntPtr,double>
	// System.Action<System.IntPtr,float>
	// System.Action<System.IntPtr,int>
	// System.Action<System.IntPtr,long>
	// System.Action<System.IntPtr,object>
	// System.Action<System.IntPtr,sbyte>
	// System.Action<System.IntPtr,short>
	// System.Action<System.IntPtr,uint>
	// System.Action<System.IntPtr,ulong>
	// System.Action<System.IntPtr,ushort>
	// System.Action<byte>
	// System.Action<int>
	// System.Action<object,int,int>
	// System.Action<object,object>
	// System.Action<object>
	// System.Collections.Generic.ArraySortHelper<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.ArraySortHelper<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.ArraySortHelper<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.Comparer<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.Comparer<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary<int,int>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.ICollection<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.ICollection<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.ICollection<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.IComparer<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.IComparer<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.IComparer<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IEnumerable<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.IEnumerable<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.IEnumerable<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerator<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.IEnumerator<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.IEnumerator<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IList<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.IList<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.IList<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IReadOnlyDictionary<object,object>
	// System.Collections.Generic.KeyValuePair<int,int>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.List.Enumerator<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.List.Enumerator<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.List.Enumerator<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.List<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.List<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.Generic.ObjectComparer<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.Generic.ObjectComparer<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Collections.ObjectModel.ReadOnlyCollection<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Comparison<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Comparison<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Comparison<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Comparison<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Comparison<int>
	// System.Comparison<object>
	// System.Func<System.IntPtr,int,System.Decimal>
	// System.Func<System.IntPtr,int,System.IntPtr>
	// System.Func<System.IntPtr,int,byte>
	// System.Func<System.IntPtr,int,double>
	// System.Func<System.IntPtr,int,float>
	// System.Func<System.IntPtr,int,int>
	// System.Func<System.IntPtr,int,long>
	// System.Func<System.IntPtr,int,object>
	// System.Func<System.IntPtr,int,sbyte>
	// System.Func<System.IntPtr,int,short>
	// System.Func<System.IntPtr,int,uint>
	// System.Func<System.IntPtr,int,ulong>
	// System.Func<System.IntPtr,int,ushort>
	// System.Func<object,byte>
	// System.Func<object,object>
	// System.Func<object>
	// System.Linq.Enumerable.Iterator<object>
	// System.Linq.Enumerable.WhereArrayIterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<object>
	// System.Linq.Enumerable.WhereListIterator<object>
	// System.Predicate<Game.Common.Utility.SerializedDictionary.Item<object,object>>
	// System.Predicate<LxyDemo.UIFramework.UIPanelRuntimeSnapshot>
	// System.Predicate<LxyDemo.UIFramework.UIStackSnapshot>
	// System.Predicate<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Predicate<int>
	// System.Predicate<object>
	// UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene,UnityEngine.SceneManagement.Scene>
	// UnityEngine.Pool.CollectionPool.<>c<object,int>
	// UnityEngine.Pool.CollectionPool<object,int>
	// }}

	public void RefMethods()
	{
		// object System.Activator.CreateInstance<object>()
		// object[] System.Array.Empty<object>()
		// object System.Collections.Generic.CollectionExtensions.GetValueOrDefault<object,object>(System.Collections.Generic.IReadOnlyDictionary<object,object>,object,object)
		// bool System.Enum.TryParse<int>(string,bool,int&)
		// bool System.Enum.TryParse<int>(string,int&)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// object System.Reflection.CustomAttributeExtensions.GetCustomAttribute<object>(System.Reflection.MemberInfo)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.Component.GetComponentInChildren<object>(bool)
		// object[] UnityEngine.Component.GetComponentsInChildren<object>(bool)
		// bool UnityEngine.Component.TryGetComponent<object>(object&)
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object UnityEngine.GameObject.GetComponentInChildren<object>(bool)
		// object[] UnityEngine.GameObject.GetComponentsInChildren<object>(bool)
		// bool UnityEngine.GameObject.TryGetComponent<object>(object&)
		// object UnityEngine.Object.FindObjectOfType<object>()
		// object UnityEngine.Object.FindObjectOfType<object>(bool)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
		// object UnityEngine.Resources.Load<object>(string)
		// System.Void XLua.LuaTable.Get<object,float>(object,float&)
		// System.Void XLua.LuaTable.Get<object,object>(object,object&)
		// float XLua.LuaTable.Get<float>(string)
		// object XLua.LuaTable.Get<object>(string)
		// System.Void XLua.LuaTable.Set<object,byte>(object,byte)
		// System.Void XLua.LuaTable.Set<object,float>(object,float)
		// System.Void XLua.LuaTable.Set<object,int>(object,int)
		// System.Void XLua.LuaTable.Set<object,object>(object,object)
		// System.Void XLua.ObjectTranslator.Get<float>(System.IntPtr,int,float&)
		// System.Void XLua.ObjectTranslator.Get<object>(System.IntPtr,int,object&)
		// System.Void XLua.ObjectTranslator.PushByType<byte>(System.IntPtr,byte)
		// System.Void XLua.ObjectTranslator.PushByType<float>(System.IntPtr,float)
		// System.Void XLua.ObjectTranslator.PushByType<int>(System.IntPtr,int)
		// System.Void XLua.ObjectTranslator.PushByType<object>(System.IntPtr,object)
		// object YooAsset.AssetHandle.GetAssetObject<object>()
		// YooAsset.AssetHandle YooAsset.ResourcePackage.LoadAssetAsync<object>(string,uint)
		// YooAsset.AssetHandle YooAsset.ResourcePackage.LoadAssetSync<object>(string)
	}
}