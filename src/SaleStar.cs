using BepInEx;
using EntityStates;
using EntityStates.Barrel;
using HG;
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
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
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
			//c.MarkLabel(label);
			//c.Index -= 3;
			//c.Emit(OpCodes.Br, label);
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
			if (interaction.TryGetComponent(out ChestLootTracker chestLootTracker) && chestLootTracker.ItemIndex == DLC2Content.Items.LowerPricedChests.itemIndex)
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