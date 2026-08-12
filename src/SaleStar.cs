using BepInEx;
using EntityStates;
using EntityStates.Barrel;
using HG;
using Logger;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Items;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using static RetroactiveMacro.SaleStar;

namespace RetroactiveMacro;

public static class SaleStar
{
	[SystemInitializer]
	static void Init()
	{
		if (!RetroactiveMacro.ChangeSaleStar.Value)
			return;
			
		string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lang", "SaleStar.language");
		if (File.Exists(path))
		{
			LanguageAPI.AddOverlayPath(path);
		} else {
			Log.Error("Failed to find path: " + path);
		}

		On.EntityStates.Barrel.Opened.OnEnter += Opened_OnEnter;
		IL.RoR2.InteractionDriver.MyFixedUpdate += InteractionDriver_MyFixedUpdate;
		IL.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;
		SceneExitController.onBeginExit += BeginExit;
		On.RoR2.ChestBehavior.Open += ChestBehavior_Open;
		IL.RoR2.OutsideInteractableLocker.LockInteractable += LockInteractable;
	}

	private static void LockInteractable(ILContext il)
	{
		ILCursor c = new ILCursor(il);
		ILLabel label = c.DefineLabel();

		if (c.TryGotoNext(MoveType.After,
			x => x.MatchLdfld(typeof(OutsideInteractableLocker), nameof(OutsideInteractableLocker.lockPrefab))
		))
		{
			c.Emit(OpCodes.Ldarg_1);
			c.EmitDelegate<Func<GameObject, IInteractableLockable, GameObject>>(replaceLock);
		}
		else
		{
			Log.Error(il.Method.Name + "Failed to find patch location");
		}

		static GameObject replaceLock(GameObject LockPrefab, IInteractableLockable interactableLockable)
		{
			GameObject interactable = interactableLockable.GetGameObject();
			if (interactable && interactable.TryGetComponent(out PurchaseInteraction purchaseInteraction))
			{
				if (purchaseInteraction.costType == SaleStarCost.SaleStar)
					return RetroactiveMacro.FakeInteractableLock;
			}
			return LockPrefab;
		}
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
			if (self.TryGetComponent(out ChestLootTracker chestLootTracker) && chestLootTracker.ItemIndex == DLC2Content.Items.LowerPricedChests.itemIndex)
				return;
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

	private static void ChestBehavior_Open(On.RoR2.ChestBehavior.orig_Open orig, ChestBehavior self)
	{
		if (RetroactiveMacro.ExcludeSaleStarChest.Value)
		{
			ChestLootTracker tracker = self.EnsureComponent<ChestLootTracker>();
			if (self.currentPickup.isValid)
			{
				tracker.ItemIndex = PickupCatalog.GetPickupDef(self.currentPickup.pickupIndex).itemIndex;
			}
		}
		orig(self);
	}

	public class ChestLootTracker : MonoBehaviour
	{
		public ItemIndex ItemIndex;
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
			ItemIndex itemIndex = DLC2Content.Items.LowerPricedChests.itemIndex;
			if (QualityCompat.enabled)
			{
				itemIndex = QualityCompat.GetBaseItemIndex(DLC2Content.Items.LowerPricedChests.itemIndex);
			}
			if (interaction.TryGetComponent(out ChestLootTracker chestLootTracker) && chestLootTracker.ItemIndex == itemIndex)
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