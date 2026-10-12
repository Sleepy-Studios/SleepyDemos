using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"Core.Runtime.dll",
		"Newtonsoft.Json.dll",
		"SleepyStudios.LoopScroll.dll",
		"System.Core.dll",
		"UniTask.dll",
		"Unity.InputSystem.dll",
		"Unity.RenderPipelines.Core.Runtime.dll",
		"UnityEngine.CoreModule.dll",
		"UnityEngine.JSONSerializeModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// Core.Runtime.ActionConvert<object>
	// Core.Runtime.HandlerBase<object,object>
	// Core.Runtime.LoopScrollItemViewBridge.<>c__DisplayClass21_0<object>
	// Core.Runtime.LoopScrollRegisterExtend.<>c__DisplayClass10_0<object>
	// Core.Runtime.LoopScrollRegisterExtend.<>c__DisplayClass15_0<object>
	// Core.Runtime.LoopScrollRegisterExtend.<>c__DisplayClass5_0<object>
	// Core.Runtime.Singleton<object>
	// Core.Runtime.UIManager.<>c__DisplayClass40_0<object,object>
	// Core.Runtime.UIManager.<>c__DisplayClass41_0<object>
	// Core.Runtime.View.<>c__DisplayClass77_0<object>
	// Core.Runtime.View<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Cfg.LubanConfigService.<>c__DisplayClass6_0.<<LoadFromResourcesAsync>b__0>d,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Cfg.LubanConfigService.<LoadFromResourcesAsync>d__6,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Cfg.LubanConfigService.<LoadTablesAsync>d__7,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.AppDelegate.HotfixEntry.<Awake>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.AppDelegate.LubanConfigSystem.<RunAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.CommonTipsView.<CloseEmptyAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.Dlss.DlssDemoController.<InitializeAsync>d__20>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.Dlss.DlssDemoController.<RestoreControlsAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightHandler.<ChangeOverlayAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ClearFlightViewsAsync>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseOwnedViewsAsync>d__36>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseViewAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CompleteVehicleSelectAsync>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowFlightViewsAsync>d__34,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowVehicleSelectAsync>d__32,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<StartSelectedAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.DroneFlightVehicleSelectView.<FollowMenuFocusAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.GraphicsSettingsHandler.<ApplyModeAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.HubLanguageFlow.<ApplyAsync>d__4,Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.HubLanguageFlow.<RebuildAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.HubSettingsView.<SubmitAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartMenuPaperMotion.<FoldAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<CloseHintsViewAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<CloseOptionsAsync>d__49>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<InitializeHintsAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<ShowHintsViewAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<StartRaceAsync>d__149>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.MainMenuHandler.<EnterAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.MainMenuView.<OpenEffectsAsync>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.MainMenuView.<OpenSettingsAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.MainMenuView.<UpdateMenuInputAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.PresentationCinematicPanel.<ExitLetterboxAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.PresentationCinematicPanel.<PlayAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.PresentationLabFlow.<CloseAsync>d__4,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.PresentationLabFlow.<OpenAsync>d__3,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.PresentationLabView.<PlayAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.PresentationLabView.<StopAndRestoreAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.PresentationTimePanel.<PlayAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneLoadingPresenter.<BeginAsync>d__1,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneLoadingPresenter.<CompleteAsync>d__3,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneLoadingPresenter.<RestoreAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24,Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23,Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneNavigator.<WaitUntilStableAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8,Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SceneManagement.GameSceneRuntime.<ReturnToHubAsync>d__9,Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SettingsFlow.<ApplyAsync>d__3,Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SettingsFlow.<OpenAsync>d__2,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SettingsHandler.<ApplyAsync>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SimpleTipsTrigger.<ObserveShowAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SingleUIManager.<CloseSimpleAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SingleUIManager.<ObserveAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<Hotfix.SingleUIManager.<ShowTipsMessageBarAsync>d__3,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Cfg.LubanConfigService.<>c__DisplayClass6_0.<<LoadFromResourcesAsync>b__0>d,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Cfg.LubanConfigService.<LoadFromResourcesAsync>d__6,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Cfg.LubanConfigService.<LoadTablesAsync>d__7,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.AppDelegate.HotfixEntry.<Awake>d__0>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.AppDelegate.LubanConfigSystem.<RunAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.CommonTipsView.<CloseEmptyAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.Dlss.DlssDemoController.<InitializeAsync>d__20>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.Dlss.DlssDemoController.<RestoreControlsAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightHandler.<ChangeOverlayAsync>d__30>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ClearFlightViewsAsync>d__35>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseOwnedViewsAsync>d__36>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseViewAsync>d__38>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CompleteVehicleSelectAsync>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowFlightViewsAsync>d__34,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowVehicleSelectAsync>d__32,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<StartSelectedAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.DroneFlightVehicleSelectView.<FollowMenuFocusAsync>d__15>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.GraphicsSettingsHandler.<ApplyModeAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.HubLanguageFlow.<ApplyAsync>d__4,Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.HubLanguageFlow.<RebuildAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.HubSettingsView.<SubmitAsync>d__24>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartMenuPaperMotion.<FoldAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<CloseHintsViewAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<CloseOptionsAsync>d__49>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<InitializeHintsAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<ShowHintsViewAsync>d__7>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<StartRaceAsync>d__149>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.MainMenuHandler.<EnterAsync>d__2>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.MainMenuView.<OpenEffectsAsync>d__33>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.MainMenuView.<OpenSettingsAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.MainMenuView.<UpdateMenuInputAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.PresentationCinematicPanel.<ExitLetterboxAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.PresentationCinematicPanel.<PlayAsync>d__22>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.PresentationLabFlow.<CloseAsync>d__4,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.PresentationLabFlow.<OpenAsync>d__3,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.PresentationLabView.<PlayAsync>d__28>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.PresentationLabView.<StopAndRestoreAsync>d__29>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.PresentationTimePanel.<PlayAsync>d__8>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneLoadingPresenter.<BeginAsync>d__1,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneLoadingPresenter.<CompleteAsync>d__3,object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneLoadingPresenter.<RestoreAsync>d__4>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24,Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23,Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneNavigator.<WaitUntilStableAsync>d__21>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8,Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SceneManagement.GameSceneRuntime.<ReturnToHubAsync>d__9,Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SettingsFlow.<ApplyAsync>d__3,Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4,byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SettingsFlow.<OpenAsync>d__2,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SettingsHandler.<ApplyAsync>d__1>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SimpleTipsTrigger.<ObserveShowAsync>d__23>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SingleUIManager.<CloseSimpleAsync>d__11>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SingleUIManager.<ObserveAsync>d__12>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<Hotfix.SingleUIManager.<ShowTipsMessageBarAsync>d__3,Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Cfg.LubanConfigService.<InitializeCoreAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<BeginSelectedRaceAsync>d__160>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<ChangeRoundAsync>d__162>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<CloseDestroyedHintsViewAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<CloseOptionsCommandAsync>d__50>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<OpenOptionsAsync>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<ResumeFromOptionsAsync>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.KagitKart.KagitKartWorld.<SynchronizeModalAsync>d__167>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid.<>c<Hotfix.WallSqueeze.WallSqueezeWorld.<SynchronizeModalAsync>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Cfg.LubanConfigService.<InitializeCoreAsync>d__5>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<BeginSelectedRaceAsync>d__160>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<ChangeRoundAsync>d__162>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<CloseDestroyedHintsViewAsync>d__16>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<CloseOptionsCommandAsync>d__50>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<OpenOptionsAsync>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<ResumeFromOptionsAsync>d__52>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.KagitKart.KagitKartWorld.<SynchronizeModalAsync>d__167>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49>
	// Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoid<Hotfix.WallSqueeze.WallSqueezeWorld.<SynchronizeModalAsync>d__48>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<byte>
	// Cysharp.Threading.Tasks.CompilerServices.IStateMachineRunnerPromise<object>
	// Cysharp.Threading.Tasks.ITaskPoolNode<object>
	// Cysharp.Threading.Tasks.IUniTaskSource<Core.Runtime.ResourceSceneLoadResult>
	// Cysharp.Threading.Tasks.IUniTaskSource<Core.Runtime.ResourceSceneUnloadResult>
	// Cysharp.Threading.Tasks.IUniTaskSource<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.IUniTaskSource<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.IUniTaskSource<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.IUniTaskSource<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.IUniTaskSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.IUniTaskSource<byte>
	// Cysharp.Threading.Tasks.IUniTaskSource<object>
	// Cysharp.Threading.Tasks.Internal.StatePool<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>>
	// Cysharp.Threading.Tasks.Internal.StatePool<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>
	// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>>
	// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneLoadResult>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneUnloadResult>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<byte>
	// Cysharp.Threading.Tasks.UniTask.Awaiter<object>
	// Cysharp.Threading.Tasks.UniTask.CanceledResultSource<byte>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<Core.Runtime.ResourceSceneLoadResult>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<Core.Runtime.ResourceSceneUnloadResult>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<byte>
	// Cysharp.Threading.Tasks.UniTask.IsCanceledSource<object>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<Core.Runtime.ResourceSceneLoadResult>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<Core.Runtime.ResourceSceneUnloadResult>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<byte>
	// Cysharp.Threading.Tasks.UniTask.MemoizeSource<object>
	// Cysharp.Threading.Tasks.UniTask<Core.Runtime.ResourceSceneLoadResult>
	// Cysharp.Threading.Tasks.UniTask<Core.Runtime.ResourceSceneUnloadResult>
	// Cysharp.Threading.Tasks.UniTask<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.UniTask<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.UniTask<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,byte>>
	// Cysharp.Threading.Tasks.UniTask<System.ValueTuple<byte,object>>
	// Cysharp.Threading.Tasks.UniTask<byte>
	// Cysharp.Threading.Tasks.UniTask<object>
	// Cysharp.Threading.Tasks.UniTaskCompletionSource<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.UniTaskCompletionSource<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.UniTaskCompletionSource<byte>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Core.Runtime.Settings.SettingsApplyResult>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Cysharp.Threading.Tasks.AsyncUnit>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Hotfix.SceneManagement.GameSceneSwitchResult>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<byte>
	// Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<object>
	// Cysharp.Threading.Tasks.UniTaskExtensions.<>c__51<Core.Runtime.UIOperationResult>
	// Cysharp.Threading.Tasks.UniTaskExtensions.<>c__51<byte>
	// Cysharp.Threading.Tasks.UniTaskExtensions.AttachExternalCancellationSource<byte>
	// SleepyStudios.LoopScroll.LoopScrollView.<>c__DisplayClass148_0<object>
	// System.Action<Hotfix.KagitKart.KagitKartItemEvent>
	// System.Action<Hotfix.KagitKart.KagitKartRaceEvent>
	// System.Action<SleepyStudios.LoopScroll.ScrollResult>
	// System.Action<System.ValueTuple<object,object>>
	// System.Action<UnityEngine.Color>
	// System.Action<UnityEngine.EventSystems.RaycastResult>
	// System.Action<UnityEngine.Rect>
	// System.Action<UnityEngine.Vector2Int>
	// System.Action<UnityEngine.Vector3,float>
	// System.Action<byte,object>
	// System.Action<byte>
	// System.Action<double>
	// System.Action<float>
	// System.Action<int,int,double>
	// System.Action<int>
	// System.Action<object,SleepyStudios.LoopScroll.CellBindContext>
	// System.Action<object,byte>
	// System.Action<object,int,SleepyStudios.LoopScroll.CellBindContext>
	// System.Action<object,int>
	// System.Action<object,object>
	// System.Action<object>
	// System.ArraySegment.Enumerator<object>
	// System.ArraySegment.Enumerator<ushort>
	// System.ArraySegment<object>
	// System.ArraySegment<ushort>
	// System.ByReference<object>
	// System.ByReference<ushort>
	// System.Collections.Concurrent.ConcurrentQueue.<Enumerate>d__28<object>
	// System.Collections.Concurrent.ConcurrentQueue.Segment<object>
	// System.Collections.Concurrent.ConcurrentQueue<object>
	// System.Collections.Generic.ArraySortHelper<System.ValueTuple<object,object>>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Color>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.RaycastHit>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Rect>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector2Int>
	// System.Collections.Generic.ArraySortHelper<double>
	// System.Collections.Generic.ArraySortHelper<float>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<Core.Runtime.ResourceSceneLoadResult>
	// System.Collections.Generic.Comparer<Core.Runtime.ResourceSceneUnloadResult>
	// System.Collections.Generic.Comparer<Core.Runtime.Settings.SettingsApplyResult>
	// System.Collections.Generic.Comparer<Core.Runtime.UIOperationResult>
	// System.Collections.Generic.Comparer<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Collections.Generic.Comparer<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.Comparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.Comparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.Comparer<UnityEngine.Color>
	// System.Collections.Generic.Comparer<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.Comparer<UnityEngine.RaycastHit>
	// System.Collections.Generic.Comparer<UnityEngine.Rect>
	// System.Collections.Generic.Comparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.Comparer<byte>
	// System.Collections.Generic.Comparer<double>
	// System.Collections.Generic.Comparer<float>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<UnityEngine.Vector2Int,object>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.Enumerator<object,double>
	// System.Collections.Generic.Dictionary.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<UnityEngine.Vector2Int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,double>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<UnityEngine.Vector2Int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.KeyCollection<object,double>
	// System.Collections.Generic.Dictionary.KeyCollection<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<UnityEngine.Vector2Int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,double>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<UnityEngine.Vector2Int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary.ValueCollection<object,double>
	// System.Collections.Generic.Dictionary.ValueCollection<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary<UnityEngine.Vector2Int,object>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.Dictionary<object,double>
	// System.Collections.Generic.Dictionary<object,int>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.EqualityComparer<Core.Runtime.ResourceSceneLoadResult>
	// System.Collections.Generic.EqualityComparer<Core.Runtime.ResourceSceneUnloadResult>
	// System.Collections.Generic.EqualityComparer<Core.Runtime.Settings.SettingsApplyResult>
	// System.Collections.Generic.EqualityComparer<Core.Runtime.UIOperationResult>
	// System.Collections.Generic.EqualityComparer<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Collections.Generic.EqualityComparer<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.EqualityComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.EqualityComparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.EqualityComparer<byte>
	// System.Collections.Generic.EqualityComparer<double>
	// System.Collections.Generic.EqualityComparer<float>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.HashSet.Enumerator<int>
	// System.Collections.Generic.HashSet.Enumerator<object>
	// System.Collections.Generic.HashSet<int>
	// System.Collections.Generic.HashSet<object>
	// System.Collections.Generic.HashSetEqualityComparer<int>
	// System.Collections.Generic.HashSetEqualityComparer<object>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<UnityEngine.Vector2Int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,double>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<System.ValueTuple<object,object>>
	// System.Collections.Generic.ICollection<UnityEngine.Color>
	// System.Collections.Generic.ICollection<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.ICollection<UnityEngine.Rect>
	// System.Collections.Generic.ICollection<UnityEngine.Vector2Int>
	// System.Collections.Generic.ICollection<UnityEngine.Vector3>
	// System.Collections.Generic.ICollection<double>
	// System.Collections.Generic.ICollection<float>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.IComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.IComparer<UnityEngine.Color>
	// System.Collections.Generic.IComparer<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.IComparer<UnityEngine.RaycastHit>
	// System.Collections.Generic.IComparer<UnityEngine.Rect>
	// System.Collections.Generic.IComparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.IComparer<double>
	// System.Collections.Generic.IComparer<float>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<UnityEngine.Vector2Int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,double>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerable<UnityEngine.Color>
	// System.Collections.Generic.IEnumerable<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.IEnumerable<UnityEngine.Rect>
	// System.Collections.Generic.IEnumerable<UnityEngine.Vector2Int>
	// System.Collections.Generic.IEnumerable<UnityEngine.Vector3>
	// System.Collections.Generic.IEnumerable<double>
	// System.Collections.Generic.IEnumerable<float>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<UnityEngine.Vector2Int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,double>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.IEnumerator<UnityEngine.Color>
	// System.Collections.Generic.IEnumerator<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.IEnumerator<UnityEngine.Rect>
	// System.Collections.Generic.IEnumerator<UnityEngine.Vector2Int>
	// System.Collections.Generic.IEnumerator<double>
	// System.Collections.Generic.IEnumerator<float>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEqualityComparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IList<System.ValueTuple<object,object>>
	// System.Collections.Generic.IList<UnityEngine.Color>
	// System.Collections.Generic.IList<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.IList<UnityEngine.Rect>
	// System.Collections.Generic.IList<UnityEngine.Vector2Int>
	// System.Collections.Generic.IList<UnityEngine.Vector3>
	// System.Collections.Generic.IList<double>
	// System.Collections.Generic.IList<float>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IReadOnlyCollection<Core.Runtime.Inputs.InputBindingDisplay.Part>
	// System.Collections.Generic.IReadOnlyCollection<UnityEngine.Rect>
	// System.Collections.Generic.IReadOnlyCollection<double>
	// System.Collections.Generic.IReadOnlyCollection<object>
	// System.Collections.Generic.IReadOnlyList<UnityEngine.Rect>
	// System.Collections.Generic.IReadOnlyList<double>
	// System.Collections.Generic.IReadOnlyList<object>
	// System.Collections.Generic.KeyValuePair<UnityEngine.Vector2Int,object>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<object,System.ValueTuple<object,object>>
	// System.Collections.Generic.KeyValuePair<object,double>
	// System.Collections.Generic.KeyValuePair<object,int>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.List.Enumerator<System.ValueTuple<object,object>>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Color>
	// System.Collections.Generic.List.Enumerator<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Rect>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector2Int>
	// System.Collections.Generic.List.Enumerator<double>
	// System.Collections.Generic.List.Enumerator<float>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List<System.ValueTuple<object,object>>
	// System.Collections.Generic.List<UnityEngine.Color>
	// System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.List<UnityEngine.Rect>
	// System.Collections.Generic.List<UnityEngine.Vector2Int>
	// System.Collections.Generic.List<double>
	// System.Collections.Generic.List<float>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<Core.Runtime.ResourceSceneLoadResult>
	// System.Collections.Generic.ObjectComparer<Core.Runtime.ResourceSceneUnloadResult>
	// System.Collections.Generic.ObjectComparer<Core.Runtime.Settings.SettingsApplyResult>
	// System.Collections.Generic.ObjectComparer<Core.Runtime.UIOperationResult>
	// System.Collections.Generic.ObjectComparer<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Collections.Generic.ObjectComparer<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Color>
	// System.Collections.Generic.ObjectComparer<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.Generic.ObjectComparer<UnityEngine.RaycastHit>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Rect>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.ObjectComparer<byte>
	// System.Collections.Generic.ObjectComparer<double>
	// System.Collections.Generic.ObjectComparer<float>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<Core.Runtime.ResourceSceneLoadResult>
	// System.Collections.Generic.ObjectEqualityComparer<Core.Runtime.ResourceSceneUnloadResult>
	// System.Collections.Generic.ObjectEqualityComparer<Core.Runtime.Settings.SettingsApplyResult>
	// System.Collections.Generic.ObjectEqualityComparer<Core.Runtime.UIOperationResult>
	// System.Collections.Generic.ObjectEqualityComparer<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Collections.Generic.ObjectEqualityComparer<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,byte>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<byte,object>>
	// System.Collections.Generic.ObjectEqualityComparer<System.ValueTuple<object,object>>
	// System.Collections.Generic.ObjectEqualityComparer<UnityEngine.Vector2Int>
	// System.Collections.Generic.ObjectEqualityComparer<byte>
	// System.Collections.Generic.ObjectEqualityComparer<double>
	// System.Collections.Generic.ObjectEqualityComparer<float>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.Queue.Enumerator<Hotfix.KagitKart.KagitKartItemEvent>
	// System.Collections.Generic.Queue.Enumerator<int>
	// System.Collections.Generic.Queue.Enumerator<object>
	// System.Collections.Generic.Queue<Hotfix.KagitKart.KagitKartItemEvent>
	// System.Collections.Generic.Queue<int>
	// System.Collections.Generic.Queue<object>
	// System.Collections.Generic.Stack.Enumerator<object>
	// System.Collections.Generic.Stack<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.ValueTuple<object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Color>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.EventSystems.RaycastResult>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Rect>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector2Int>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector3>
	// System.Collections.ObjectModel.ReadOnlyCollection<double>
	// System.Collections.ObjectModel.ReadOnlyCollection<float>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Comparison<System.ValueTuple<object,object>>
	// System.Comparison<UnityEngine.Color>
	// System.Comparison<UnityEngine.EventSystems.RaycastResult>
	// System.Comparison<UnityEngine.RaycastHit>
	// System.Comparison<UnityEngine.Rect>
	// System.Comparison<UnityEngine.Vector2Int>
	// System.Comparison<double>
	// System.Comparison<float>
	// System.Comparison<int>
	// System.Comparison<object>
	// System.Converter<object,UnityEngine.Color>
	// System.Func<Core.Runtime.ResourceSceneLoadResult>
	// System.Func<Core.Runtime.ResourceSceneUnloadResult>
	// System.Func<Core.Runtime.Settings.SettingsApplyResult>
	// System.Func<Core.Runtime.UIOperationResult>
	// System.Func<Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult>>
	// System.Func<Hotfix.KagitKart.KagitKartControls>
	// System.Func<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Func<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Func<System.Threading.CancellationToken,Cysharp.Threading.Tasks.UniTask<byte>>
	// System.Func<System.Threading.CancellationToken,Cysharp.Threading.Tasks.UniTask>
	// System.Func<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Func<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Func<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Func<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Func<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Func<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<System.ValueTuple<byte,byte>>
	// System.Func<System.ValueTuple<byte,object>>
	// System.Func<UnityEngine.Vector2Int,byte>
	// System.Func<UnityEngine.Vector2Int,object>
	// System.Func<byte>
	// System.Func<int,byte>
	// System.Func<int,object>
	// System.Func<int>
	// System.Func<object,Core.Runtime.ResourceSceneLoadResult>
	// System.Func<object,Core.Runtime.ResourceSceneUnloadResult>
	// System.Func<object,Core.Runtime.Settings.SettingsApplyResult>
	// System.Func<object,Core.Runtime.UIOperationResult>
	// System.Func<object,Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Func<object,Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Func<object,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Func<object,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Func<object,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Func<object,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Func<object,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Func<object,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Func<object,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Func<object,System.ValueTuple<byte,byte>>
	// System.Func<object,System.ValueTuple<byte,object>>
	// System.Func<object,byte>
	// System.Func<object,int>
	// System.Func<object,object,Cysharp.Threading.Tasks.UniTask<object>>
	// System.Func<object,object,object>
	// System.Func<object,object>
	// System.Func<object>
	// System.Lazy<object>
	// System.Linq.Buffer<object>
	// System.Linq.EnumerableSorter<object,int>
	// System.Linq.EnumerableSorter<object>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<object>
	// System.Linq.OrderedEnumerable<object,int>
	// System.Linq.OrderedEnumerable<object>
	// System.Nullable<SleepyStudios.LoopScroll.ScrollResult>
	// System.Nullable<UnityEngine.Color>
	// System.Nullable<UnityEngine.Vector2>
	// System.Nullable<UnityEngine.Vector3>
	// System.Nullable<byte>
	// System.Nullable<double>
	// System.Nullable<float>
	// System.Nullable<int>
	// System.Nullable<uint>
	// System.Predicate<System.ValueTuple<object,object>>
	// System.Predicate<UnityEngine.Color>
	// System.Predicate<UnityEngine.EventSystems.RaycastResult>
	// System.Predicate<UnityEngine.InputSystem.InputBinding>
	// System.Predicate<UnityEngine.Rect>
	// System.Predicate<UnityEngine.Vector2Int>
	// System.Predicate<double>
	// System.Predicate<float>
	// System.Predicate<int>
	// System.Predicate<object>
	// System.ReadOnlySpan.Enumerator<object>
	// System.ReadOnlySpan.Enumerator<ushort>
	// System.ReadOnlySpan<object>
	// System.ReadOnlySpan<ushort>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<Core.Runtime.ResourceSceneLoadResult>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<Core.Runtime.ResourceSceneUnloadResult>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<Core.Runtime.UIOperationResult>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<byte>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<Core.Runtime.ResourceSceneLoadResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<Core.Runtime.ResourceSceneUnloadResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<Core.Runtime.Settings.SettingsApplyResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<Core.Runtime.UIOperationResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<Core.Runtime.ResourceSceneLoadResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<Core.Runtime.ResourceSceneUnloadResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<Core.Runtime.Settings.SettingsApplyResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<Core.Runtime.UIOperationResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<byte>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<Core.Runtime.ResourceSceneLoadResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<Core.Runtime.ResourceSceneUnloadResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<Core.Runtime.Settings.SettingsApplyResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<Core.Runtime.UIOperationResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter<object>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<Core.Runtime.ResourceSceneLoadResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<Core.Runtime.ResourceSceneUnloadResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<Core.Runtime.Settings.SettingsApplyResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<Core.Runtime.UIOperationResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<byte>
	// System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable<object>
	// System.Runtime.CompilerServices.TaskAwaiter<Core.Runtime.ResourceSceneLoadResult>
	// System.Runtime.CompilerServices.TaskAwaiter<Core.Runtime.ResourceSceneUnloadResult>
	// System.Runtime.CompilerServices.TaskAwaiter<Core.Runtime.Settings.SettingsApplyResult>
	// System.Runtime.CompilerServices.TaskAwaiter<Core.Runtime.UIOperationResult>
	// System.Runtime.CompilerServices.TaskAwaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Runtime.CompilerServices.TaskAwaiter<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.TaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.TaskAwaiter<byte>
	// System.Runtime.CompilerServices.TaskAwaiter<object>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<Core.Runtime.ResourceSceneLoadResult>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<Core.Runtime.ResourceSceneUnloadResult>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<Core.Runtime.Settings.SettingsApplyResult>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<Core.Runtime.UIOperationResult>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,byte>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<System.ValueTuple<byte,object>>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<byte>
	// System.Runtime.CompilerServices.ValueTaskAwaiter<object>
	// System.Span<object>
	// System.Span<ushort>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<byte>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<object>
	// System.Threading.Tasks.Sources.IValueTaskSource<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.Sources.IValueTaskSource<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.Sources.IValueTaskSource<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.Sources.IValueTaskSource<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.Sources.IValueTaskSource<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.Sources.IValueTaskSource<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Sources.IValueTaskSource<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Sources.IValueTaskSource<byte>
	// System.Threading.Tasks.Sources.IValueTaskSource<object>
	// System.Threading.Tasks.Task<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.Task<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.Task<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.Task<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.Task<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.Task<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.Task<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.Task<byte>
	// System.Threading.Tasks.Task<object>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<byte>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<object>
	// System.Threading.Tasks.TaskFactory<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.TaskFactory<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.TaskFactory<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.TaskFactory<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.TaskFactory<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.TaskFactory<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.TaskFactory<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.TaskFactory<byte>
	// System.Threading.Tasks.TaskFactory<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<byte>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask.<>c<object>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<byte>
	// System.Threading.Tasks.ValueTask.ValueTaskSourceAsTask<object>
	// System.Threading.Tasks.ValueTask<Core.Runtime.ResourceSceneLoadResult>
	// System.Threading.Tasks.ValueTask<Core.Runtime.ResourceSceneUnloadResult>
	// System.Threading.Tasks.ValueTask<Core.Runtime.Settings.SettingsApplyResult>
	// System.Threading.Tasks.ValueTask<Core.Runtime.UIOperationResult>
	// System.Threading.Tasks.ValueTask<Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.Threading.Tasks.ValueTask<Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,byte>>
	// System.Threading.Tasks.ValueTask<System.ValueTuple<byte,object>>
	// System.Threading.Tasks.ValueTask<byte>
	// System.Threading.Tasks.ValueTask<object>
	// System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>
	// System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>
	// System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>
	// System.ValueTuple<byte,Core.Runtime.UIOperationResult>
	// System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>
	// System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>
	// System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>
	// System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>
	// System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>
	// System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>
	// System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>
	// System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneLoadResult>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.ResourceSceneUnloadResult>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.Settings.SettingsApplyResult>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Core.Runtime.UIOperationResult>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneRuntimeResult>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,Hotfix.SceneManagement.GameSceneSwitchResult>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,byte>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,System.ValueTuple<byte,object>>>
	// System.ValueTuple<byte,System.ValueTuple<byte,byte>>
	// System.ValueTuple<byte,System.ValueTuple<byte,object>>
	// System.ValueTuple<byte,byte>
	// System.ValueTuple<byte,object>
	// System.ValueTuple<double,double>
	// System.ValueTuple<int,int,double>
	// System.ValueTuple<int,object>
	// System.ValueTuple<object,object,object,object,object>
	// System.ValueTuple<object,object>
	// UnityEngine.Events.InvokableCall<float>
	// UnityEngine.Events.UnityAction<float>
	// UnityEngine.Events.UnityEvent<float>
	// UnityEngine.InputSystem.InputBindingComposite<UnityEngine.Vector2>
	// UnityEngine.InputSystem.InputBindingComposite<float>
	// UnityEngine.InputSystem.InputControl<UnityEngine.Vector2>
	// UnityEngine.InputSystem.InputControl<float>
	// UnityEngine.InputSystem.InputProcessor<UnityEngine.Vector2>
	// UnityEngine.InputSystem.InputProcessor<float>
	// UnityEngine.InputSystem.Utilities.InlinedArray<object>
	// UnityEngine.InputSystem.Utilities.ReadOnlyArray.Enumerator<UnityEngine.InputSystem.InputBinding>
	// UnityEngine.InputSystem.Utilities.ReadOnlyArray<UnityEngine.InputSystem.InputBinding>
	// UnityEngine.Rendering.VolumeParameter<float>
	// }}

	public void RefMethods()
	{
		// object Core.Runtime.ComponentItemIndex.Get<object>(int)
		// object Core.Runtime.GlobalData.Add<object>()
		// object Core.Runtime.GlobalData.Add<object>(object)
		// object Core.Runtime.GlobalData.Get<object>()
		// object Core.Runtime.GlobalData.RegisterState<object>(System.Type,object)
		// bool Core.Runtime.GlobalData.Remove<object>()
		// System.Void Core.Runtime.GlobalData.Subscribe<object>(System.Action<object>,bool)
		// Cysharp.Threading.Tasks.UniTask<object> Core.Runtime.IResourceLoader.LoadAssetAsync<object>(string)
		// UnityEngine.Vector2 Core.Runtime.Inputs.InputActionSession.Read<UnityEngine.Vector2>(string)
		// float Core.Runtime.Inputs.InputActionSession.Read<float>(string)
		// object Core.Runtime.LocalDataManager.LoadData<object>(string,object,System.Action<object>)
		// object Core.Runtime.LocalDataManager.LoadData<object>(string,object,string&,System.Action<object>)
		// System.Void Core.Runtime.LocalDataManager.SaveData<object>(string,object,System.Action<object>)
		// System.Void Core.Runtime.LoopScrollItemViewBridge.<Configure>b__20_0<object>(SleepyStudios.LoopScroll.LoopCell,int,SleepyStudios.LoopScroll.CellBindContext)
		// System.Void Core.Runtime.LoopScrollItemViewBridge.Configure<object>()
		// object Core.Runtime.LoopScrollItemViewBridge.GetOrCreate<object>(SleepyStudios.LoopScroll.LoopCell,SleepyStudios.LoopScroll.CellBindContext)
		// object Core.Runtime.LoopScrollItemViewBridge.GetSimpleCallback<object>(System.Delegate,System.Func<object>)
		// System.Void Core.Runtime.LoopScrollRegisterExtend.RegisterLoopScrollClick<object>(Core.Runtime.View,SleepyStudios.LoopScroll.LoopScrollView,System.Action<object,int>)
		// System.Void Core.Runtime.LoopScrollRegisterExtend.RegisterLoopScrollItemHide<object>(Core.Runtime.View,SleepyStudios.LoopScroll.LoopScrollView,System.Action<object>)
		// System.Void Core.Runtime.LoopScrollRegisterExtend.RegisterLoopScrollRect<object>(Core.Runtime.View,SleepyStudios.LoopScroll.LoopScrollView,System.Action<object,int>)
		// System.IDisposable Core.Runtime.LoopScrollRegisterExtend.SubscribeSimpleClick<object>(SleepyStudios.LoopScroll.LoopScrollView,System.Action<object,int>)
		// System.IDisposable Core.Runtime.LoopScrollRegisterExtend.SubscribeSimpleItemHide<object>(SleepyStudios.LoopScroll.LoopScrollView,System.Action<object>)
		// System.IDisposable Core.Runtime.LoopScrollRegisterExtend.SubscribeSimpleRect<object>(SleepyStudios.LoopScroll.LoopScrollView,System.Action<object,int>)
		// Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult> Core.Runtime.UIManager.CloseAsync<object>(bool,System.Threading.CancellationToken)
		// object Core.Runtime.UIManager.Get<object>()
		// Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult> Core.Runtime.UIManager.ReplaceAsync<object>(Core.Runtime.UIShowOptions,System.Threading.CancellationToken)
		// Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult> Core.Runtime.UIManager.ShowAsync<object,object>(object,Core.Runtime.UIShowOptions,System.Threading.CancellationToken)
		// Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult> Core.Runtime.UIManager.ShowAsync<object>(Core.Runtime.UIShowOptions,System.Threading.CancellationToken)
		// Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult> Core.Runtime.UIManager.ShowAsync<object>(System.Action<object>,Core.Runtime.UIShowOptions,System.Threading.CancellationToken)
		// System.Void Core.Runtime.View.BindData<object>(System.Action<object>)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.AppDelegate.LubanConfigSystem.<RunAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.AppDelegate.LubanConfigSystem.<RunAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.CommonTipsView.<CloseEmptyAsync>d__7>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.CommonTipsView.<CloseEmptyAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ClearFlightViewsAsync>d__35>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ClearFlightViewsAsync>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseOwnedViewsAsync>d__36>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseOwnedViewsAsync>d__36&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CompleteVehicleSelectAsync>d__33>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CompleteVehicleSelectAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<StartSelectedAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<StartSelectedAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartMenuPaperMotion.<FoldAsync>d__21>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartMenuPaperMotion.<FoldAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<InitializeHintsAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<InitializeHintsAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<StartRaceAsync>d__149>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<StartRaceAsync>d__149&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.PresentationCinematicPanel.<ExitLetterboxAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.PresentationCinematicPanel.<ExitLetterboxAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.PresentationCinematicPanel.<PlayAsync>d__22>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.PresentationCinematicPanel.<PlayAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.PresentationLabView.<PlayAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.PresentationLabView.<PlayAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.PresentationLabView.<StopAndRestoreAsync>d__29>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.PresentationLabView.<StopAndRestoreAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.PresentationTimePanel.<PlayAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.PresentationTimePanel.<PlayAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.SceneManagement.GameSceneNavigator.<WaitUntilStableAsync>d__21>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.SceneManagement.GameSceneNavigator.<WaitUntilStableAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.SimpleTipsTrigger.<ObserveShowAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.SimpleTipsTrigger.<ObserveShowAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.SingleUIManager.<CloseSimpleAsync>d__11>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.SingleUIManager.<CloseSimpleAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>,Hotfix.GraphicsSettingsHandler.<ApplyModeAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>&,Hotfix.GraphicsSettingsHandler.<ApplyModeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>,Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>&,Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>,Hotfix.HubSettingsView.<SubmitAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>&,Hotfix.HubSettingsView.<SubmitAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>,Hotfix.SettingsHandler.<ApplyAsync>d__1>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>&,Hotfix.SettingsHandler.<ApplyAsync>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.Dlss.DlssDemoController.<InitializeAsync>d__20>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.Dlss.DlssDemoController.<InitializeAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.Dlss.DlssDemoController.<RestoreControlsAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.Dlss.DlssDemoController.<RestoreControlsAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.DroneFlight.DroneFlightHandler.<ChangeOverlayAsync>d__30>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.DroneFlight.DroneFlightHandler.<ChangeOverlayAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseViewAsync>d__38>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseViewAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.HubLanguageFlow.<RebuildAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.HubLanguageFlow.<RebuildAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<CloseHintsViewAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<CloseHintsViewAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<CloseOptionsAsync>d__49>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<CloseOptionsAsync>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<ShowHintsViewAsync>d__7>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<ShowHintsViewAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.MainMenuView.<OpenEffectsAsync>d__33>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.MainMenuView.<OpenEffectsAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.MainMenuView.<OpenSettingsAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.MainMenuView.<OpenSettingsAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SceneManagement.GameSceneLoadingPresenter.<RestoreAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SceneManagement.GameSceneLoadingPresenter.<RestoreAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SingleUIManager.<ObserveAsync>d__12>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SingleUIManager.<ObserveAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>&,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>,Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>&,Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>,Hotfix.MainMenuHandler.<EnterAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>&,Hotfix.MainMenuHandler.<EnterAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.AppDelegate.HotfixEntry.<Awake>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.DroneFlightVehicleSelectView.<FollowMenuFocusAsync>d__15>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.DroneFlightVehicleSelectView.<FollowMenuFocusAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.MainMenuView.<UpdateMenuInputAsync>d__16>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.MainMenuView.<UpdateMenuInputAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31>(System.Runtime.CompilerServices.TaskAwaiter&,Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.HubLanguageFlow.<ApplyAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.HubLanguageFlow.<ApplyAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>,Hotfix.HubLanguageFlow.<ApplyAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>&,Hotfix.HubLanguageFlow.<ApplyAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>,Hotfix.SettingsFlow.<ApplyAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.Settings.SettingsApplyResult>&,Hotfix.SettingsFlow.<ApplyAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.HubLanguageFlow.<ApplyAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.HubLanguageFlow.<ApplyAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.PresentationLabFlow.<CloseAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.PresentationLabFlow.<CloseAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.SettingsFlow.<OpenAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.SettingsFlow.<OpenAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.PresentationLabFlow.<CloseAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.PresentationLabFlow.<CloseAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.PresentationLabFlow.<OpenAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.PresentationLabFlow.<OpenAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SettingsFlow.<OpenAsync>d__2>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SettingsFlow.<OpenAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SingleUIManager.<ShowTipsMessageBarAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SingleUIManager.<ShowTipsMessageBarAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Hotfix.PresentationLabFlow.<OpenAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Hotfix.PresentationLabFlow.<OpenAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneRuntimeResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneLoadResult>,Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneLoadResult>&,Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneRuntimeResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneUnloadResult>,Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneUnloadResult>&,Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneRuntimeResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneUnloadResult>,Hotfix.SceneManagement.GameSceneRuntime.<ReturnToHubAsync>d__9>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.ResourceSceneUnloadResult>&,Hotfix.SceneManagement.GameSceneRuntime.<ReturnToHubAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>,Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>&,Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>,Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneRuntimeResult>&,Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.SwitchToMainThreadAwaitable.Awaiter,Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4>(Cysharp.Threading.Tasks.SwitchToMainThreadAwaitable.Awaiter&,Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowFlightViewsAsync>d__34>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowFlightViewsAsync>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowVehicleSelectAsync>d__32>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowVehicleSelectAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SceneManagement.GameSceneLoadingPresenter.<BeginAsync>d__1>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SceneManagement.GameSceneLoadingPresenter.<BeginAsync>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.SceneManagement.GameSceneLoadingPresenter.<CompleteAsync>d__3>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.SceneManagement.GameSceneLoadingPresenter.<CompleteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Cfg.LubanConfigService.<>c__DisplayClass6_0.<<LoadFromResourcesAsync>b__0>d>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Cfg.LubanConfigService.<>c__DisplayClass6_0.<<LoadFromResourcesAsync>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Cfg.LubanConfigService.<LoadFromResourcesAsync>d__6>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Cfg.LubanConfigService.<LoadFromResourcesAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Cfg.LubanConfigService.<LoadTablesAsync>d__7>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Cfg.LubanConfigService.<LoadTablesAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4>(Hotfix.AppDelegate.HotfixBootService.<RunBootSystems>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.AppDelegate.HotfixEntry.<Awake>d__0>(Hotfix.AppDelegate.HotfixEntry.<Awake>d__0&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.AppDelegate.LubanConfigSystem.<RunAsync>d__4>(Hotfix.AppDelegate.LubanConfigSystem.<RunAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.CommonTipsView.<CloseEmptyAsync>d__7>(Hotfix.CommonTipsView.<CloseEmptyAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26>(Hotfix.Dlss.DlssDemoController.<DisposeControlsAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30>(Hotfix.Dlss.DlssDemoController.<ExitAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.Dlss.DlssDemoController.<InitializeAsync>d__20>(Hotfix.Dlss.DlssDemoController.<InitializeAsync>d__20&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.Dlss.DlssDemoController.<RestoreControlsAsync>d__24>(Hotfix.Dlss.DlssDemoController.<RestoreControlsAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightHandler.<ChangeOverlayAsync>d__30>(Hotfix.DroneFlight.DroneFlightHandler.<ChangeOverlayAsync>d__30&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31>(Hotfix.DroneFlight.DroneFlightHandler.<CloseOverlaysAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ClearFlightViewsAsync>d__35>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ClearFlightViewsAsync>d__35&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseOwnedViewsAsync>d__36>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseOwnedViewsAsync>d__36&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseViewAsync>d__38>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CloseViewAsync>d__38&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CompleteVehicleSelectAsync>d__33>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<CompleteVehicleSelectAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<SpawnSelectedAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<StartSelectedAsync>d__26>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<StartSelectedAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.DroneFlightVehicleSelectView.<FollowMenuFocusAsync>d__15>(Hotfix.DroneFlightVehicleSelectView.<FollowMenuFocusAsync>d__15&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.GraphicsSettingsHandler.<ApplyModeAsync>d__2>(Hotfix.GraphicsSettingsHandler.<ApplyModeAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.HubLanguageFlow.<RebuildAsync>d__5>(Hotfix.HubLanguageFlow.<RebuildAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26>(Hotfix.HubSettingsView.<CancelAndCloseAsync>d__26&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.HubSettingsView.<SubmitAsync>d__24>(Hotfix.HubSettingsView.<SubmitAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartMenuPaperMotion.<FoldAsync>d__21>(Hotfix.KagitKart.KagitKartMenuPaperMotion.<FoldAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<CloseHintsViewAsync>d__8>(Hotfix.KagitKart.KagitKartWorld.<CloseHintsViewAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<CloseOptionsAsync>d__49>(Hotfix.KagitKart.KagitKartWorld.<CloseOptionsAsync>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168>(Hotfix.KagitKart.KagitKartWorld.<CloseViewsAsync>d__168&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<InitializeHintsAsync>d__5>(Hotfix.KagitKart.KagitKartWorld.<InitializeHintsAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156>(Hotfix.KagitKart.KagitKartWorld.<OpenMenuAsync>d__156&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<ShowHintsViewAsync>d__7>(Hotfix.KagitKart.KagitKartWorld.<ShowHintsViewAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<StartRaceAsync>d__149>(Hotfix.KagitKart.KagitKartWorld.<StartRaceAsync>d__149&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150>(Hotfix.KagitKart.KagitKartWorld.<StartRoundAsync>d__150&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.MainMenuHandler.<EnterAsync>d__2>(Hotfix.MainMenuHandler.<EnterAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.MainMenuView.<OpenEffectsAsync>d__33>(Hotfix.MainMenuView.<OpenEffectsAsync>d__33&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.MainMenuView.<OpenSettingsAsync>d__31>(Hotfix.MainMenuView.<OpenSettingsAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.MainMenuView.<UpdateMenuInputAsync>d__16>(Hotfix.MainMenuView.<UpdateMenuInputAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.PresentationCinematicPanel.<ExitLetterboxAsync>d__23>(Hotfix.PresentationCinematicPanel.<ExitLetterboxAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.PresentationCinematicPanel.<PlayAsync>d__22>(Hotfix.PresentationCinematicPanel.<PlayAsync>d__22&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.PresentationLabView.<PlayAsync>d__28>(Hotfix.PresentationLabView.<PlayAsync>d__28&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.PresentationLabView.<StopAndRestoreAsync>d__29>(Hotfix.PresentationLabView.<StopAndRestoreAsync>d__29&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.PresentationTimePanel.<PlayAsync>d__8>(Hotfix.PresentationTimePanel.<PlayAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.SceneManagement.GameSceneLoadingPresenter.<RestoreAsync>d__4>(Hotfix.SceneManagement.GameSceneLoadingPresenter.<RestoreAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.SceneManagement.GameSceneNavigator.<WaitUntilStableAsync>d__21>(Hotfix.SceneManagement.GameSceneNavigator.<WaitUntilStableAsync>d__21&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.SettingsHandler.<ApplyAsync>d__1>(Hotfix.SettingsHandler.<ApplyAsync>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.SimpleTipsTrigger.<ObserveShowAsync>d__23>(Hotfix.SimpleTipsTrigger.<ObserveShowAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.SingleUIManager.<CloseSimpleAsync>d__11>(Hotfix.SingleUIManager.<CloseSimpleAsync>d__11&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<Hotfix.SingleUIManager.<ObserveAsync>d__12>(Hotfix.SingleUIManager.<ObserveAsync>d__12&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>.Start<Hotfix.HubLanguageFlow.<ApplyAsync>d__4>(Hotfix.HubLanguageFlow.<ApplyAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.Settings.SettingsApplyResult>.Start<Hotfix.SettingsFlow.<ApplyAsync>d__3>(Hotfix.SettingsFlow.<ApplyAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.Start<Hotfix.PresentationLabFlow.<CloseAsync>d__4>(Hotfix.PresentationLabFlow.<CloseAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.Start<Hotfix.PresentationLabFlow.<OpenAsync>d__3>(Hotfix.PresentationLabFlow.<OpenAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.Start<Hotfix.SettingsFlow.<OpenAsync>d__2>(Hotfix.SettingsFlow.<OpenAsync>d__2&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.Start<Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14>(Hotfix.SingleUIManager.<CompleteSimpleShowAsync>d__14&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Core.Runtime.UIOperationResult>.Start<Hotfix.SingleUIManager.<ShowTipsMessageBarAsync>d__3>(Hotfix.SingleUIManager.<ShowTipsMessageBarAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneRuntimeResult>.Start<Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8>(Hotfix.SceneManagement.GameSceneRuntime.<LoadAsync>d__8&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneRuntimeResult>.Start<Hotfix.SceneManagement.GameSceneRuntime.<ReturnToHubAsync>d__9>(Hotfix.SceneManagement.GameSceneRuntime.<ReturnToHubAsync>d__9&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.Start<Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24>(Hotfix.SceneManagement.GameSceneNavigator.<ReloadCurrentAsync>d__24&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<Hotfix.SceneManagement.GameSceneSwitchResult>.Start<Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23>(Hotfix.SceneManagement.GameSceneNavigator.<SwitchAsync>d__23&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<RestoreFlightViewsAsync>d__37&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowFlightViewsAsync>d__34>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowFlightViewsAsync>d__34&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowVehicleSelectAsync>d__32>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ShowVehicleSelectAsync>d__32&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4>(Hotfix.SceneManagement.DemoIslandEditorBootstrap.<EnsureReadyAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<byte>.Start<Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4>(Hotfix.SettingsFlow.<ConfirmDisplayAsync>d__4&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Cfg.LubanConfigService.<>c__DisplayClass6_0.<<LoadFromResourcesAsync>b__0>d>(Cfg.LubanConfigService.<>c__DisplayClass6_0.<<LoadFromResourcesAsync>b__0>d&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Cfg.LubanConfigService.<LoadFromResourcesAsync>d__6>(Cfg.LubanConfigService.<LoadFromResourcesAsync>d__6&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Cfg.LubanConfigService.<LoadTablesAsync>d__7>(Cfg.LubanConfigService.<LoadTablesAsync>d__7&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Hotfix.SceneManagement.GameSceneLoadingPresenter.<BeginAsync>d__1>(Hotfix.SceneManagement.GameSceneLoadingPresenter.<BeginAsync>d__1&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Start<Hotfix.SceneManagement.GameSceneLoadingPresenter.<CompleteAsync>d__3>(Hotfix.SceneManagement.GameSceneLoadingPresenter.<CompleteAsync>d__3&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<BeginSelectedRaceAsync>d__160>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<BeginSelectedRaceAsync>d__160&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ChangeRoundAsync>d__162>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ChangeRoundAsync>d__162&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<CloseDestroyedHintsViewAsync>d__16>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<CloseDestroyedHintsViewAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<CloseOptionsCommandAsync>d__50>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<CloseOptionsCommandAsync>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ResumeFromOptionsAsync>d__52>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ResumeFromOptionsAsync>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43>(Cysharp.Threading.Tasks.UniTask.Awaiter&,Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<OpenOptionsAsync>d__48>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<OpenOptionsAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.KagitKart.KagitKartWorld.<SynchronizeModalAsync>d__167>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.KagitKart.KagitKartWorld.<SynchronizeModalAsync>d__167&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>,Hotfix.WallSqueeze.WallSqueezeWorld.<SynchronizeModalAsync>d__48>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>&,Hotfix.WallSqueeze.WallSqueezeWorld.<SynchronizeModalAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>&,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>,Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49>(Cysharp.Threading.Tasks.UniTask.Awaiter<Hotfix.SceneManagement.GameSceneSwitchResult>&,Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>,Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>&,Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Cfg.LubanConfigService.<InitializeCoreAsync>d__5>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Cfg.LubanConfigService.<InitializeCoreAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter<object>,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157>(Cysharp.Threading.Tasks.UniTask.Awaiter<object>&,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.YieldAwaitable.Awaiter,Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49>(Cysharp.Threading.Tasks.YieldAwaitable.Awaiter&,Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Cfg.LubanConfigService.<InitializeCoreAsync>d__5>(Cfg.LubanConfigService.<InitializeCoreAsync>d__5&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<BeginAsync>d__25&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31>(Hotfix.DroneFlight.DroneFlightSceneCoordinator.<ChangeSceneAsync>d__31&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<BeginSelectedRaceAsync>d__160>(Hotfix.KagitKart.KagitKartWorld.<BeginSelectedRaceAsync>d__160&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<ChangeRoundAsync>d__162>(Hotfix.KagitKart.KagitKartWorld.<ChangeRoundAsync>d__162&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<CloseDestroyedHintsViewAsync>d__16>(Hotfix.KagitKart.KagitKartWorld.<CloseDestroyedHintsViewAsync>d__16&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<CloseOptionsCommandAsync>d__50>(Hotfix.KagitKart.KagitKartWorld.<CloseOptionsCommandAsync>d__50&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148>(Hotfix.KagitKart.KagitKartWorld.<InitializeAsync>d__148&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<OpenOptionsAsync>d__48>(Hotfix.KagitKart.KagitKartWorld.<OpenOptionsAsync>d__48&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<ResumeFromOptionsAsync>d__52>(Hotfix.KagitKart.KagitKartWorld.<ResumeFromOptionsAsync>d__52&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169>(Hotfix.KagitKart.KagitKartWorld.<ReturnHubAsync>d__169&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157>(Hotfix.KagitKart.KagitKartWorld.<ShowPodiumAsync>d__157&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159>(Hotfix.KagitKart.KagitKartWorld.<ShowPodiumPageAsync>d__159&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58>(Hotfix.KagitKart.KagitKartWorld.<ShowStandingsAsync>d__58&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.KagitKart.KagitKartWorld.<SynchronizeModalAsync>d__167>(Hotfix.KagitKart.KagitKartWorld.<SynchronizeModalAsync>d__167&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43>(Hotfix.WallSqueeze.WallSqueezeWorld.<InitializeAsync>d__43&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49>(Hotfix.WallSqueeze.WallSqueezeWorld.<ReturnHubAsync>d__49&)
		// System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskVoidMethodBuilder.Start<Hotfix.WallSqueeze.WallSqueezeWorld.<SynchronizeModalAsync>d__48>(Hotfix.WallSqueeze.WallSqueezeWorld.<SynchronizeModalAsync>d__48&)
		// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>> Cysharp.Threading.Tasks.Internal.StateTuple.Create<Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>>(Cysharp.Threading.Tasks.UniTask.Awaiter<Core.Runtime.UIOperationResult>)
		// Cysharp.Threading.Tasks.Internal.StateTuple<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>> Cysharp.Threading.Tasks.Internal.StateTuple.Create<Cysharp.Threading.Tasks.UniTask.Awaiter<byte>>(Cysharp.Threading.Tasks.UniTask.Awaiter<byte>)
		// Cysharp.Threading.Tasks.UniTask<byte> Cysharp.Threading.Tasks.UniTask.FromCanceled<byte>(System.Threading.CancellationToken)
		// Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult> Cysharp.Threading.Tasks.UniTask.FromResult<Core.Runtime.UIOperationResult>(Core.Runtime.UIOperationResult)
		// Cysharp.Threading.Tasks.UniTask<byte> Cysharp.Threading.Tasks.UniTask.FromResult<byte>(byte)
		// Cysharp.Threading.Tasks.UniTask<byte> Cysharp.Threading.Tasks.UniTaskExtensions.AttachExternalCancellation<byte>(Cysharp.Threading.Tasks.UniTask<byte>,System.Threading.CancellationToken)
		// System.Void Cysharp.Threading.Tasks.UniTaskExtensions.Forget<Core.Runtime.UIOperationResult>(Cysharp.Threading.Tasks.UniTask<Core.Runtime.UIOperationResult>)
		// System.Void Cysharp.Threading.Tasks.UniTaskExtensions.Forget<byte>(Cysharp.Threading.Tasks.UniTask<byte>)
		// object Newtonsoft.Json.JsonConvert.DeserializeObject<object>(string)
		// object Newtonsoft.Json.JsonConvert.DeserializeObject<object>(string,Newtonsoft.Json.JsonSerializerSettings)
		// System.Void SleepyStudios.LoopScroll.LoopScrollView.RegisterCellBinding<object>(System.Action<object,int,SleepyStudios.LoopScroll.CellBindContext>,System.Action<object,SleepyStudios.LoopScroll.CellBindContext>)
		// object System.Activator.CreateInstance<object>()
		// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector3> System.Array.AsReadOnly<UnityEngine.Vector3>(UnityEngine.Vector3[])
		// System.Collections.ObjectModel.ReadOnlyCollection<object> System.Array.AsReadOnly<object>(object[])
		// UnityEngine.Color[] System.Array.ConvertAll<object,UnityEngine.Color>(object[],System.Converter<object,UnityEngine.Color>)
		// Hotfix.WallSqueeze.WallSqueezeMonsterLayout[] System.Array.Empty<Hotfix.WallSqueeze.WallSqueezeMonsterLayout>()
		// Hotfix.WallSqueeze.WallSqueezeWallLayout[] System.Array.Empty<Hotfix.WallSqueeze.WallSqueezeWallLayout>()
		// UnityEngine.Quaternion[] System.Array.Empty<UnityEngine.Quaternion>()
		// UnityEngine.Vector3[] System.Array.Empty<UnityEngine.Vector3>()
		// int[] System.Array.Empty<int>()
		// object[] System.Array.Empty<object>()
		// System.Void System.Array.Fill<double>(double[],double)
		// System.Void System.Array.Fill<float>(float[],float)
		// System.Void System.Array.Fill<int>(int[],int)
		// object System.Array.Find<object>(object[],System.Predicate<object>)
		// int System.Array.FindIndex<object>(object[],System.Predicate<object>)
		// int System.Array.FindIndex<object>(object[],int,int,System.Predicate<object>)
		// int System.Array.IndexOf<int>(int[],int)
		// int System.Array.IndexOf<object>(object[],object)
		// int System.Array.IndexOfImpl<int>(int[],int,int,int)
		// int System.Array.IndexOfImpl<object>(object[],object,int,int)
		// System.Void System.Array.Sort<UnityEngine.RaycastHit>(UnityEngine.RaycastHit[],System.Comparison<UnityEngine.RaycastHit>)
		// bool System.Enum.TryParse<int>(string,bool,int&)
		// object System.Linq.Enumerable.First<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderByDescending<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// object[] System.Linq.Enumerable.ToArray<object>(System.Collections.Generic.IEnumerable<object>)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// int System.Runtime.InteropServices.Marshal.SizeOf<Hotfix.KagitKart.KagitKartRaceFx.BitGpu>()
		// int System.Runtime.InteropServices.Marshal.SizeOf<Hotfix.KagitKart.KagitKartRaceFx.CardGpu>()
		// System.Void* Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf<UnityEngine.Vector2>(UnityEngine.Vector2&)
		// System.Void* Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf<float>(float&)
		// int Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<UnityEngine.Vector2>()
		// int Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<float>()
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.Component.GetComponentInChildren<object>()
		// object UnityEngine.Component.GetComponentInChildren<object>(bool)
		// object UnityEngine.Component.GetComponentInParent<object>()
		// object UnityEngine.Component.GetComponentInParent<object>(bool)
		// object[] UnityEngine.Component.GetComponentsInChildren<object>()
		// object[] UnityEngine.Component.GetComponentsInChildren<object>(bool)
		// bool UnityEngine.Component.TryGetComponent<object>(object&)
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object UnityEngine.GameObject.GetComponentInChildren<object>(bool)
		// object UnityEngine.GameObject.GetComponentInParent<object>()
		// object UnityEngine.GameObject.GetComponentInParent<object>(bool)
		// object[] UnityEngine.GameObject.GetComponents<object>()
		// object[] UnityEngine.GameObject.GetComponentsInChildren<object>(bool)
		// bool UnityEngine.GameObject.TryGetComponent<object>(object&)
		// UnityEngine.Vector2 UnityEngine.InputSystem.InputAction.ReadValue<UnityEngine.Vector2>()
		// float UnityEngine.InputSystem.InputAction.ReadValue<float>()
		// UnityEngine.Vector2 UnityEngine.InputSystem.InputActionState.ApplyProcessors<UnityEngine.Vector2>(int,UnityEngine.Vector2,UnityEngine.InputSystem.InputControl<UnityEngine.Vector2>)
		// float UnityEngine.InputSystem.InputActionState.ApplyProcessors<float>(int,float,UnityEngine.InputSystem.InputControl<float>)
		// UnityEngine.Vector2 UnityEngine.InputSystem.InputActionState.ReadValue<UnityEngine.Vector2>(int,int,bool)
		// float UnityEngine.InputSystem.InputActionState.ReadValue<float>(int,int,bool)
		// object UnityEngine.JsonUtility.FromJson<object>(string)
		// object UnityEngine.Object.Instantiate<object>(object)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
		// object UnityEngine.Rendering.ContextContainer.Get<object>()
		// bool UnityEngine.Rendering.VolumeProfile.TryGet<object>(System.Type,object&)
		// bool UnityEngine.Rendering.VolumeProfile.TryGet<object>(object&)
		// object UnityEngine.ScriptableObject.CreateInstance<object>()
	}
}