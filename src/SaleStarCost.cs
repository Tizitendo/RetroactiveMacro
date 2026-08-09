using RoR2;
using Logger;

namespace RetroactiveMacro;

public static class SaleStarCost
{
	static readonly CostTypeDef _saleStarCost = new CostTypeDef
	{
		name = "SaleStarCost",
		colorIndex = ColorCatalog.ColorIndex.Tier2Item,
		itemTier = ItemTier.Tier2,
		costStringFormatToken = "ITEM_LOWERPRICEDCHESTS_NAME",
		isAffordable = IsAffordableSaleStar,
		payCost = (context, result) => { }
	};

	public static CostTypeIndex SaleStar { get; private set; } = CostTypeIndex.None;

	[SystemInitializer]
	static void Init()
	{
		CostTypeCatalog.modHelper.getAdditionalEntries += (entries) =>
		{
			entries.Add(_saleStarCost);
		};
	}

	[SystemInitializer(typeof(CostTypeCatalog))]
	static void InitCostType()
	{
		for (CostTypeIndex costTypeIndex = 0; (int)costTypeIndex < CostTypeCatalog.costTypeCount; costTypeIndex++)
		{
			CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(costTypeIndex);
			if (costTypeDef == _saleStarCost)
			{
				SaleStar = costTypeIndex;
			}
		}
	}

	static bool IsAffordableSaleStar(CostTypeDef costTypeDef, CostTypeDef.IsAffordableContext context)
	{
		CharacterBody activatorBody = context.activator ? context.activator.GetComponent<CharacterBody>() : null;
		Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;
		
		return activatorInventory && activatorInventory.GetItemCountEffective(DLC2Content.Items.LowerPricedChests) > 0;
	}
}