using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("RimworldStackXXL")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("RimworldStackXXL")]
[assembly: AssemblyCopyright("Copyright ©  2016")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("3efdc834-9423-4174-9129-86c62bea6bf3")]
[assembly: AssemblyFileVersion("1.7.0.2")]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: AssemblyVersion("1.7.0.2")]
namespace StackXXL;

public class StackXXLMod : Mod
{
	private readonly StackXXLSettings settings;

	private Vector2 scrollPosition;

	private float settingsHeight = 800f;

	private string sizeXLBuffer;

	private string sizeXXLBuffer;

	private bool stackSizesApplied;

	public StackXXLMod(ModContentPack content)
		: base(content)
	{
		settings = ((Mod)this).GetSettings<StackXXLSettings>();
		if (!settings.LoadedFromFile && settings.TryImportLegacySettings())
		{
			((Mod)this).WriteSettings();
		}
	}

	public override string SettingsCategory()
	{
		return ((Mod)this).Content.Name;
	}

	public override void WriteSettings()
	{
		settings.Validate();
		((Mod)this).WriteSettings();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_030c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_0326: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		Text.Font = (GameFont)1;
		string text = TaggedString.op_Implicit(Translator.Translate("StackXXL.RestartRequired"));
		float num = Text.CalcHeight(text, ((Rect)(ref inRect)).width);
		Rect val = default(Rect);
		((Rect)(ref val))..ctor(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).yMax - num, ((Rect)(ref inRect)).width, num);
		Rect val2 = new Rect(((Rect)(ref inRect)).x, ((Rect)(ref inRect)).y, ((Rect)(ref inRect)).width, Math.Max(0f, ((Rect)(ref val)).y - ((Rect)(ref inRect)).y - 8f));
		Rect val3 = default(Rect);
		((Rect)(ref val3))..ctor(0f, 0f, ((Rect)(ref inRect)).width - 20f, settingsHeight);
		Widgets.BeginScrollView(val2, ref scrollPosition, val3, true);
		Listing_Standard val4 = new Listing_Standard
		{
			maxOneColumn = true
		};
		((Listing)val4).Begin(val3);
		DrawMultiplier(val4, "StackXXL.XLSize", ref settings.SizeXL, ref sizeXLBuffer);
		DrawMultiplier(val4, "StackXXL.XXLSize", ref settings.SizeXXL, ref sizeXXLBuffer);
		val4.Label(Translator.Translate("StackXXL.Multiplier.Validation"), -1f, (string)null);
		((Listing)val4).GapLine(12f);
		DrawStackSetting(val4, "Resources", settings.ResourcesStack, delegate(StackSize value)
		{
			settings.ResourcesStack = value;
		});
		DrawStackSetting(val4, "Medicine", settings.MedicineStack, delegate(StackSize value)
		{
			settings.MedicineStack = value;
		});
		DrawStackSetting(val4, "Silver", settings.SilverStack, delegate(StackSize value)
		{
			settings.SilverStack = value;
		});
		DrawStackSetting(val4, "Textiles", settings.TextilesStack, delegate(StackSize value)
		{
			settings.TextilesStack = value;
		});
		DrawStackSetting(val4, "Drugs", settings.DrugsStack, delegate(StackSize value)
		{
			settings.DrugsStack = value;
		});
		DrawStackSetting(val4, "Meat", settings.MeatStack, delegate(StackSize value)
		{
			settings.MeatStack = value;
		});
		DrawStackSetting(val4, "Raw", settings.RawStack, delegate(StackSize value)
		{
			settings.RawStack = value;
		});
		DrawStackSetting(val4, "Meals", settings.MealsStack, delegate(StackSize value)
		{
			settings.MealsStack = value;
		});
		DrawStackSetting(val4, "BodyParts", settings.BodyPartsStack, delegate(StackSize value)
		{
			settings.BodyPartsStack = value;
		});
		DrawStackSetting(val4, "Others.Stackable", settings.OthersStackableStack, delegate(StackSize value)
		{
			settings.OthersStackableStack = value;
		});
		DrawStackSetting(val4, "Others.Single", settings.OthersSingleStack, delegate(StackSize value)
		{
			settings.OthersSingleStack = value;
		});
		((Listing)val4).GapLine(12f);
		val4.CheckboxLabeled(TaggedString.op_Implicit(Translator.Translate("StackXXL.DebugMode.Title")), ref settings.DebugMode, TaggedString.op_Implicit(Translator.Translate("StackXXL.DebugMode.Desc")), 0f, 1f);
		settingsHeight = ((Listing)val4).CurHeight + 12f;
		((Listing)val4).End();
		Widgets.EndScrollView();
		Color color = GUI.color;
		TextAnchor anchor = Text.Anchor;
		GUI.color = Color.red;
		Text.Anchor = (TextAnchor)4;
		Widgets.Label(val, text);
		Text.Anchor = anchor;
		GUI.color = color;
		Text.Font = font;
	}

	private static void DrawMultiplier(Listing_Standard listing, string key, ref double value, ref string buffer)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		string text = TaggedString.op_Implicit(Translator.Translate(key + ".Title"));
		Rect rect = ((Listing)listing).GetRect(Math.Max(30f, Text.CalcHeight(text, ((Listing)listing).ColumnWidth - 140f)), 1f);
		Widgets.Label(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width - 140f, ((Rect)(ref rect)).height), text);
		TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(Translator.Translate(key + ".Desc")));
		if (buffer == null)
		{
			buffer = value.ToString(CultureInfo.CurrentCulture);
		}
		Color color = GUI.color;
		if (!StackXXLSettings.TryParseMultiplier(buffer, out var value2))
		{
			GUI.color = Color.red;
		}
		buffer = Widgets.TextField(new Rect(((Rect)(ref rect)).xMax - 130f, ((Rect)(ref rect)).y, 130f, 30f), buffer);
		GUI.color = color;
		if (StackXXLSettings.TryParseMultiplier(buffer, out value2))
		{
			value = value2;
		}
		((Listing)listing).Gap(6f);
	}

	private static void DrawStackSetting(Listing_Standard listing, string category, StackSize value, Action<StackSize> setter)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Expected O, but got Unknown
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Expected O, but got Unknown
		string text = "StackXXL.Stack." + category;
		string text2 = TaggedString.op_Implicit(Translator.Translate(text + ".Title"));
		Rect rect = ((Listing)listing).GetRect(Math.Max(30f, Text.CalcHeight(text2, ((Listing)listing).ColumnWidth - 140f)), 1f);
		Widgets.Label(new Rect(((Rect)(ref rect)).x, ((Rect)(ref rect)).y, ((Rect)(ref rect)).width - 140f, ((Rect)(ref rect)).height), text2);
		TooltipHandler.TipRegion(rect, TipSignal.op_Implicit(Translator.Translate(text + ".Desc")));
		if (Widgets.ButtonText(new Rect(((Rect)(ref rect)).xMax - 130f, ((Rect)(ref rect)).y, 130f, 30f), TaggedString.op_Implicit(Translator.Translate("StackXXL.Size." + value)), true, true, true, (TextAnchor?)null))
		{
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			foreach (StackSize value2 in Enum.GetValues(typeof(StackSize)))
			{
				StackSize selected = value2;
				list.Add(new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("StackXXL.Size." + value2)), (Action)delegate
				{
					setter(selected);
				}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0));
			}
			Find.WindowStack.Add((Window)new FloatMenu(list));
		}
		((Listing)listing).Gap(6f);
	}

	private static bool StackIncreaseAllowed(ThingDef d)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		if (GenList.NullOrEmpty<ThingCategoryDef>((IList<ThingCategoryDef>)d.thingCategories))
		{
			return false;
		}
		if (!d.IsStuff && !d.isTechHediff)
		{
			if ((int)d.category == 2 && !d.isUnfinishedThing && !d.IsCorpse && !d.destroyOnDrop && !d.IsRangedWeapon && !d.IsApparel)
			{
				return d.stackLimit > 1;
			}
			return false;
		}
		return true;
	}

	private static bool IsResources(ThingCategoryDef category)
	{
		if (category != ThingCategoryDefOf.Manufactured && category != ThingCategoryDefOf.ResourcesRaw)
		{
			return category == ThingCategoryDefOf.StoneBlocks;
		}
		return true;
	}

	private static bool IsTextiles(ThingCategoryDef category)
	{
		if (category != ThingCategoryDefOf.Leathers)
		{
			return ((Def)category).defName == "Textiles";
		}
		return true;
	}

	private static bool IsRaw(ThingCategoryDef category)
	{
		if (!((Def)category).defName.EndsWith("FoodRaw") && !(((Def)category).defName == "PlantMatter") && !(((Def)category).defName == "AnimalProductRaw") && !(((Def)category).defName == "AnimalFeed") && !((Def)category).defName.StartsWith("Eggs"))
		{
			return ((Def)category).defName == "CookingSupplies";
		}
		return true;
	}

	private static bool IsMeals(ThingCategoryDef category)
	{
		if (!((Def)category).defName.EndsWith("Meals"))
		{
			return ((Def)category).defName == "Foods";
		}
		return true;
	}

	private static bool IsBodyParts(ThingCategoryDef category)
	{
		if (!((Def)category).defName.EndsWith("Prostheses") && !((Def)category).defName.StartsWith("BodyParts"))
		{
			return ((Def)category).defName.EndsWith("Organs");
		}
		return true;
	}

	private double GetSize(StackSize size)
	{
		return size switch
		{
			StackSize.XL => settings.SizeXL, 
			StackSize.XXL => settings.SizeXXL, 
			_ => 1.0, 
		};
	}

	private void UpdateStackLimit(ThingDef thing, StackSize size, string category)
	{
		if (settings.DebugMode)
		{
			Log.Message("[Stack XXL] " + ((Def)thing).defName + " " + ((Def)thing.thingCategories[0]).defName + " " + category);
		}
		double val = Math.Round((double)thing.stackLimit * GetSize(size));
		thing.stackLimit = (int)Math.Min(2147483647.0, Math.Max(1.0, val));
	}

	internal void ApplyStackSizes()
	{
		if (stackSizesApplied)
		{
			return;
		}
		settings.Validate();
		foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
		{
			if (StackIncreaseAllowed(allDef))
			{
				ThingCategoryDef val = allDef.thingCategories[0];
				if (allDef == ThingDefOf.Silver)
				{
					UpdateStackLimit(allDef, settings.SilverStack, "silver");
				}
				else if (val == ThingCategoryDefOf.Medicine)
				{
					UpdateStackLimit(allDef, settings.MedicineStack, "medicine");
				}
				else if (IsResources(val))
				{
					UpdateStackLimit(allDef, settings.ResourcesStack, "resources");
				}
				else if (IsTextiles(val))
				{
					UpdateStackLimit(allDef, settings.TextilesStack, "textiles");
				}
				else if (((Def)val).defName == "Drugs")
				{
					UpdateStackLimit(allDef, settings.DrugsStack, "drugs");
				}
				else if (val == ThingCategoryDefOf.MeatRaw)
				{
					UpdateStackLimit(allDef, settings.MeatStack, "meat");
				}
				else if (IsRaw(val))
				{
					UpdateStackLimit(allDef, settings.RawStack, "raw");
				}
				else if (IsMeals(val))
				{
					UpdateStackLimit(allDef, settings.MealsStack, "meals");
				}
				else if (IsBodyParts(val))
				{
					UpdateStackLimit(allDef, settings.BodyPartsStack, "bodyParts");
				}
				else if (allDef.stackLimit > 1)
				{
					UpdateStackLimit(allDef, settings.OthersStackableStack, "othersStackable");
				}
				else
				{
					UpdateStackLimit(allDef, settings.OthersSingleStack, "othersSingle");
				}
			}
		}
		stackSizesApplied = true;
		Log.Message("[Stack XXL] Loaded");
	}
}
[StaticConstructorOnStartup]
internal static class StackXXLStartup
{
	static StackXXLStartup()
	{
		LongEventHandler.ExecuteWhenFinished((Action)delegate
		{
			LoadedModManager.GetMod<StackXXLMod>().ApplyStackSizes();
		});
	}
}
public enum StackSize
{
	Default,
	XL,
	XXL
}
public class StackXXLSettings : ModSettings
{
	public double SizeXL = 10.0;

