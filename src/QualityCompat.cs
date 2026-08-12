using Logger;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.HookGen;
using MonoMod.Cil;
using RoR2;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: MonoDetourTargets(typeof(ItemQualities.Items.LowerPricedChests))]
[assembly: MonoDetourTargets(typeof(ItemQualities.ItemCostQualityPatch))]

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

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static void Init()
	{
		if (!RetroactiveMacro.ChangeSaleStar.Value)
			return;

		Md.ItemQualities.Items.LowerPricedChests.generateQualityDropTiersFromSaleStars.ILHook(generateQualityDropTiersFromSaleStars);
		Md.ItemQualities.Items.LowerPricedChests.tryUpgradePickupQualityFromSaleStars.ILHook(tryUpgradePickupQualityFromSaleStars);

		Md.ItemQualities.ItemCostQualityPatch.tryUpgradeQualityFromCost.Postfix(tryUpgradeQualityFromCost);
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
		return PickupCatalog.GetPickupDef(ItemQualities.QualityCatalog.GetPickupIndexOfQuality(PickupCatalog.itemIndexToPickupIndex[(int)itemIndex], ItemQualities.QualityTier.None)).itemIndex; ;
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