using EntityStates;
using EntityStates.Barrel;
using Logger;
using Mono.Cecil.Cil;
using MonoDetour;
using MonoDetour.Cil;
using MonoDetour.HookGen;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Items;
using System;
using System.Reflection;
using UnityEngine;

namespace RetroactiveMacro;

public static class SaleStar
{
	[SystemInitializer]
	static void Init()
	{
		if (!RetroactiveMacro.ChangeSaleStar.Value)
			return;
		LanguageAPI.AddOverlayPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lang", "SaleStar.language"));

		On.EntityStates.Barrel.Opened.OnEnter += Opened_OnEnter;
		IL.RoR2.InteractionDriver.MyFixedUpdate += InteractionDriver_MyFixedUpdate;
		IL.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;
		SceneExitController.onBeginExit += BeginExit;
	}

	private static void BeginExit(SceneExitController controller)
	{
		foreach (PurchaseInteraction interaction in InstanceTracker.GetInstancesList<PurchaseInteraction>())
		{
			if (interaction.saleStarCompatible && interaction.costType == SaleStarCost.SaleStar)
			{
				interaction.SetAvailable(false);
			}
		}
	}

	private static void PurchaseInteraction_OnInteractionBegin(ILContext il)
	{
		ILCursor c = new(il);
		ILLabel label = null;

		if (!c.TryGotoNext(MoveType.After,
			x => x.MatchLdfld(typeof(PurchaseInteraction), nameof(PurchaseInteraction.saleStarCompatible)),
			x => x.MatchBrfalse(out label)
		))
		{
			Log.Error(il.Method.Name + "Failed to find patch location");
		}

		c.Emit(OpCodes.Ldarg_0);
		c.EmitDelegate<Func<PurchaseInteraction, bool>>(checkReopen);
		c.Emit(OpCodes.Brfalse, label);

		static bool checkReopen(PurchaseInteraction self)
		{
			if (self.TryGetComponent(out RouletteChestController rouletteChestController))
				return true;
			return self.costType == SaleStarCost.SaleStar;
		}

		if (c.TryGotoNext(MoveType.Before,
			x => x.MatchStfld(typeof(ChestBehavior), nameof(ChestBehavior.dropCount))
		))
		{
			c.Emit(OpCodes.Ldc_I4_1);
			c.Emit(OpCodes.Sub);
		}
		else
		{
			Log.Error(il.Method.Name + "Failed to find patch location");
		}
	}

	private static void InteractionDriver_MyFixedUpdate(ILContext il)
	{
		ILCursor c = new(il);
		ILLabel label = null;

		if (c.TryGotoNext(MoveType.After,
		x => x.MatchLdfld(typeof(PurchaseInteraction), nameof(PurchaseInteraction.saleStarCompatible)),
		x => x.MatchBrfalse(out label)
		))
		{
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<InteractionDriver, bool>>(checkReopen);
			c.Emit(OpCodes.Brfalse, label);
		}
		else
		{
			Log.Error(il.Method.Name + "Failed to find patch location");
		}

		static bool checkReopen(InteractionDriver self)
		{
			if (!self.currentInteractable.TryGetComponent(out PurchaseInteraction purchaseInteration))
				return false;
			if (self.currentInteractable.TryGetComponent(out RouletteChestController rouletteChestController))
				return true;
			return purchaseInteration.costType == SaleStarCost.SaleStar;
		}
	}

