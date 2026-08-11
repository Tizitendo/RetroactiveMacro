using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.Options;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RetroactiveMacro;

static class Options
{
	public static bool IsEnabled => Chainloader.PluginInfos.ContainsKey(RiskOfOptions.PluginInfo.PLUGIN_GUID);

	public static void Init()
	{
		RetroactiveMacro.ChangeSaleStar = RetroactiveMacro.Instance.Config.Bind("Sale Star", "Change Sale Star", true, "Reopen chests instead");
		RetroactiveMacro.ExcludeSaleStarChest = RetroactiveMacro.Instance.Config.Bind("Sale Star", "Exclude Sale Star Chests", true, "Chests that drop a Sale Star cannot be reopened");
		RetroactiveMacro.ChangeCard = RetroactiveMacro.Instance.Config.Bind("Executive Card", "Change Credit Card", true, "While holding the card, reopen all closed multishops");
		RetroactiveMacro.ExcludeEquipShops = RetroactiveMacro.Instance.Config.Bind("Executive Card", "Exclude Equipment Shops", true, "Exclude equipment tri shops from all card changes");
		
		if (Options.IsEnabled)
		{
			RiskOfOptionsConfig();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static void RiskOfOptionsConfig() {
		const string MOD_GUID = RetroactiveMacro.PluginGUID;
		const string MOD_NAME = RetroactiveMacro.PluginName;

		ModSettingsManager.AddOption(new CheckBoxOption(RetroactiveMacro.ChangeSaleStar, true), MOD_GUID, MOD_NAME);
		ModSettingsManager.AddOption(new CheckBoxOption(RetroactiveMacro.ExcludeSaleStarChest, true), MOD_GUID, MOD_NAME);
		ModSettingsManager.AddOption(new CheckBoxOption(RetroactiveMacro.ChangeCard), MOD_GUID, MOD_NAME);
		ModSettingsManager.AddOption(new CheckBoxOption(RetroactiveMacro.ExcludeEquipShops), MOD_GUID, MOD_NAME);

		ModSettingsManager.SetModDescription($"Options for {MOD_NAME}", MOD_GUID, MOD_NAME);

		FileInfo iconFile = null;
		DirectoryInfo dir = new DirectoryInfo(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
		do
		{
			FileInfo[] files = dir.GetFiles("icon.png", SearchOption.TopDirectoryOnly);
			if (files != null && files.Length > 0)
			{
				iconFile = files[0];
				break;
			}

			dir = dir.Parent;
		} while (dir != null && dir.Exists && !string.Equals(dir.Name, "plugins", StringComparison.OrdinalIgnoreCase));

		if (iconFile != null)
		{
			Texture2D iconTexture = new Texture2D(256, 256);
			if (iconTexture.LoadImage(File.ReadAllBytes(iconFile.FullName)))
			{
				Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0f, 0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
				iconSprite.name = $"{MOD_NAME}Icon";

				ModSettingsManager.SetModIcon(iconSprite, MOD_GUID, MOD_NAME);
			}
		}
	}
}