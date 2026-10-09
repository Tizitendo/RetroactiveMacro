using Logger;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.HookGen;
using MonoMod.Cil;
using R2API;
using R2API.Models;
using R2API.Utils;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

[assembly: MonoDetourTargets(typeof(ItemQualities.Items.LowerPricedChests))]
[assembly: MonoDetourTargets(typeof(ItemQualities.ItemCostQualityPatch))]
[assembly: MonoDetourTargets(typeof(ItemQualities.ItemQualitiesContent))]

namespace RetroactiveMacro;

public static class QualityCompat
{
	private static bool? _enabled;
	public static bool enabled
	{
		get
		{
			if (_enabled == null)
			{
				_enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.Gorakh.ItemQualities");
			}
			return (bool)_enabled;
		}
	}

	[SystemInitializer]
	public static void Init()
	{
		if (enabled)
		{
			InitContent();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static void InjectQualityInitializer()
	{
		SystemInitializerInjector.InjectDependency(typeof(QualityCompat), typeof(ItemQualities.QualityCatalog));
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	private static void InitContent()
	{
		if (RetroactiveMacro.ChangeSaleStar.Value)
		{
			Md.ItemQualities.Items.LowerPricedChests.generateQualityDropTiersFromSaleStars.ILHook(generateQualityDropTiersFromSaleStars);
			Md.ItemQualities.Items.LowerPricedChests.tryUpgradePickupQualityFromSaleStars.ILHook(tryUpgradePickupQualityFromSaleStars);

			Md.ItemQualities.ItemCostQualityPatch.tryUpgradeQualityFromCost.Postfix(tryUpgradeQualityFromCost);
		}

		AssetAsyncReferenceManager<RuntimeAnimatorController>.LoadAsset(new AssetReferenceT<RuntimeAnimatorController>(RoR2_Base_EquipmentBarrel.animEquipmentBarrel_controller)).Completed += (controller) =>
		{
			AnimatorDiff diff = RetroactiveMacro.Bundle.LoadAsset<AnimatorDiff>("Assets/Animations/EquipBarrel/Closing.controllerdiff");
			AnimatorModifications newAnimations = AnimatorModifications.CreateFromDiff(diff, RetroactiveMacro.bepInPlugin);
			GameObject prefab = ItemQualities.ItemQualitiesContent.SpawnCards.QualityEquipmentBarrel.prefab;
			AnimationsAPI.AddModifications(RetroactiveMacro.GetBaseBundlePath("ror2-base-equipmentbarrel_static_assets_all_4b2bbdba8df2b852424cc377002cbfb8"), controller.Result, newAnimations);
			RetroactiveMacro.RegisterPurchaseReplacementAnimation(controller.Result, prefab, "ModelBase/mdlQualityEquipmentBarrel");
		};
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static EquipmentIndex GetBaseEquipmentIndex(EquipmentIndex equipmentIndex)
	{
		if (equipmentIndex == EquipmentIndex.None)
			return equipmentIndex;
		return PickupCatalog.GetPickupDef(ItemQualities.QualityCatalog.GetPickupIndexOfQuality(PickupCatalog.equipmentIndexToPickupIndex[(int)equipmentIndex], ItemQualities.QualityTier.None)).equipmentIndex;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static ItemIndex GetBaseItemIndex(ItemIndex itemIndex)
	{
		if (itemIndex == ItemIndex.None)
			return itemIndex;
		return ItemQualities.QualityCatalog.GetItemIndexOfQuality(itemIndex, ItemQualities.QualityTier.None);
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static void AddLastbuyTracker(GameObject gameObject)
	{
		gameObject.AddComponent<LastBuyTracker>();
	}

	private static void tryUpgradeQualityFromCost(ref PickupIndex intendedDropPickupIndex, ref GameObject dropperObject, ref PickupIndex returnValue)
	{
		if (!dropperObject.TryGetComponent(out LastBuyTracker lastBuyTracker))
			return;
		if (lastBuyTracker.qualityTier > ItemQualities.QualityCatalog.GetQualityTier(returnValue))
		{
			returnValue = ItemQualities.QualityCatalog.GetPickupIndexOfQuality(intendedDropPickupIndex, lastBuyTracker.qualityTier);
		}
		lastBuyTracker.qualityTier = ItemQualities.QualityCatalog.GetQualityTier(returnValue);
	}

	private static void tryUpgradePickupQualityFromSaleStars(ILManipulationInfo info)
	{
		ILCursor c = new(info.Context);

		if (c.TryGotoNext(MoveType.Before,
			x => x.MatchLdcI4(0),
			x => x.MatchBle(out ILLabel _)
		))
		{
			c.Emit(OpCodes.Ldc_I4, 1);
			c.Emit(OpCodes.Add);
		}
		else
		{
			Log.Error(info.Context.Method.Name + "Failed to find patch location");
		}

		if (c.TryGotoNext(MoveType.After,
			x => x.MatchLdcI4(1),
			x => x.MatchSub()
		))
		{
			c.Emit(OpCodes.Ldc_I4, 1);
			c.Emit(OpCodes.Add);
		}
		else
		{
			Log.Error(info.Context.Method.Name + "Failed to find patch location 2");
		}
	}

	private static void generateQualityDropTiersFromSaleStars(ILManipulationInfo info)
	{
		ILCursor c = new(info.Context);

		if (c.TryGotoNext(MoveType.After,
			x => x.MatchLdcI4(1),
			x => x.MatchSub()
		))
		{
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<int, GameObject, int>>(makeFirstItemQuality);
		}
		else
		{
			Log.Error(info.Context.Method.Name + "Failed to find patch location");
		}

		static int makeFirstItemQuality(int minQuality, GameObject purchasedObject)
		{
			if (purchasedObject.TryGetComponent(out RouletteChestController rouletteChestController))
				return minQuality;
			return minQuality + 1;
		}
	}

	public class LastBuyTracker : MonoBehaviour
	{
		public ItemQualities.QualityTier qualityTier = ItemQualities.QualityTier.None;
	}
}