	private static void Opened_OnEnter(On.EntityStates.Barrel.Opened.orig_OnEnter orig, Opened self)
	{
		orig(self);
		if (!self.TryGetComponent(out ChestBehavior chestBehavior))
			return;
		if (!self.TryGetComponent(out PurchaseInteraction purchaseInteraction) || !purchaseInteraction.saleStarCompatible)
			return;

		if (purchaseInteraction.costType != SaleStarCost.SaleStar)
		{
			if (purchaseInteraction.lastActivator && purchaseInteraction.lastActivator.TryGetComponent(out CharacterBody body)
			&& body.inventory && body.inventory.GetItemCountEffective(DLC2Content.Items.LowerPricedChests) > 0)
			{
				purchaseInteraction.SetAvailable(true);
			}
			self.SetPingable(true);
			purchaseInteraction.costType = SaleStarCost.SaleStar;
			chestBehavior.openState = new SerializableEntityStateType(typeof(CloseOpen));
		}
		else
		{
			purchaseInteraction.costType = CostTypeIndex.Money;
		}
	}
}

public class CloseOpen : Closing
{
	public override void OnExit()
	{
		base.OnExit();
		if (!outer.TryGetComponent(out ChestBehavior chestBehavior))
			return;
		if (!outer.TryGetComponent(out PurchaseInteraction purchaseInteraction))
			return;

		chestBehavior.Roll();
		foreach (UnityEngine.Events.PersistentCall eventCall in purchaseInteraction.onPurchase.m_PersistentCalls.m_Calls)
		{
			if (eventCall.methodName == "ItemDrop")
			{
				chestBehavior.ItemDrop();
				break;
			}
		}

		outer.SetNextState(new Opening());
	}
}

public class LowerPricedChestsBodyBehavoir : BaseItemBodyBehavior
{
	[ItemDefAssociation(useOnServer = true, useOnClient = false)]
	private static ItemDef GetItemDef() => DLC2Content.Items.LowerPricedChests;

	void OnEnable()
	{
		foreach (PurchaseInteraction interaction in InstanceTracker.GetInstancesList<PurchaseInteraction>())
		{
			if (!interaction.TryGetComponent(out EntityStateMachine stateMachine) || stateMachine.state is not Opened)
				continue;
			if (interaction.saleStarCompatible && interaction.costType == SaleStarCost.SaleStar)
			{
				interaction.SetAvailable(true);
			}
		}
	}

	void OnDisable()
	{
		if (Util.GetItemCountForTeam(body.teamComponent.teamIndex, DLC2Content.Items.LowerPricedChests.itemIndex, true) > 0)
			return;
		foreach (PurchaseInteraction interaction in InstanceTracker.GetInstancesList<PurchaseInteraction>())
		{
			if (interaction.saleStarCompatible && interaction.costType == SaleStarCost.SaleStar)
			{
				interaction.SetAvailable(false);
			}
		}
	}
}

[MonoDetourTargets(typeof(ItemQualities.Items.LowerPricedChests))]
[MonoDetourTargets(typeof(ItemQualities.ItemCostQualityPatch))]
public class QualityCompat
{
	[MonoDetourHookInitialize]
	static void Init()
	{
		if (!RetroactiveMacro.ChangeSaleStar.Value)
			return;
		if (!enabled) 
			return;

		Md.ItemQualities.Items.LowerPricedChests.generateQualityDropTiersFromSaleStars.ILHook(generateQualityDropTiersFromSaleStars);
		Md.ItemQualities.Items.LowerPricedChests.tryUpgradePickupQualityFromSaleStars.ILHook(tryUpgradePickupQualityFromSaleStars);

		Md.ItemQualities.ItemCostQualityPatch.tryUpgradeQualityFromCost.Postfix(tryUpgradeQualityFromCost);
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

	private static void tryUpgradePickupQualityFromSaleStars(ILManipulationInfo info)
	{
		ILCursor c = new(info.Context);

		if (c.TryGotoNext(MoveType.After,
			x => x.MatchLdcI4(0),
			x => x.MatchCgt()
		))
		{
			c.Index--;
			c.Emit(OpCodes.Ldc_I4, 1);
			c.Emit(OpCodes.Sub);
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
			Log.Error(info.Context.Method.Name + "Failed to find patch location");
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
			//c.Emit(OpCodes.Ldc_I4, 1);
			//c.Emit(OpCodes.Add);
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