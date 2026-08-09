using BepInEx;
using BepInEx.Configuration;
using Logger;
using R2API;
using R2API.Models;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace RetroactiveMacro;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(RiskOfOptions.PluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
public class RetroactiveMacro : BaseUnityPlugin
{
	public const string PluginGUID = PluginAuthor + "." + PluginName;
	public const string PluginAuthor = "Onyx";
	public const string PluginName = "RetroactiveMacro";
	public const string PluginVersion = "1.0.0";

	public static RetroactiveMacro Instance;
	public static AssetBundle Bundle;

	public static ConfigEntry<bool> ChangeSaleStar { get; set; }
	public static ConfigEntry<bool> ChangeCard { get; set; }

	public void Awake()
	{
		Log.Init(Logger);
		BepInPlugin bepInPlugin = new(PluginGUID, PluginName, PluginVersion);
		Instance = SingletonHelper.Assign(Instance, this);
		Options.Init();

		Bundle = AssetBundle.LoadFromFile(AssetBundlePath);
		ContentAddition.AddEntityState<CloseOpen>(out _);

		AssetAsyncReferenceManager<RuntimeAnimatorController>.LoadAsset(new(RoR2_Base_MultiShopTerminal.animMultiShopTerminal_controller)).Completed += (controller) =>
		{
			AnimatorDiff diff = RetroactiveMacro.Bundle.LoadAsset<AnimatorDiff>("Assets/Animations/MultiShop/Reopen.controllerdiff");
			AnimatorModifications newAnimations = AnimatorModifications.CreateFromDiff(diff, bepInPlugin);
			AnimationsAPI.AddModifications(GetBundlePath("ror2-base-multishopterminal_assets_all_e550cfc9295bb6ea35be13bc7fc042d2"), controller.Result, newAnimations);
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_MultiShopTerminal.MultiShopTerminal_prefab)).Completed += (prefab) =>
			{
				Transform model = prefab.Result.transform.Find("Display/mdlMultiShopTerminal");
				AnimationsAPI.AddAnimatorController(model.GetComponent<Animator>(), controller.Result);
			};
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_MultiShopEquipmentTerminal.MultiShopEquipmentTerminal_prefab)).Completed += (prefab) =>
			{
				Transform model = prefab.Result.transform.Find("Display/mdlMultiShopTerminal");
				AnimationsAPI.AddAnimatorController(model.GetComponent<Animator>(), controller.Result);
			};
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_MultiShopLargeTerminal.MultiShopLargeTerminal_prefab)).Completed += (prefab) =>
			{
				Transform model = prefab.Result.transform.Find("Display/mdlMultiShopTerminal");
				AnimationsAPI.AddAnimatorController(model.GetComponent<Animator>(), controller.Result);
			};
		};

		AssetAsyncReferenceManager<RuntimeAnimatorController>.LoadAsset(new(RoR2_Base_EquipmentBarrel.animEquipmentBarrel_controller)).Completed += (controller) =>
		{
			AnimatorDiff diff = RetroactiveMacro.Bundle.LoadAsset<AnimatorDiff>("Assets/Animations/EquipBarrel/Closing.controllerdiff");
			AnimatorModifications newAnimations = AnimatorModifications.CreateFromDiff(diff, bepInPlugin);
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_EquipmentBarrel.EquipmentBarrel_prefab)).Completed += (prefab) =>
			{
				AnimationsAPI.AddModifications(GetBundlePath("ror2-base-equipmentbarrel_static_assets_all_4b2bbdba8df2b852424cc377002cbfb8"), controller.Result, newAnimations);
				Transform model = prefab.Result.transform.Find("ModelBase/mdlEquipmentBarrel");
				AnimationsAPI.AddAnimatorController(model.GetComponent<Animator>(), controller.Result);
			};
		};

		AssetAsyncReferenceManager<RuntimeAnimatorController>.LoadAsset(new(RoR2_DLC1_FreeChestMultiShop.animShippingDronePod_controller)).Completed += (controller) =>
		{
			AnimatorDiff diff = RetroactiveMacro.Bundle.LoadAsset<AnimatorDiff>("Assets/Animations/ShippingDrone/Reopen.controllerdiff");
			AnimatorModifications newAnimations = AnimatorModifications.CreateFromDiff(diff, bepInPlugin);
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC1_FreeChestTerminalShippingDrone.FreeChestTerminalShippingDrone_prefab)).Completed += (prefab) =>
			{
				AnimationsAPI.AddModifications(GetBundlePath("ror2-dlc1-freechestmultishop_static_assets_all_1d7b71789b08d225b234934cdc572855"), controller.Result, newAnimations);
				Transform model = prefab.Result.transform.Find("mdlShippingDronePod");
				AnimationsAPI.AddAnimatorController(model.GetComponent<Animator>(), controller.Result);
			};
		};
	}

	public static string AssetBundlePath
	{
		get
		{
			return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "bundle.bundle");
		}
	}

	public static string GetBundlePath(string bundleName)
	{
		return System.IO.Path.Combine(
			Addressables.RuntimePath,
			"StandaloneWindows64",
			bundleName + ".bundle");
	}
}