	public double SizeXXL = 20.0;

	public StackSize ResourcesStack = StackSize.XL;

	public StackSize MedicineStack = StackSize.XL;

	public StackSize SilverStack = StackSize.XL;

	public StackSize TextilesStack = StackSize.XL;

	public StackSize DrugsStack = StackSize.XL;

	public StackSize MeatStack = StackSize.XL;

	public StackSize RawStack = StackSize.XL;

	public StackSize MealsStack = StackSize.XL;

	public StackSize BodyPartsStack;

	public StackSize OthersStackableStack = StackSize.XL;

	public StackSize OthersSingleStack;

	public bool DebugMode;

	internal bool LoadedFromFile { get; private set; }

	public override void ExposeData()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Invalid comparison between Unknown and I4
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Invalid comparison between Unknown and I4
		((ModSettings)this).ExposeData();
		if ((int)Scribe.mode == 2)
		{
			LoadedFromFile = true;
		}
		Scribe_Values.Look<double>(ref SizeXL, "sizeXL", 10.0, false);
		Scribe_Values.Look<double>(ref SizeXXL, "sizeXXL", 20.0, false);
		Scribe_Values.Look<StackSize>(ref ResourcesStack, "resourcesStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref MedicineStack, "medicineStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref SilverStack, "silverStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref TextilesStack, "textilesStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref DrugsStack, "drugsStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref MeatStack, "meatStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref RawStack, "rawStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref MealsStack, "mealsStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref BodyPartsStack, "bodyPartsStack", StackSize.Default, false);
		Scribe_Values.Look<StackSize>(ref OthersStackableStack, "othersStackableStack", StackSize.XL, false);
		Scribe_Values.Look<StackSize>(ref OthersSingleStack, "othersSingleStack", StackSize.Default, false);
		Scribe_Values.Look<bool>(ref DebugMode, "debugMode", false, false);
		if ((int)Scribe.mode == 4)
		{
			Validate();
		}
	}

	internal void Validate()
	{
		if (!IsValidMultiplier(SizeXL))
		{
			SizeXL = 10.0;
		}
		if (!IsValidMultiplier(SizeXXL))
		{
			SizeXXL = 20.0;
		}
		ResourcesStack = ValidSize(ResourcesStack, StackSize.XL);
		MedicineStack = ValidSize(MedicineStack, StackSize.XL);
		SilverStack = ValidSize(SilverStack, StackSize.XL);
		TextilesStack = ValidSize(TextilesStack, StackSize.XL);
		DrugsStack = ValidSize(DrugsStack, StackSize.XL);
		MeatStack = ValidSize(MeatStack, StackSize.XL);
		RawStack = ValidSize(RawStack, StackSize.XL);
		MealsStack = ValidSize(MealsStack, StackSize.XL);
		BodyPartsStack = ValidSize(BodyPartsStack, StackSize.Default);
		OthersStackableStack = ValidSize(OthersStackableStack, StackSize.XL);
		OthersSingleStack = ValidSize(OthersSingleStack, StackSize.Default);
	}

	private static StackSize ValidSize(StackSize size, StackSize fallback)
	{
		if (!Enum.IsDefined(typeof(StackSize), size))
		{
			return fallback;
		}
		return size;
	}

	private static bool IsValidMultiplier(double value)
	{
		if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= 1.0)
		{
			return value <= 3.4028234663852886E+38;
		}
		return false;
	}

	internal static bool TryParseMultiplier(string text, out double value)
	{
		bool flag = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		if (!flag && text != null && text.IndexOf(',') >= 0 && text.IndexOf('.') < 0)
		{
			flag = double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}
		if (flag)
		{
			return IsValidMultiplier(value);
		}
		return false;
	}

	internal bool TryImportLegacySettings()
	{
		string text = Path.Combine(GenFilePaths.SaveDataFolderPath, "HugsLib", "ModSettings.xml");
		if (!File.Exists(text))
		{
			return false;
		}
		try
		{
			XmlDocument xmlDocument = new XmlDocument
			{
				XmlResolver = null
			};
			XmlReaderSettings settings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null
			};
			using (XmlReader reader = XmlReader.Create(text, settings))
			{
				xmlDocument.Load(reader);
			}
			XmlNode xmlNode = xmlDocument.SelectSingleNode("/settings/StackXXL");
			if (xmlNode == null)
			{
				return false;
			}
			SizeXL = ReadMultiplier(xmlNode, "sizeXL", SizeXL);
			SizeXXL = ReadMultiplier(xmlNode, "sizeXXL", SizeXXL);
			ResourcesStack = ReadSize(xmlNode, "resourcesStack", ResourcesStack);
			MedicineStack = ReadSize(xmlNode, "medicineStack", ResourcesStack);
			SilverStack = ReadSize(xmlNode, "silverStack", ResourcesStack);
			TextilesStack = ReadSize(xmlNode, "textilesStack", TextilesStack);
			DrugsStack = ReadSize(xmlNode, "drugsStack", DrugsStack);
			MeatStack = ReadSize(xmlNode, "meatStack", MeatStack);
			RawStack = ReadSize(xmlNode, "rawStack", RawStack);
			MealsStack = ReadSize(xmlNode, "mealsStack", MealsStack);
			BodyPartsStack = ReadSize(xmlNode, "bodyPartsStack", BodyPartsStack);
			OthersStackableStack = ReadSize(xmlNode, "othersStackableStack", OthersStackableStack);
			OthersSingleStack = ReadSize(xmlNode, "othersSingleStack", OthersSingleStack);
			if (bool.TryParse(xmlNode["debugMode"]?.InnerText, out var result))
			{
				DebugMode = result;
			}
			Validate();
			Log.Message("[Stack XXL] Imported existing settings into RimWorld's mod settings.");
			return true;
		}
		catch (Exception ex)
		{
			Log.Warning("[Stack XXL] Could not import legacy settings: " + ex.Message);
			return false;
		}
	}

	private static double ReadMultiplier(XmlNode node, string key, double fallback)
	{
		if (!TryParseMultiplier(node[key]?.InnerText, out var value))
		{
			return fallback;
		}
		return value;
	}

	private static StackSize ReadSize(XmlNode node, string key, StackSize fallback)
	{
		if (!Enum.TryParse<StackSize>(node[key]?.InnerText, out var result))
		{
			return fallback;
		}
		return ValidSize(result, fallback);
	}
}
