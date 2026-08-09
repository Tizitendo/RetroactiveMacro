using Logger;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.Networking;

namespace RetroactiveMacro;

public static class Card
{
	[SystemInitializer]
	static void Init()
	{
		if (!RetroactiveMacro.ChangeCard.Value)
			return;
		On.RoR2.Inventory.SetEquipmentInternal_EquipmentState_uint_uint += SetEquipment;
		On.EntityStates.GenericCharacterDeath.OnEnter += GenericCharacterDeath_OnEnter;
		On.RoR2.ShopTerminalBehavior.UpdatePickupDisplayAndAnimations += UpdatePickupDisplayAndAnimations;
		On.RoR2.CharacterMaster.TryReviveOnBodyDeath += TryReviveOnBodyDeath;
		SceneDirector.onPrePopulateSceneServer += PrePopulateSceneServer;

		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_TripleShop.TripleShop_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardItemHandler>();
		};
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_TripleShopEquipment.TripleShopEquipment_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardItemHandler>();
		};
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_Base_TripleShopLarge.TripleShopLarge_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardItemHandler>();
		};
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC1_FreeChestMultiShop.FreeChestMultiShop_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardItemHandler>();
		};
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC3_TripleDroneShop.TripleDroneShop_prefab)).Completed += (x) =>
		{
			x.Result.AddComponent<MacroCardDroneHandler>();
		};
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

	private static bool TryReviveOnBodyDeath(On.RoR2.CharacterMaster.orig_TryReviveOnBodyDeath orig, CharacterMaster self, CharacterBody body)
	{
		bool result = orig(self, body);
		if (result && PlayerTeamHasEquipment(DLC1Content.Equipment.MultiShopCard.equipmentIndex))
		{
			HasCard();
		}
		return result;
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

	private static bool SetEquipment(On.RoR2.Inventory.orig_SetEquipmentInternal_EquipmentState_uint_uint orig, Inventory self, EquipmentState equipmentState, uint slot, uint set)
	{
		bool result = orig(self, equipmentState, slot, set);
		if (PlayerTeamHasEquipment(DLC1Content.Equipment.MultiShopCard.equipmentIndex))
		{
			HasCard();
		}
		else
		{
			HasNoCard();
		}
		return result;
	}

	private static void GenericCharacterDeath_OnEnter(On.EntityStates.GenericCharacterDeath.orig_OnEnter orig, EntityStates.GenericCharacterDeath self)
	{
		orig(self);
		if (self.isPlayerDeath && !PlayerTeamHasEquipment(DLC1Content.Equipment.MultiShopCard.equipmentIndex))
		{
			HasNoCard();
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
			for (uint set = 0; set < inventory.GetEquipmentSetCount(slot); set++)
			{
				EquipmentState state = inventory.GetEquipment(slot, set);
				if (state.equipmentIndex == equipment && state.charges > 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void HasNoCard()
	{
		foreach (MacroCardItemHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardItemHandler>())
		{
			if (cardHandler.recoveredShop)
			{
				GameObject[] terminals = cardHandler.multiShopController.terminalGameObjects;
				for (int i = 0; i < terminals.Length; i++)
				{
					if (!terminals[i].TryGetComponent(out ShopTerminalBehavior shopTerminalBehavior))
						continue;
					if (!shopTerminalBehavior.animator)
						continue;
					if (shopTerminalBehavior.hasBeenPurchased)
						continue;
					cardHandler.multiShopController.available = false;
					terminals[i].GetComponent<PurchaseInteraction>().available = false;
					shopTerminalBehavior.SetNoPickup();
				}
			}
		}

		foreach (MacroCardDroneHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardDroneHandler>())
		{
			if (cardHandler.recoveredShop)
			{
				DroneVendorTerminalBehavior[] terminals = cardHandler.multiShopController._terminals;
				for (int i = 0; i < terminals.Length; i++)
				{
					if (terminals[i].hasBeenPurchased)
						continue;
					cardHandler.multiShopController.available = false;
					terminals[i].purchaseInteraction.available = false;
					terminals[i].SetNoPickup();
				}
			}
		}
	}

	private static void HasCard()
	{
		foreach (MacroCardItemHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardItemHandler>())
		{
			GameObject[] terminals = cardHandler.multiShopController.terminalGameObjects;
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
				cardHandler.recoveredShop = true;
				cardHandler.multiShopController.available = true;
				terminals[i].GetComponent<PurchaseInteraction>().available = true;
				Util.PlaySound("Play_UI_tripleChestShutter", shopTerminalBehavior.gameObject);

				if (NetworkServer.active)
				{
					shopTerminalBehavior.SetPickup(cardHandler.savedPickups[i], cardHandler.hidden[i]);
					shopTerminalBehavior.UpdatePickupDisplayAndAnimations();
				}
			}
		}

		foreach (MacroCardDroneHandler cardHandler in InstanceTracker.GetInstancesList<MacroCardDroneHandler>())
		{
			DroneVendorTerminalBehavior[] terminals = cardHandler.multiShopController._terminals;
			for (int i = 0; i < terminals.Length; i++)
			{
				if (terminals[i].hasBeenPurchased)
					continue;
				if (terminals[i].purchaseInteraction.available)
					continue;
				cardHandler.recoveredShop = true;
				if (terminals[i].GetComponent<EntityStateMachine>().state is EntityStates.Idle)
				{
					cardHandler.multiShopController.available = true;
				}
				terminals[i].GetComponent<PurchaseInteraction>().available = true;
				terminals[i].SetPickup(cardHandler.savedPickups[i]);
			}
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
		savedPickups = new UniquePickup[multiShopController._terminals.Length];
		for (int i = 0; i < multiShopController._terminals.Length; i++)
		{
			DroneVendorTerminalBehavior terminal = multiShopController._terminals[i];
			savedPickups[i] = terminal.currentPickup;
			terminal.purchaseInteraction.onDetailedPurchaseServer.AddListener(OnPurchase);
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