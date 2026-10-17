using Logger;
using On.RoR2.Items;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace RetroactiveMacro;

public static class Card
{
	[SystemInitializer]
	static void Init()
	{
		if (!RetroactiveMacro.ChangeCard.Value)
			return;

		AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_Base_TripleShop.TripleShop_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardItemHandler>();
		};
		if (!RetroactiveMacro.ExcludeEquipShops.Value)
		{
			AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_Base_TripleShopEquipment.TripleShopEquipment_prefab)).Completed += (x) =>
			{
				x.Result.AddComponent<MacroCardItemHandler>();
			};
		}
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_Base_TripleShopLarge.TripleShopLarge_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardItemHandler>();
		};
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_DLC1_FreeChestMultiShop.FreeChestMultiShop_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardItemHandler>();
		};
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_DLC3_TripleDroneShop.TripleDroneShop_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardDroneHandler>();
		};

		On.RoR2.ShopTerminalBehavior.UpdatePickupDisplayAndAnimations += UpdatePickupDisplayAndAnimations;
		SceneDirector.onPrePopulateSceneServer += PrePopulateSceneServer;
		On.EntityStates.DroneVendor.DroneVendorAssumeIdle.OnExit += DroneVendorAssumeIdle_OnExit;
		On.RoR2.CharacterBody.RecalculateStats += RecalculateStats;
		On.RoR2.Items.MultiShopCardUtils.OnPurchase += MultiShopCardUtils_OnPurchase;
	}

    private static void MultiShopCardUtils_OnPurchase(MultiShopCardUtils.orig_OnPurchase orig, CostTypeDef.PayCostContext context, int moneyCost)
    {
        orig(context, moneyCost);
		GameObject terminal = context.purchasedObject;
		if (!terminal)
			return;
		
		if (terminal.TryGetComponent(out ShopTerminalBehavior shopTerminalBehavior) && terminal.TryGetComponent(out PurchaseInteraction purchaseInteraction) && PlayerTeamHasEquipment(DLC1Content.Equipment.MultiShopCard.equipmentIndex))
		{
			if (shopTerminalBehavior.serverMultiShopController)
			{
				shopTerminalBehavior.serverMultiShopController.SetCloseOnTerminalPurchase(purchaseInteraction, doCloseMultiShop: false);
			}
		}
	}

    private static void RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
    {
		orig(self);
        if (!self.isPlayerControlled)
			return;
		checkCardAvailability();
	}

	public static void checkCardAvailability()
	{
		if (PlayerTeamHasEquipment(DLC1Content.Equipment.MultiShopCard.equipmentIndex))
		{
			foreach (MacroCardItemHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardItemHandler>())
			{
				cardHandler.tryRecoverShop();
			}

			foreach (MacroCardDroneHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardDroneHandler>())
			{
				cardHandler.tryRecoverShop();
			}
		} else
		{
			foreach (MacroCardItemHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardItemHandler>())
			{
				cardHandler.TryCloseShop();
			}

			foreach (MacroCardDroneHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardDroneHandler>())
			{
				cardHandler.TryCloseShop();
			}
		}
	}

	private static void PrePopulateSceneServer(SceneDirector director)
	{
		foreach (PurchaseInteraction purchaseInteraction in InstanceTracker.GetInstancesList<PurchaseInteraction>())
		{
			Transform parent = purchaseInteraction.transform.parent;
			if (!parent)
				continue;
			if (parent.TryGetComponent(out MultiShopController _))
			{
				parent.gameObject.AddComponent<MacroCardItemHandler>();
			}
			if (parent.TryGetComponent(out DroneVendorMultiShopController _))
			{
				parent.gameObject.AddComponent<MacroCardItemHandler>();
			}
		}
	}

	private static void UpdatePickupDisplayAndAnimations(On.RoR2.ShopTerminalBehavior.orig_UpdatePickupDisplayAndAnimations orig, ShopTerminalBehavior self)
	{
		orig(self);
		if (!self.hasStarted)
			return;
		if (self.animator && !self.hasBeenPurchased && self.pickup != UniquePickup.none)
		{
			int layer = self.animator.GetLayerIndex("Body");
			self.animator.PlayInFixedTime("Reopen", layer);
		}
	}

	static bool PlayerTeamHasEquipment(EquipmentIndex equipment)
	{
		foreach (PlayerCharacterMasterController instance in PlayerCharacterMasterController.instances)
		{
			if (instance && instance.isConnected && instance.master.hasBody && instance.master.GetBody().healthComponent && instance.master.GetBody().healthComponent.alive)
			{
				if (HasEquipment(instance.master.inventory, equipment))
					return true;
			}
		}
		return false;
	}

	static bool HasEquipment(Inventory inventory, EquipmentIndex equipment)
	{
		for (uint slot = 0; slot < inventory.GetEquipmentSlotCount(); slot++)
		{
			for (uint set = 0; set < inventory.GetEquipmentSetCount(); set++)
			{
				EquipmentState state = inventory.GetEquipment(new EquipmentLocation
				{
					slot = slot,
					set = set
				});

				EquipmentIndex heldEquipment = state.equipmentIndex;
				if (QualityCompat.enabled)
				{
					heldEquipment = QualityCompat.GetBaseEquipmentIndex(heldEquipment);
				}
				if (heldEquipment == equipment && (state.charges > 0 || state.chargeFinishTime.timeUntil < 0.5))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void DroneVendorAssumeIdle_OnExit(On.EntityStates.DroneVendor.DroneVendorAssumeIdle.orig_OnExit orig, EntityStates.DroneVendor.DroneVendorAssumeIdle self)
	{
		orig(self);
		if (PlayerTeamHasEquipment(DLC1Content.Equipment.MultiShopCard.equipmentIndex))
		{
			RetroactiveMacro.Instance.StartCoroutine(WaitToReenable());
		}

		IEnumerator WaitToReenable()
		{
			yield return new WaitForFixedUpdate();
			checkCardAvailability();
		}
	}
}

public class MacroCardItemHandler : MonoBehaviour
{
	public MultiShopController multiShopController;
	public UniquePickup[] savedPickups;
	public bool[] hidden;
	public bool recoveredShop;

	private void Awake()
	{
		multiShopController = GetComponent<MultiShopController>();
	}

	private void OnPurchase(CostTypeDef.PayCostContext arg0, CostTypeDef.PayCostResults arg1)
	{
		foreach (bool close in multiShopController.doCloseOnTerminalPurchase)
		{
			recoveredShop &= !close;
		}
	}

	private void Start()
	{
		if (!NetworkServer.active)
			return;
		savedPickups = new UniquePickup[multiShopController.terminalGameObjects.Length];
		hidden = new bool[multiShopController.terminalGameObjects.Length];
		for (int i = 0; i < multiShopController.terminalGameObjects.Length; i++)
		{
			GameObject terminal = multiShopController.terminalGameObjects[i];
			if (terminal.TryGetComponent(out ShopTerminalBehavior shopTerminalBehavior))
			{
				savedPickups[i] = shopTerminalBehavior.pickup;
				hidden[i] = shopTerminalBehavior.hidden;
			}
			if (terminal.TryGetComponent(out PurchaseInteraction purchaseInteraction))
			{
				purchaseInteraction.onDetailedPurchaseServer.AddListener(OnPurchase);
			}
		}
	}

	public void tryRecoverShop()
	{
		GameObject[] terminals = multiShopController.terminalGameObjects;
		for (int i = 0; i < terminals.Length; i++)
		{
			if (!terminals[i].TryGetComponent(out ShopTerminalBehavior shopTerminalBehavior))
				continue;
			if (!shopTerminalBehavior.animator)
				continue;
			if (shopTerminalBehavior.hasBeenPurchased)
				continue;
			if (terminals[i].GetComponent<PurchaseInteraction>().available)
				continue;
			recoveredShop = true;
			multiShopController.Networkavailable = true;
			terminals[i].GetComponent<PurchaseInteraction>().SetAvailable(true);
			Util.PlaySound("Play_UI_tripleChestShutter", shopTerminalBehavior.gameObject);

			if (NetworkServer.active)
			{
				shopTerminalBehavior.SetPickup(savedPickups[i], hidden[i]);
				shopTerminalBehavior.UpdatePickupDisplayAndAnimations();
			}
		}
	}

	public void TryCloseShop()
	{
		if (!NetworkServer.active)
			return;
		if (recoveredShop)
		{
			GameObject[] terminals = multiShopController.terminalGameObjects;
			for (int i = 0; i < terminals.Length; i++)
			{
				if (!terminals[i].TryGetComponent(out ShopTerminalBehavior shopTerminalBehavior))
					continue;
				if (!shopTerminalBehavior.animator)
					continue;
				if (shopTerminalBehavior.hasBeenPurchased)
					continue;
				multiShopController.Networkavailable = false;
				terminals[i].GetComponent<PurchaseInteraction>().SetAvailable(false);
				shopTerminalBehavior.SetNoPickup();
			}
		}
	}

	private void OnEnable()
	{
		InstanceTracker.Add(this);
	}

	private void OnDisable()
	{
		InstanceTracker.Remove(this);
	}
}

public class MacroCardDroneHandler : MonoBehaviour
{
	public DroneVendorMultiShopController multiShopController;
	public UniquePickup[] savedPickups;
	public bool recoveredShop;

	private void Awake()
	{
		multiShopController = GetComponent<DroneVendorMultiShopController>();
	}

	private void OnPurchase(CostTypeDef.PayCostContext arg0, CostTypeDef.PayCostResults arg1)
	{
		foreach (bool close in multiShopController.doCloseOnTerminalPurchase)
		{
			recoveredShop &= !close;
		}
	}

	private void Start()
	{
		if (!NetworkServer.active)
			return;
		savedPickups = new UniquePickup[multiShopController._terminals.Length];
		for (int i = 0; i < multiShopController._terminals.Length; i++)
		{
			DroneVendorTerminalBehavior terminal = multiShopController._terminals[i];
			savedPickups[i] = terminal.currentPickup;
			terminal.purchaseInteraction.onDetailedPurchaseServer.AddListener(OnPurchase);
		}
	}

	public void tryRecoverShop()
	{
		if (!NetworkServer.active)
			return;
		if (multiShopController.TryGetComponent(out EntityStateMachine entityStateMachine) &&
		entityStateMachine.state is not EntityStates.Idle)
			return;
		DroneVendorTerminalBehavior[] terminals = multiShopController._terminals;
		for (int i = 0; i < terminals.Length; i++)
		{
			if (terminals[i].hasBeenPurchased)
				continue;
			if (terminals[i].purchaseInteraction.available)
				continue;
			recoveredShop = true;
			if (terminals[i].GetComponent<EntityStateMachine>().state is EntityStates.Idle)
			{
				multiShopController.Networkavailable = true;
			}
			terminals[i].GetComponent<PurchaseInteraction>().SetAvailable(true);
			terminals[i].SetPickup(savedPickups[i]);
		}
	}

	public void TryCloseShop()
	{
		if (recoveredShop)
		{
			DroneVendorTerminalBehavior[] terminals = multiShopController._terminals;
			for (int i = 0; i < terminals.Length; i++)
			{
				if (terminals[i].hasBeenPurchased)
					continue;
				multiShopController.Networkavailable = false;
				terminals[i].purchaseInteraction.SetAvailable(false);
				terminals[i].SetNoPickup();
			}
		}
	}

	private void OnEnable()
	{
		InstanceTracker.Add(this);
	}

	private void OnDisable()
	{
		InstanceTracker.Remove(this);
	}
}