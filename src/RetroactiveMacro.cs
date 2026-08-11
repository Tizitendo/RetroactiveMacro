using BepInEx;
using BepInEx.Configuration;
using Logger;
using Mono.Cecil.Cil;
using MonoDetour;
using MonoMod.Cil;
using R2API;
using R2API.Models;
using RoR2;
using RoR2.ContentManagement;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Reflection;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static RetroactiveMacro.QualityCompat;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace RetroactiveMacro;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(RiskOfOptions.PluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.Gorakh.ItemQualities", BepInDependency.DependencyFlags.SoftDependency)]
public class RetroactiveMacro : BaseUnityPlugin
{
	public const string PluginGUID = PluginAuthor + "." + PluginName;
	public const string PluginAuthor = "Onyx";
	public const string PluginName = "RetroactiveMacro";
	public const string PluginVersion = "1.0.0";

	public static RetroactiveMacro Instance;
	public static AssetBundle Bundle;
	public static GameObject FakeInteractableLock;

	public static ConfigEntry<bool> ChangeSaleStar { get; set; }
	public static ConfigEntry<bool> ChangeCard { get; set; }
	public static ConfigEntry<bool> ExcludeEquipShops { get; set; }
	public static ConfigEntry<bool> ExcludeSaleStarChest { get; set; }

	public void Awake()
	{
		Log.Init(Logger);
		BepInPlugin bepInPlugin = new(PluginGUID, PluginName, PluginVersion);
		Instance = SingletonHelper.Assign(Instance, this);
		Options.Init();

		MonoDetourManager.InvokeHookInitializers(Assembly.GetExecutingAssembly(), false);

		Bundle = AssetBundle.LoadFromFile(AssetBundlePath);
		ContentAddition.AddEntityState<CloseOpen>(out _);

		FakeInteractableLock = PrefabAPI.CreateEmptyPrefab("FakeInteractableLock", true);
		PrefabAPI.RegisterNetworkPrefab(FakeInteractableLock);

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

		AssetAsyncReferenceManager<RuntimeAnimatorController>.LoadAsset(new(RoR2_Base_TreasureCache.animLockbox_controller)).Completed += (controller) =>
		{
			AnimatorDiff diff = RetroactiveMacro.Bundle.LoadAsset<AnimatorDiff>("Assets/Animations/Lockbox/Closing.controllerdiff");
			AnimatorModifications newAnimations = AnimatorModifications.CreateFromDiff(diff, bepInPlugin);
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_TreasureCache.Lockbox_prefab)).Completed += (prefab) =>
			{
				AnimationsAPI.AddModifications(GetBundlePath("ror2-base-treasurecache_static_assets_all_d08d914a36f0eb6c803eb3c820e8852d"), controller.Result, newAnimations);
				Transform model = prefab.Result.transform.Find("ModelBase/mdlKeyLockbox");
				AnimationsAPI.AddAnimatorController(model.GetComponent<Animator>(), controller.Result);
				prefab.Result.AddComponent<LastBuyTracker>();
			};
		};

		AssetAsyncReferenceManager<RuntimeAnimatorController>.LoadAsset(new(RoR2_Base_GoldChest.animGoldChest_controller)).Completed += (controller) =>
		{
			AnimatorDiff diff = RetroactiveMacro.Bundle.LoadAsset<AnimatorDiff>("Assets/Animations/GoldChest/Closing.controllerdiff");
			AnimatorModifications newAnimations = AnimatorModifications.CreateFromDiff(diff, bepInPlugin);
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_GoldChest.GoldChest_prefab)).Completed += (prefab) =>
			{
				AnimationsAPI.AddModifications(GetBundlePath("ror2-base-goldchest_static_assets_all_073b623b25fd304ed31873a2430b080e"), controller.Result, newAnimations);
				Transform model = prefab.Result.transform.Find("mdlGoldChest");
				AnimationsAPI.AddAnimatorController(model.GetComponent<Animator>(), controller.Result);
			};
		};

		IL.RoR2.UI.PingIndicator.Update += PingIndicator_Update;
	}

	private static void PingIndicator_Update(ILContext il)
	{
		ILCursor c = new(il);
		ILLabel label = null;

		if (c.TryGotoNext(MoveType.After,
			x => x.MatchLdfld(typeof(PurchaseInteraction), nameof(PurchaseInteraction.available)),
			x => x.MatchBrtrue(out label)
		))
		{
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<PingIndicator, bool>>(OverwritePingable);
			c.Emit(OpCodes.Brtrue, label);
		}
		else
		{
			Log.Error(il.Method.Name + "Failed to find patch location");
		}

		static bool OverwritePingable(PingIndicator self)
		{
			if (ChangeCard.Value)
			{
				if (self.pingTargetPurchaseInteraction.TryGetComponent(out ShopTerminalBehavior shopTerminalBehavior))
				{
					if (shopTerminalBehavior.serverMultiShopController)
					{
						bool pingable = false;
						foreach(GameObject terminal in shopTerminalBehavior.serverMultiShopController.terminalGameObjects)
						{
							if (terminal.TryGetComponent(out ShopTerminalBehavior behavior) && !behavior.hasBeenPurchased)
							{
								pingable = true;
								break;
							}
						}
						if (!pingable)
							return false;
					}

					int layer = shopTerminalBehavior.animator.GetLayerIndex("Body");
					if (shopTerminalBehavior.animator.GetCurrentAnimatorStateInfo(layer).normalizedTime != 0)
						return true;
				}
			}

			if (ChangeSaleStar.Value)
			{
				return self.pingTargetPurchaseInteraction.costType == SaleStarCost.SaleStar;
			}
			return false;
		}
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


