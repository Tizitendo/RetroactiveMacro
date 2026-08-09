using EntityStates;
using EntityStates.Barrel;
using Logger;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Items;
using RoR2.UI;
using System;

namespace RetroactiveMacro;

public static class SaleStar
{
	[SystemInitializer]
	static void Init()
	{
		if (!RetroactiveMacro.ChangeSaleStar.Value)
			return;
		On.EntityStates.Barrel.Opened.OnEnter += Opened_OnEnter;
		IL.RoR2.InteractionDriver.MyFixedUpdate += InteractionDriver_MyFixedUpdate;
		IL.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;
		LanguageAPI.AddOverlay("ITEM_LOWERPRICEDCHESTS_PICKUP", "Reopen a chest for an additional reward. Usable once per stage.", "en");
		LanguageAPI.AddOverlay("ITEM_LOWERPRICEDCHESTS_DESC", "Reopen a chest for an extra item. <style=cStack>Each additional Sale Star increases the chance of getting more items by 5%</style>.", "en");
		//LanguageAPI.AddOverlayPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "language"));
		IL.RoR2.UI.PingIndicator.Update += PingIndicator_Update;
		SceneExitController.onBeginExit += onBeginExit;
	}

	private static void onBeginExit(SceneExitController controller)
	{
		foreach (PurchaseInteraction interaction in InstanceTracker.GetInstancesList<PurchaseInteraction>())
		{
			if (interaction.saleStarCompatible && interaction.costType == SaleStarCost.SaleStar)
			{
				interaction.SetAvailable(false);
			}
		}
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
			c.EmitDelegate<Func<PingIndicator, bool>>(SaleStarChestPingable);
			c.Emit(OpCodes.Brtrue, label);
		}
		else
		{
			Log.Error(il.Method.Name + "Failed to find patch location");
		}

		static bool SaleStarChestPingable(PingIndicator self)
		{
			return self.pingTargetPurchaseInteraction.costType == SaleStarCost.SaleStar;
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