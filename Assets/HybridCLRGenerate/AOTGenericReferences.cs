using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"Core.Runtime.dll",
		"UniTask.dll",
		"UnityEngine.CoreModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// Core.Runtime.Singleton<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<object>
	// Cysharp.Threading.Tasks.ITaskPoolNode<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Cysharp.Threading.Tasks.AsyncUnit>
	// System.Action<object>
	// System.Func<int>
	// }}

	public void RefMethods()
	{
		// object Core.Runtime.ComponentItemIndex.Get<object>(int)
		// object Core.Runtime.UIManager.Show<object>(bool)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,object>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,object&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<object>(object&)
		// object UnityEngine.GameObject.GetComponent<object>()
	}
}