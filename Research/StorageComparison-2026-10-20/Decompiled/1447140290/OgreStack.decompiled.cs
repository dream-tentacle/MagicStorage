using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OgreStack.PersistentData;
using OgreStack.Support;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyCompany("OgreStack")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0+1084c3ed9c98b7141dad488f949b1340c16244a9")]
[assembly: AssemblyProduct("OgreStack")]
[assembly: AssemblyTitle("OgreStack")]
[assembly: AssemblyVersion("1.0.0.0")]
[module: RefSafetyRules(11)]
namespace OgreStack
{
	internal class IndividualOverrides
	{
		private XDocument _overrides = null;

		private static Dictionary<string, int> _itemLevelOverrides = null;

		private static HashSet<string> _categoryNoStackOverrides = null;

		private static List<KeyValuePair<string, int>> _itemLevelOverridesOrdered = null;

		private static readonly object _lock = new object();

		internal IndividualOverrides()
		{
			lock (_lock)
			{
				if (_itemLevelOverrides != null && _categoryNoStackOverrides != null)
				{
					return;
				}
				_overrides = DataUtil.GetUserOverrides();
				if (_overrides == null)
				{
					return;
				}
				_itemLevelOverrides = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				_categoryNoStackOverrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				_itemLevelOverridesOrdered = new List<KeyValuePair<string, int>>();
				XElement xElement = _overrides.Root.Element("IndividualItemOverrides");
				if (xElement != null)
				{
					IEnumerable<XElement> enumerable = xElement.Elements("item");
					if (enumerable != null && enumerable.Any())
					{
						foreach (XElement item in enumerable)
						{
							addItemLevelOverride(item);
						}
					}
				}
				XElement xElement2 = _overrides.Root.Element("BanItemsFromStackingByCategory");
				if (xElement2 == null)
				{
					return;
				}
				IEnumerable<XElement> enumerable2 = xElement2.Elements("category");
				if (enumerable2 == null || !enumerable2.Any())
				{
					return;
				}
				foreach (XElement item2 in enumerable2)
				{
					Dictionary<string, string> attributePairs = getAttributePairs(item2);
					string value = string.Empty;
					if (attributePairs.TryGetValue("name", out value))
					{
						value = value.Trim();
						if (!string.IsNullOrEmpty(value))
						{
							_categoryNoStackOverrides.Add(value);
						}
					}
				}
			}
		}

		private static Dictionary<string, string> getAttributePairs(XElement element)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (element.HasAttributes)
			{
				foreach (XAttribute item in element.Attributes())
				{
					dictionary.Add(item.Name.ToString(), item.Value);
				}
			}
			return dictionary;
		}

		private void addItemLevelOverride(XElement def)
		{
			Dictionary<string, string> attributePairs = getAttributePairs(def);
			string value = string.Empty;
			string value2 = string.Empty;
			if (!attributePairs.TryGetValue("defName", out value) && !attributePairs.TryGetValue("name", out value))
			{
				return;
			}
			value = value.Trim();
			if (!attributePairs.TryGetValue("stackLimit", out value2))
			{
				return;
			}
			int result = -1;
			if (!int.TryParse(value2, out result) || result <= 0 || string.IsNullOrEmpty(value))
			{
				return;
			}
			if (_itemLevelOverrides.ContainsKey(value))
			{
				_itemLevelOverrides.Remove(value);
				for (int num = _itemLevelOverridesOrdered.Count - 1; num > -1; num--)
				{
					if (string.Compare(_itemLevelOverridesOrdered[num].Key, value, ignoreCase: true) == 0)
					{
						_itemLevelOverridesOrdered.RemoveAt(num);
					}
				}
			}
			_itemLevelOverrides.Add(value, result);
			_itemLevelOverridesOrdered.Add(new KeyValuePair<string, int>(value, result));
		}

		internal List<KeyValuePair<string, int>> ViewInternalOverrides()
		{
			List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
			if (_itemLevelOverridesOrdered != null && _itemLevelOverridesOrdered.Count > 0)
			{
				foreach (KeyValuePair<string, int> item in _itemLevelOverridesOrdered)
				{
					list.Add(new KeyValuePair<string, int>(item.Key, item.Value));
				}
			}
			return list;
		}

		internal int GetItemLevelOverride(string defName)
		{
			int value = 0;
			if (_itemLevelOverrides != null)
			{
				return _itemLevelOverrides.TryGetValue(defName, out value) ? value : 0;
			}
			return value;
		}

		internal bool IsCategoryBanned(string category)
		{
			if (string.IsNullOrEmpty(category))
			{
				return false;
			}
			if (_categoryNoStackOverrides == null)
			{
				return false;
			}
			return Enumerable.Contains(_categoryNoStackOverrides, category);
		}

		internal static string GenerateDefaultOverridesXml()
		{
			return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<OgreStack>\r\n\t<IndividualItemOverrides>\r\n\t\t<!--\r\n\t\t\t* This lets you target items by their DefName.\r\n\t\t\t* Set the stackLimit directly\r\n\t\t\t\r\n\t\t\t* Items adjusted here will ignore any additional\r\n\t\t\t* processing rules in OgreStack\r\n\t\t\t\r\n\t\t\t* FORMAT: <item defName=\"<string:DefName>\" stackLimit=\"<int:desiredStackLimit>\" />\r\n\t\t\t* Example: set the stack limit for unfertilized chicken eggs to 2000\r\n\t\t\t\r\n\t\t\t* <item defName=\"EggChickenUnfertilized\" stackLimit=\"2000\" />\r\n\t\t-->\r\n\r\n\t\t<item defName=\"AIPersonaCore\" stackLimit=\"1\" />\r\n\t\t<item defName=\"TechprofSubpersonaCore\" stackLimit=\"1\" />\r\n\t</IndividualItemOverrides>\r\n\r\n\t<BanItemsFromStackingByCategory>\r\n\t\t<!--\r\n\t\t * Lets you ban items that identifies itself\r\n\t\t * in a category named in this section\r\n\t\t \r\n\t\t * Items adjusted here will ignore additional\r\n\t\t * processing rules in OgreStack\r\n\t\t \r\n\t\t * FORMAT: <category name=\"<string:Category>\" />\r\n\t\t * Example: Ban stacking for any egg that is fertilized \r\n\t\t * that categorizes itself 'EggsFertilized'\r\n\t\t \r\n\t\t * <category name=\"EggsFertilized\" />\r\n\t\t-->\r\n\t</BanItemsFromStackingByCategory>\r\n</OgreStack>";
		}
	}
	public enum Category
	{
		SmallVolumeResource,
		Resource,
		RawFoodMeat,
		RawFoodPlant,
		PlantMatter,
		Meal,
		FoodForAnimal,
		Food,
		Item,
		BodyPartOrImplant,
		Leather,
		Textile,
		StoneBlock,
		Manufactured,
		Drug,
		Medicine,
		MortarShell,
		Artifact,
		Other
	}
	public class CategorySetting
	{
		public MultiplierMode Mode;

		public float Value;

		public string Buffer;

		private static readonly Dictionary<string, int> _baseCounts = new Dictionary<string, int>();

		public CategorySetting(MultiplierMode mode)
		{
			Mode = mode;
		}

		public CategorySetting(MultiplierMode mode, float value)
		{
			Mode = mode;
			Value = value;
			Buffer = value.ToString();
		}

		public CategorySetting(MultiplierMode mode, string buffer)
		{
			Mode = mode;
			Buffer = buffer;
			Value = (float.TryParse(buffer, out Value) ? Value : 0f);
		}

		public void Modify(MultiplierMode mode, float value, string buffer)
		{
			Mode = mode;
			Value = value;
			Buffer = buffer;
		}

		public bool ParseBuffer()
		{
			if (float.TryParse(Buffer, out Value))
			{
				return true;
			}
			Value = 0f;
			return false;
		}

		public static int GetBaseStackLimit(ThingDef def)
		{
			if (def == null)
			{
				Log.Message("def should not be null. returning 1 for stacklimit.");
				return 1;
			}
			int value = 1;
			if (!_baseCounts.TryGetValue(((Def)def).defName, out value))
			{
				_baseCounts.Add(((Def)def).defName, def.stackLimit);
				value = def.stackLimit;
			}
			return value;
		}

		public void ProcessThing(ThingDef def, Dictionary<string, List<Thing>> activeThings)
		{
			//IL_0116: Unknown result type (might be due to invalid IL or missing references)
			//IL_011b: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
			int baseStackLimit = GetBaseStackLimit(def);
			int num = 1;
			if (Mode == MultiplierMode.Fixed)
			{
				num = Math.Max(1, (int)Math.Floor(Value));
			}
			else
			{
				int num2 = Math.Max(1, baseStackLimit);
				num = Math.Max(1, (int)Math.Floor((float)num2 * Value));
			}
			def.stackLimit = num;
			if (activeThings == null)
			{
				return;
			}
			List<Thing> value = null;
			if (!activeThings.TryGetValue(((Def)def).defName, out value))
			{
				return;
			}
			foreach (Thing item in value)
			{
				if (item == null || item.def == null || item.stackCount <= num)
				{
					continue;
				}
				ThingDef val = ((item.Stuff == null) ? null : item.Stuff);
				bool flag = false;
				if (item is ThingWithComps)
				{
					CompForbiddable comp = ((ThingWithComps)((item is ThingWithComps) ? item : null)).GetComp<CompForbiddable>();
					if (comp != null)
					{
						flag = comp.Forbidden;
					}
				}
				if (item.Position != IntVec3.Invalid && item.Map != null)
				{
					for (int num3 = item.stackCount - num; num3 > 0; num3 -= num)
					{
						Thing val2 = ((val == null) ? ThingMaker.MakeThing(item.def, (ThingDef)null) : ThingMaker.MakeThing(item.def, val));
						val2.stackCount = Math.Min(num3, num);
						val2.HitPoints = Math.Min(item.HitPoints, val2.MaxHitPoints);
						if (flag)
						{
							ThingWithComps val3 = (ThingWithComps)(object)((val2 is ThingWithComps) ? val2 : null);
							CompForbiddable comp2 = val3.GetComp<CompForbiddable>();
							if (comp2 != null)
							{
								comp2.Forbidden = true;
							}
						}
						GenPlace.TryPlaceThing(val2, item.Position, item.Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);
					}
				}
				item.stackCount = num;
			}
		}

		public override string ToString()
		{
			return string.Format("({0}{1})", (Mode == MultiplierMode.Fixed) ? "F" : "x", Value);
		}
	}
	public enum MultiplierMode
	{
		Fixed,
		Scalar
	}
	public class Preset
	{
		public string NameKey { get; private set; }

		public Action<OgreStackSettings> Modify { get; set; }

		public Preset(string key)
		{
			NameKey = key;
		}
	}
	public class OgreStackMod : Mod
	{
		public OgreStackSettings settings;

		private Dictionary<string, List<Thing>> _activeThings = null;

		private static readonly List<Category> _PROCESSING_ORDER = new List<Category>
		{
			Category.SmallVolumeResource,
			Category.Resource,
			Category.RawFoodMeat,
			Category.RawFoodPlant,
			Category.PlantMatter,
			Category.Meal,
			Category.FoodForAnimal,
			Category.Food,
			Category.Item,
			Category.BodyPartOrImplant,
			Category.Leather,
			Category.Textile,
			Category.StoneBlock,
			Category.Manufactured,
			Category.Drug,
			Category.Medicine,
			Category.MortarShell,
			Category.Artifact
		};

		public static readonly Dictionary<Category, CategorySetting> _DEFAULTS = new Dictionary<Category, CategorySetting>
		{
			{
				Category.SmallVolumeResource,
				new CategorySetting(MultiplierMode.Scalar, 30f)
			},
			{
				Category.Resource,
				new CategorySetting(MultiplierMode.Fixed, 1000f)
			},
			{
				Category.RawFoodMeat,
				new CategorySetting(MultiplierMode.Fixed, 1000f)
			},
			{
				Category.RawFoodPlant,
				new CategorySetting(MultiplierMode.Fixed, 1000f)
			},
			{
				Category.PlantMatter,
				new CategorySetting(MultiplierMode.Fixed, 2000f)
			},
			{
				Category.Meal,
				new CategorySetting(MultiplierMode.Fixed, 150f)
			},
			{
				Category.FoodForAnimal,
				new CategorySetting(MultiplierMode.Fixed, 2000f)
			},
			{
				Category.Food,
				new CategorySetting(MultiplierMode.Fixed, 1000f)
			},
			{
				Category.Item,
				new CategorySetting(MultiplierMode.Fixed, 20f)
			},
			{
				Category.BodyPartOrImplant,
				new CategorySetting(MultiplierMode.Fixed, 5f)
			},
			{
				Category.Leather,
				new CategorySetting(MultiplierMode.Fixed, 1000f)
			},
			{
				Category.Textile,
				new CategorySetting(MultiplierMode.Fixed, 1000f)
			},
			{
				Category.StoneBlock,
				new CategorySetting(MultiplierMode.Fixed, 2500f)
			},
			{
				Category.Manufactured,
				new CategorySetting(MultiplierMode.Scalar, 10f)
			},
			{
				Category.Drug,
				new CategorySetting(MultiplierMode.Fixed, 4000f)
			},
			{
				Category.Medicine,
				new CategorySetting(MultiplierMode.Fixed, 75f)
			},
			{
				Category.MortarShell,
				new CategorySetting(MultiplierMode.Fixed, 25f)
			},
			{
				Category.Artifact,
				new CategorySetting(MultiplierMode.Fixed, 10f)
			},
			{
				Category.Other,
				new CategorySetting(MultiplierMode.Scalar, 10f)
			}
		};

		private static readonly HashSet<string> _CATEGORIES_BANNED = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Chunks", "Furniture", "StoneChunks", "WeaponsMelee", "Books" };

		public OgreStackMod(ModContentPack content)
			: base(content)
		{
			settings = ((Mod)this).GetSettings<OgreStackSettings>();
			settings.ReModify = delegate
			{
				Log.Message("[OgreStack]: Remodify From Settings Change");
				_activeThings = new Dictionary<string, List<Thing>>();
				List<Map> maps = Find.Maps;
				if (maps != null)
				{
					foreach (Map item in maps)
					{
						if (item != null && item.listerThings != null)
						{
							List<Thing> allThings = item.listerThings.AllThings;
							foreach (Thing item2 in allThings)
							{
								if (item2 != null && item2.def != null && !string.IsNullOrEmpty(((Def)item2.def).defName) && isStackIncreaseAllowed(item2.def))
								{
									if (!_activeThings.ContainsKey(((Def)item2.def).defName))
									{
										_activeThings.Add(((Def)item2.def).defName, new List<Thing>());
									}
									_activeThings[((Def)item2.def).defName].Add(item2);
								}
							}
						}
					}
				}
				ModifyStackSizes();
				_activeThings.Clear();
				_activeThings = null;
			};
			LongEventHandler.QueueLongEvent((Action)delegate
			{
				LongEventHandler.QueueLongEvent((Action)delegate
				{
					ModifyStackSizes();
				}, "OgreStack_Init_Execute", true, (Action<Exception>)null, true, false, (Action)null);
			}, "OgreStack_Init_Reg", true, (Action<Exception>)null, true, false, (Action)null);
		}

		public override string SettingsCategory()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return TaggedString.op_Implicit(Translator.Translate("OgreStack.ModName"));
		}

		public override void DoSettingsWindowContents(Rect rect)
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Expected O, but got Unknown
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Expected O, but got Unknown
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Expected O, but got Unknown
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Expected O, but got Unknown
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Expected O, but got Unknown
			//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Expected O, but got Unknown
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b5: Expected O, but got Unknown
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_0140: Unknown result type (might be due to invalid IL or missing references)
			//IL_0149: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Unknown result type (might be due to invalid IL or missing references)
			//IL_0155: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0163: Unknown result type (might be due to invalid IL or missing references)
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			//IL_0197: Unknown result type (might be due to invalid IL or missing references)
			//IL_019c: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
			//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_023e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0243: Unknown result type (might be due to invalid IL or missing references)
			//IL_0245: Unknown result type (might be due to invalid IL or missing references)
			//IL_024c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0269: Unknown result type (might be due to invalid IL or missing references)
			//IL_026e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0270: Unknown result type (might be due to invalid IL or missing references)
			//IL_0275: Unknown result type (might be due to invalid IL or missing references)
			//IL_0277: Unknown result type (might be due to invalid IL or missing references)
			//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
			//IL_032e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0333: Unknown result type (might be due to invalid IL or missing references)
			//IL_035f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0372: Unknown result type (might be due to invalid IL or missing references)
			//IL_0377: Unknown result type (might be due to invalid IL or missing references)
			//IL_0379: Unknown result type (might be due to invalid IL or missing references)
			//IL_0380: Unknown result type (might be due to invalid IL or missing references)
			//IL_039d: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_03a9: Unknown result type (might be due to invalid IL or missing references)
			//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
			//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0404: Unknown result type (might be due to invalid IL or missing references)
			//IL_0409: Unknown result type (might be due to invalid IL or missing references)
			//IL_0418: Unknown result type (might be due to invalid IL or missing references)
			//IL_0421: Unknown result type (might be due to invalid IL or missing references)
			//IL_0462: Unknown result type (might be due to invalid IL or missing references)
			//IL_0467: Unknown result type (might be due to invalid IL or missing references)
			//IL_04a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_04bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_04c6: Unknown result type (might be due to invalid IL or missing references)
			//IL_04cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_04e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_04ef: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f4: Unknown result type (might be due to invalid IL or missing references)
			//IL_04f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0553: Unknown result type (might be due to invalid IL or missing references)
			//IL_0568: Unknown result type (might be due to invalid IL or missing references)
			//IL_056d: Unknown result type (might be due to invalid IL or missing references)
			//IL_056f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0576: Unknown result type (might be due to invalid IL or missing references)
			//IL_0580: Unknown result type (might be due to invalid IL or missing references)
			//IL_0591: Unknown result type (might be due to invalid IL or missing references)
			//IL_0598: Unknown result type (might be due to invalid IL or missing references)
			//IL_059d: Unknown result type (might be due to invalid IL or missing references)
			//IL_059f: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a6: Unknown result type (might be due to invalid IL or missing references)
			//IL_05a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_05af: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c0: Unknown result type (might be due to invalid IL or missing references)
			//IL_05c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_05cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_05dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_05e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0643: Unknown result type (might be due to invalid IL or missing references)
			//IL_0648: Unknown result type (might be due to invalid IL or missing references)
			//IL_064a: Unknown result type (might be due to invalid IL or missing references)
			//IL_064f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0651: Unknown result type (might be due to invalid IL or missing references)
			//IL_067d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0691: Unknown result type (might be due to invalid IL or missing references)
			//IL_0696: Unknown result type (might be due to invalid IL or missing references)
			//IL_0698: Unknown result type (might be due to invalid IL or missing references)
			//IL_06a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_06a7: Unknown result type (might be due to invalid IL or missing references)
			//IL_06ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_06ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_06b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_06bc: Unknown result type (might be due to invalid IL or missing references)
			//IL_06df: Unknown result type (might be due to invalid IL or missing references)
			//IL_06ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_06f2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0715: Unknown result type (might be due to invalid IL or missing references)
			//IL_071a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0725: Unknown result type (might be due to invalid IL or missing references)
			//IL_072c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0731: Unknown result type (might be due to invalid IL or missing references)
			//IL_0733: Unknown result type (might be due to invalid IL or missing references)
			//IL_073a: Unknown result type (might be due to invalid IL or missing references)
			//IL_073f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0741: Unknown result type (might be due to invalid IL or missing references)
			//IL_0743: Unknown result type (might be due to invalid IL or missing references)
			//IL_0748: Unknown result type (might be due to invalid IL or missing references)
			//IL_074d: Unknown result type (might be due to invalid IL or missing references)
			//IL_074f: Unknown result type (might be due to invalid IL or missing references)
			//IL_07af: Unknown result type (might be due to invalid IL or missing references)
			//IL_07db: Unknown result type (might be due to invalid IL or missing references)
			//IL_088c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0891: Unknown result type (might be due to invalid IL or missing references)
			//IL_08a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_08aa: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_08b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_08ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_08c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_08d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_08da: Unknown result type (might be due to invalid IL or missing references)
			//IL_08df: Unknown result type (might be due to invalid IL or missing references)
			//IL_08e1: Unknown result type (might be due to invalid IL or missing references)
			//IL_093e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0946: Unknown result type (might be due to invalid IL or missing references)
			//IL_094b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0966: Unknown result type (might be due to invalid IL or missing references)
			//IL_098e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0993: Unknown result type (might be due to invalid IL or missing references)
			//IL_099e: Unknown result type (might be due to invalid IL or missing references)
			//IL_09b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_09b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_09ba: Unknown result type (might be due to invalid IL or missing references)
			//IL_09bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_09c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_09cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_09d9: Unknown result type (might be due to invalid IL or missing references)
			//IL_09ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_09f1: Unknown result type (might be due to invalid IL or missing references)
			//IL_09fb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a0c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0829: Unknown result type (might be due to invalid IL or missing references)
			//IL_082e: Unknown result type (might be due to invalid IL or missing references)
			//IL_083f: Unknown result type (might be due to invalid IL or missing references)
			//IL_084a: Unknown result type (might be due to invalid IL or missing references)
			//IL_084c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0859: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a4b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a50: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a52: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a57: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a59: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a85: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a99: Unknown result type (might be due to invalid IL or missing references)
			//IL_0a9e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0aa0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0aa8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0aaf: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ab4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ab6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0abb: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ac4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ace: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ad6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0add: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ae2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ae4: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ae9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0af2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b01: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b09: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b3d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b72: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b77: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b8b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b90: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b92: Unknown result type (might be due to invalid IL or missing references)
			//IL_0b97: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ba0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ba7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bb8: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bc0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bc5: Unknown result type (might be due to invalid IL or missing references)
			//IL_0bc7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c24: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c38: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c3d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c3f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c47: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c4e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c53: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c55: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c5a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c63: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c6a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c75: Unknown result type (might be due to invalid IL or missing references)
			//IL_0c7d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ca2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0ca7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cac: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cb7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cbe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0cc3: Unknown result type (might be due to invalid IL or missing references)
			Color textColor = default(Color);
			((Color)(ref textColor))..ctor(1f, 0.972549f, 0.23921569f, 1f);
			GUIStyleState normal = new GUIStyleState
			{
				textColor = textColor
			};
			GUIStyle val = new GUIStyle(Text.CurFontStyle);
			val.fontStyle = (FontStyle)1;
			val.normal = normal;
			GUIStyle val2 = new GUIStyle(Text.CurFontStyle);
			val2.alignment = (TextAnchor)3;
			val2.fontSize = 11;
			val2.fontStyle = (FontStyle)1;
			val2.normal = new GUIStyleState
			{
				textColor = new Color(1f, 63f / 85f, 0.23921569f, 1f)
			};
			val2.padding = new RectOffset(4, 0, 0, 0);
			Listing_Standard val3 = new Listing_Standard((GameFont)1);
			Listing_Standard val4 = new Listing_Standard((GameFont)1);
			List<KeyValuePair<string, int>> list = new IndividualOverrides().ViewInternalOverrides();
			float num = 3f * (Text.LineHeight + 2f) + 2f * val2.lineHeight + 30f + 23f * (Text.LineHeight + 7f) + (float)list.Count * (Text.LineHeight + 3f) + 40f;
			Rect val5 = default(Rect);
			((Rect)(ref val5))..ctor(0f, 0f, ((Rect)(ref rect)).width - 25f, num);
			Widgets.BeginScrollView(rect, ref settings.ScrollPosition, val5, true);
			Rect val6 = GenUI.LeftPart(val5, 0.78f);
			Rect val7 = GenUI.RightPart(val5, 0.2f);
			((Listing)val4).ColumnWidth = ((Rect)(ref val7)).width;
			((Listing)val4).Begin(val7);
			Rect rect2 = ((Listing)val4).GetRect(Text.LineHeight + 2f, 1f);
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)0;
			GUI.Box(rect2, TaggedString.op_Implicit(Translator.Translate("OgreStack.SectionHeader.Presets")), val);
			Text.Anchor = anchor;
			Color color = GUI.color;
			GUI.color = Color.grey;
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 1f, ((Rect)(ref rect2)).width);
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 2f, ((Rect)(ref rect2)).width);
			GUI.color = color;
			rect2 = ((Listing)val4).GetRect(val2.lineHeight, 1f);
			GUI.Box(rect2, TaggedString.op_Implicit(Translator.Translate("OgreStack.Presets.Section.Ogre")), val2);
			rect2 = ((Listing)val4).GetRect(7f, 1f);
			color = GUI.color;
			GUI.color = Color.grey;
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + 3f, ((Rect)(ref rect2)).width);
			GUI.color = color;
			foreach (Preset ogrePreset in Presets.GetOgrePresets())
			{
				rect2 = ((Listing)val4).GetRect(Text.LineHeight, 1f);
				GUI.SetNextControlName(ogrePreset.NameKey);
				if (Widgets.ButtonText(rect2, TaggedString.op_Implicit(Translator.Translate(ogrePreset.NameKey)), true, true, true, (TextAnchor?)null))
				{
					ogrePreset.Modify(settings);
				}
				rect2 = ((Listing)val4).GetRect(1f, 1f);
			}
			((Listing)val4).GetRect(7f, 1f);
			rect2 = ((Listing)val4).GetRect(val2.lineHeight, 1f);
			GUI.Box(rect2, TaggedString.op_Implicit(Translator.Translate("OgreStack.Presets.Section.Scalar")), val2);
			rect2 = ((Listing)val4).GetRect(7f, 1f);
			color = GUI.color;
			GUI.color = Color.grey;
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + 3f, ((Rect)(ref rect2)).width);
			GUI.color = color;
			foreach (Preset scalarPreset in Presets.GetScalarPresets())
			{
				rect2 = ((Listing)val4).GetRect(Text.LineHeight, 1f);
				GUI.SetNextControlName(scalarPreset.NameKey);
				if (Widgets.ButtonText(rect2, TaggedString.op_Implicit(Translator.Translate(scalarPreset.NameKey)), true, true, true, (TextAnchor?)null))
				{
					scalarPreset.Modify(settings);
				}
				rect2 = ((Listing)val4).GetRect(1f, 1f);
			}
			((Listing)val4).End();
			((Listing)val3).ColumnWidth = ((Rect)(ref val6)).width;
			((Listing)val3).Begin(val6);
			rect2 = ((Listing)val3).GetRect(Text.LineHeight + 2f, 1f);
			anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)0;
			GUI.Box(rect2, TaggedString.op_Implicit(Translator.Translate("OgreStack.SectionHeader.Settings")), val);
			Text.Anchor = anchor;
			color = GUI.color;
			GUI.color = Color.grey;
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 1f, ((Rect)(ref rect2)).width);
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 2f, ((Rect)(ref rect2)).width);
			GUI.color = color;
			Rect rect3 = ((Listing)val3).GetRect(val2.lineHeight, 1f);
			GUI.Box(GenUI.LeftPart(rect3, 0.6f), TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.Category")), val2);
			Rect val8 = GenUI.RightPart(rect3, 0.4f);
			Rect val9 = GenUI.LeftHalf(val8);
			GUI.Box(val9, TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.Mode")), val2);
			GUI.Box(GenUI.RightHalf(val8), TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.Value")), val2);
			TooltipHandler.TipRegion(val9, TipSignal.op_Implicit(Translator.Translate("OgreStack.Settings.Mode.Desc")));
			List<Category> list2 = new List<Category>(_PROCESSING_ORDER);
			list2.Add(Category.Other);
			foreach (Category c in list2)
			{
				Rect rect4 = ((Listing)val3).GetRect(7f, 1f);
				color = GUI.color;
				GUI.color = Color.grey;
				Widgets.DrawLineHorizontal(((Rect)(ref rect4)).x, ((Rect)(ref rect4)).y + 3f, ((Rect)(ref rect4)).width);
				GUI.color = color;
				Rect rect5 = ((Listing)val3).GetRect(Text.LineHeight, 1f);
				Widgets.DrawHighlightIfMouseover(rect5);
				Rect val10 = GenUI.LeftPart(rect5, 0.6f);
				anchor = Text.Anchor;
				Text.Anchor = (TextAnchor)3;
				Widgets.Label(val10, Translator.Translate("OgreStack.Settings." + c.ToString() + ".Title"));
				Text.Anchor = anchor;
				TooltipHandler.TipRegion(val10, TipSignal.op_Implicit(Translator.Translate("OgreStack.Settings." + c.ToString() + ".Desc")));
				Rect val11 = GenUI.RightPart(rect5, 0.4f);
				Rect val12 = GenUI.LeftPart(val11, 0.49f);
				Rect val13 = GenUI.Rounded(GenUI.RightHalf(val11));
				Widgets.Dropdown<MultiplierMode, MultiplierMode>(val12, MultiplierMode.Fixed, (Func<MultiplierMode, MultiplierMode>)((MultiplierMode s) => s), (Func<MultiplierMode, IEnumerable<DropdownMenuElement<MultiplierMode>>>)((MultiplierMode s) => new List<DropdownMenuElement<MultiplierMode>>
				{
					new DropdownMenuElement<MultiplierMode>
					{
						option = new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.Mode.Scalar")), (Action)delegate
						{
							settings.Values[c].Mode = MultiplierMode.Scalar;
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
						payload = MultiplierMode.Scalar
					},
					new DropdownMenuElement<MultiplierMode>
					{
						option = new FloatMenuOption(TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.Mode.Fixed")), (Action)delegate
						{
							settings.Values[c].Mode = MultiplierMode.Fixed;
						}, (MenuOptionPriority)4, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, true, 0),
						payload = MultiplierMode.Fixed
					}
				}), TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.Mode." + settings.Values[c].Mode)), (Texture2D)null, (string)null, (Texture2D)null, (Action)null, false);
				settings.Values[c].Buffer = Widgets.TextField(val13, settings.Values[c].Buffer);
				if (!settings.Values[c].ParseBuffer())
				{
					color = GUI.color;
					GUI.color = new Color(0.662745f, 0f, 0f);
					Widgets.DrawBox(GenUI.Rounded(val13), 2, (Texture2D)null);
					GUI.color = color;
				}
			}
			Rect rect6 = ((Listing)val3).GetRect(15f, 1f);
			rect2 = ((Listing)val3).GetRect(Text.LineHeight + 2f, 1f);
			anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)0;
			GUI.Box(rect2, TaggedString.op_Implicit(Translator.Translate("OgreStack.SectionHeader.SingleThingDefTargeting")), val);
			Text.Anchor = anchor;
			color = GUI.color;
			GUI.color = Color.grey;
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 1f, ((Rect)(ref rect2)).width);
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 2f, ((Rect)(ref rect2)).width);
			GUI.color = color;
			anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(((Listing)val3).GetRect(Text.LineHeight * 3f, 1f), TranslatorFormattedStringExtensions.Translate("OgreStack.Settings.SingleThingDefTargeting.Desc", new NamedArgument((object)DataUtil.GenerateFilePath(DataUtil._OVERRIDES_FILE_NAME).Replace("/", "\\"), "{0}")));
			Text.Anchor = anchor;
			rect3 = ((Listing)val3).GetRect(val2.lineHeight, 1f);
			anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			GUI.Box(GenUI.LeftPart(rect3, 0.35f), TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.DefName")), val2);
			GUI.Box(GenUI.RightPart(rect3, 0.65f), TaggedString.op_Implicit(Translator.Translate("OgreStack.Settings.StackLimit")), val2);
			Text.Anchor = anchor;
			foreach (KeyValuePair<string, int> item in list)
			{
				string key = item.Key;
				int value = item.Value;
				Rect rect7 = ((Listing)val3).GetRect(3f, 1f);
				color = GUI.color;
				GUI.color = Color.grey;
				Widgets.DrawLineHorizontal(((Rect)(ref rect7)).x, ((Rect)(ref rect7)).y + 1f, ((Rect)(ref rect7)).width);
				GUI.color = color;
				Rect rect8 = ((Listing)val3).GetRect(Text.LineHeight, 1f);
				Widgets.DrawHighlightIfMouseover(rect8);
				Rect val14 = GenUI.LeftPart(rect8, 0.35f);
				anchor = Text.Anchor;
				Text.Anchor = (TextAnchor)3;
				Widgets.Label(val14, key);
				Text.Anchor = anchor;
				Rect val15 = GenUI.RightPart(rect8, 0.65f);
				anchor = Text.Anchor;
				Text.Anchor = (TextAnchor)3;
				Widgets.Label(val15, value.ToString());
				Text.Anchor = anchor;
				TooltipHandler.TipRegion(rect8, TipSignal.op_Implicit("From Rule:\n<item defName=\"" + key + "\" stackLimit=\"" + value + "\" />"));
			}
			rect6 = ((Listing)val3).GetRect(15f, 1f);
			rect2 = ((Listing)val3).GetRect(Text.LineHeight + 2f, 1f);
			anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)0;
			GUI.Box(rect2, TaggedString.op_Implicit(Translator.Translate("OgreStack.SectionHeader.Debug")), val);
			Text.Anchor = anchor;
			color = GUI.color;
			GUI.color = Color.grey;
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 1f, ((Rect)(ref rect2)).width);
			Widgets.DrawLineHorizontal(((Rect)(ref rect2)).x, ((Rect)(ref rect2)).y + ((Rect)(ref rect2)).height - 2f, ((Rect)(ref rect2)).width);
			GUI.color = color;
			Rect rect9 = ((Listing)val3).GetRect(Text.LineHeight, 1f);
			Widgets.DrawHighlightIfMouseover(rect9);
			Rect val16 = GenUI.LeftPart(rect9, 0.8f);
			anchor = Text.Anchor;
			Text.Anchor = (TextAnchor)3;
			Widgets.Label(val16, Translator.Translate("OgreStack.Settings.CSV.Title"));
			Text.Anchor = anchor;
			TooltipHandler.TipRegion(val16, TipSignal.op_Implicit(TranslatorFormattedStringExtensions.Translate("OgreStack.Settings.CSV.Desc", new NamedArgument((object)DataUtil.GenerateFilePath("OgreStack_DefsList.csv").Replace("/", "\\"), "{0}"))));
			Rect val17 = GenUI.RightPart(rect9, 0.2f);
			Widgets.Checkbox(((Rect)(ref val17)).x + ((Rect)(ref val17)).width - 26f, ((Rect)(ref val17)).y, ref settings.IsDebug, 24f, false, false, (Texture2D)null, (Texture2D)null);
			((Listing)val3).End();
			Widgets.EndScrollView();
		}

		private List<ModDefinition> getSupportedMods()
		{
			List<ModDefinition> list = new List<ModDefinition>
			{
				new VegetableGarden(),
				new RimCuisine2(),
				new CuprosDrinks(),
				new TiberiumRim(),
				new MedievalTimes(),
				new GeneticRim(),
				new ExpandedWoodworking(),
				new RimWorldOfMagic(),
				new Ammunition(),
				new VanillaExpanded(),
				new AestheticMaterials(),
				new AlphaGenes(),
				new MedievalOverhaul(),
				new MiscForbid(),
				new RimWorld()
			};
			foreach (ModDefinition item in list)
			{
				item.Init();
			}
			return list;
		}

		private bool isStackIncreaseAllowed(ThingDef d)
		{
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Invalid comparison between Unknown and I4
			if (d == null || d.thingCategories == null || d.thingCategories.Count <= 0 || d.FirstThingCategory == null)
			{
				return false;
			}
			bool flag = d.IsStuff || d.isTechHediff || ((int)d.category == 2 && !d.isUnfinishedThing && !d.IsCorpse && !d.destroyOnDrop && !d.IsRangedWeapon && !d.IsApparel && !d.Minifiable && !d.IsArt && !d.IsBed);
			if (flag)
			{
				flag = !_CATEGORIES_BANNED.Contains(((object)d.FirstThingCategory).ToString());
			}
			if (flag && d.comps != null && d.comps.Count > 0)
			{
				foreach (CompProperties comp in d.comps)
				{
					if (comp.compClass == typeof(CompQuality))
					{
						flag = d.stackLimit > 1;
						break;
					}
				}
			}
			return flag;
		}

		internal void ModifyStackSizes()
		{
			//IL_03ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_03b3: Invalid comparison between Unknown and I4
			//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
			List<ModDefinition> supportedMods = getSupportedMods();
			List<Category> list = new List<Category>(_PROCESSING_ORDER);
			supportedMods.Reverse();
			list.Reverse();
			List<string[]> list2 = null;
			IndividualOverrides individualOverrides = new IndividualOverrides();
			bool isDebug = settings.IsDebug;
			if (isDebug)
			{
				list2 = new List<string[]>();
			}
			Dictionary<Category, bool> dictionary = _PROCESSING_ORDER.ToDictionary((Category x) => x, (Category y) => settings.Values[y].ParseBuffer());
			foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
			{
				if (!isStackIncreaseAllowed(allDef))
				{
					continue;
				}
				string text = CategorySetting.GetBaseStackLimit(allDef).ToString();
				string text2 = string.Empty + text;
				string text3 = "Other " + settings.Values[Category.Other].ToString();
				string text4 = string.Empty;
				bool flag = false;
				int itemLevelOverride = individualOverrides.GetItemLevelOverride(((Def)allDef).defName);
				if (itemLevelOverride > 0)
				{
					flag = true;
					allDef.stackLimit = itemLevelOverride;
					text3 = "UserOverride.SetStackLimit(" + ((Def)allDef).defName + ": " + itemLevelOverride + ")";
					text4 = "Overrides.xml";
				}
				if (individualOverrides.IsCategoryBanned(((object)allDef.FirstThingCategory).ToString()))
				{
					flag = true;
					allDef.stackLimit = 1;
					text3 = "UserOverride.BanStacking(Category: " + ((object)allDef.FirstThingCategory).ToString() + ")";
					text4 = "Overrides.xml";
				}
				if (!flag)
				{
					for (int num = supportedMods.Count - 1; num > -1; num--)
					{
						if (supportedMods[num].IsNonStackable(allDef))
						{
							flag = true;
							text3 = "StackChangeForbidden";
							text4 = supportedMods[num].GetType().ToString();
							break;
						}
						for (int num2 = list.Count - 1; num2 > -1; num2--)
						{
							List<Func<ThingDef, bool>> categoryFunctions = supportedMods[num].GetCategoryFunctions(list[num2]);
							if (categoryFunctions != null)
							{
								foreach (Func<ThingDef, bool> item in categoryFunctions)
								{
									flag = item(allDef);
									text2 = allDef.stackLimit.ToString();
									if (flag)
									{
										if (dictionary[list[num2]])
										{
											settings.Values[list[num2]].ProcessThing(allDef, _activeThings);
										}
										text3 = list[num2].ToString() + " " + settings.Values[list[num2]].ToString();
										text4 = supportedMods[num].GetType().Name.ToString();
										num = (num2 = -1);
										break;
									}
								}
							}
						}
					}
				}
				if (!flag)
				{
					settings.Values[Category.Other].ProcessThing(allDef, _activeThings);
					text4 = "OgreStack.Other";
				}
				if (allDef.stackLimit > 1)
				{
					allDef.drawGUIOverlay = true;
					if ((int)allDef.resourceReadoutPriority == 0)
					{
						allDef.resourceReadoutPriority = (ResourceCountPriority)2;
					}
				}
				if (isDebug)
				{
					list2.Add(new string[7]
					{
						((Def)allDef).defName,
						((object)allDef.FirstThingCategory).ToString(),
						text,
						text2,
						text3,
						allDef.stackLimit.ToString(),
						text4
					});
				}
			}
			ResourceCounter.ResetDefs();
			HashSet<string> hashSet = new HashSet<string>(DefDatabase<ThingDef>.AllDefs.Select((ThingDef x) => ((Def)x).defName));
			foreach (TreeNode_ThingCategory allThingCategoryNode in ThingCategoryNodeDatabase.allThingCategoryNodes)
			{
				for (int num3 = allThingCategoryNode.catDef.childThingDefs.Count - 1; num3 > -1; num3--)
				{
					if (!hashSet.Contains(((Def)allThingCategoryNode.catDef.childThingDefs[num3]).defName))
					{
						allThingCategoryNode.catDef.childThingDefs.RemoveAt(num3);
					}
				}
			}
			if (isDebug)
			{
				list2 = (from x in list2
					orderby x[1], x[0]
					select x).ToList();
				list2.Insert(0, new string[7] { "DefName", "RimWorldCategory", "DefaultStackBase", "AlteredBase", "OgreStackCategory(FixedBase)", "FinalStackLimit", "Support" });
				try
				{
					DataUtil.WriteFisherPriceCSV(list2, "OgreStack_DefsList.csv");
					Log.Message("[OgreStack]: Write Defs CSV => [" + DataUtil.GenerateFilePath("OgreStack_DefsList.csv").Replace("/", "\\") + "]");
				}
				catch (IOException ex)
				{
					if (Regex.IsMatch(ex.Message, "sharing violation", RegexOptions.IgnoreCase))
					{
						Log.Warning("[OgreStack]: Cannot Write 'OgreStack_DefsList.csv'. Make sure you don't have the file open so OgreStack can overwrite it.");
						Log.Warning(ex.Message);
					}
				}
				list2.Clear();
				list2 = null;
			}
			Log.Message("[OgreStack]: Modify Stack Sizes Complete");
		}
	}
	internal abstract class ModDefinition
	{
		private Dictionary<Category, List<Func<ThingDef, bool>>> _definitions = new Dictionary<Category, List<Func<ThingDef, bool>>>();

		internal ModDefinition()
		{
		}

		protected void Define(Category category, Func<ThingDef, bool> fnDefine)
		{
			List<Func<ThingDef, bool>> value = null;
			if (!_definitions.TryGetValue(category, out value))
			{
				value = new List<Func<ThingDef, bool>>();
				_definitions.Add(category, value);
			}
			value.Add(fnDefine);
		}

		internal List<Func<ThingDef, bool>> GetCategoryFunctions(Category category)
		{
			List<Func<ThingDef, bool>> value;
			return _definitions.TryGetValue(category, out value) ? value : null;
		}

		internal abstract void Init();

		internal abstract bool IsNonStackable(ThingDef thing);
	}
}
namespace OgreStack.Support
{
	internal class AlphaGenes : ModDefinition
	{
		internal AlphaGenes()
		{
		}

		internal override void Init()
		{
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			if (string.Compare(((object)thing.FirstThingCategory).ToString(), "AG_GeneTools", ignoreCase: true) == 0)
			{
				return true;
			}
			if (Regex.IsMatch(((Def)thing).defName, "^AG_(Alphapack|Mixedpack|HumanEgg)$"))
			{
				return true;
			}
			return false;
		}
	}
	internal class Ammunition : ModDefinition
	{
		internal Ammunition()
		{
		}

		internal override void Init()
		{
			Define(Category.Manufactured, (ThingDef d) => string.Compare("Ammunition", ((object)d.FirstThingCategory).ToString(), ignoreCase: true) == 0 && Regex.IsMatch(((Def)d).defName, "ammunition", RegexOptions.IgnoreCase));
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class CuprosDrinks : ModDefinition
	{
		internal CuprosDrinks()
		{
		}

		internal override void Init()
		{
			Define(Category.Food, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^CPD_(Alcohol|Drinks|Energy|Sodas)", RegexOptions.IgnoreCase));
			Define(Category.Manufactured, delegate(ThingDef d)
			{
				bool flag = string.Compare(((object)d.FirstThingCategory).ToString(), "Manufactured", ignoreCase: true) == 0 && Regex.IsMatch(((Def)d).defName, "^CPD_");
				if (flag)
				{
					d.stackLimit = 25;
				}
				return flag;
			});
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class ExpandedWoodworking : ModDefinition
	{
		internal ExpandedWoodworking()
		{
		}

		internal override void Init()
		{
			Define(Category.Resource, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^(WoodLumber|WoodTypes)$", RegexOptions.IgnoreCase));
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class GeneticRim : ModDefinition
	{
		internal GeneticRim()
		{
		}

		internal override void Init()
		{
			Define(Category.Resource, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^GR_(AlphaSerums|GeneticMaterial)", RegexOptions.IgnoreCase));
			Define(Category.PlantMatter, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Manufactured", ignoreCase: true) == 0 && string.Compare(((Def)d).defName, "GR_OrganicPulp", ignoreCase: true) == 0);
			Define(Category.Manufactured, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "GR_Incubators", ignoreCase: true) == 0 && string.Compare(((Def)d).defName, "GR_EmptyIncubator", ignoreCase: true) == 0);
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return string.Compare(((object)thing.FirstThingCategory).ToString(), "GR_Incubators", ignoreCase: true) == 0 && string.Compare(((Def)thing).defName, "GR_EmptyIncubator", ignoreCase: true) != 0;
		}
	}
	internal class MedievalOverhaul : ModDefinition
	{
		private static readonly HashSet<string> WOOD_CATEGORY = new HashSet<string> { "DankPyon_Wood", "DankPyon_RawWood" };

		private static readonly HashSet<string> WOOD_DEFS = new HashSet<string> { "DankPyon_AncientWoodLog", "DankPyon_DryadWoodLog" };

		internal override void Init()
		{
			Define(Category.Resource, (ThingDef d) => WOOD_CATEGORY.Contains(((object)d.FirstThingCategory).ToString()) || WOOD_DEFS.Contains(((Def)d).defName));
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class MedievalTimes : ModDefinition
	{
		internal MedievalTimes()
		{
		}

		internal override void Init()
		{
			Define(Category.Resource, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "TradeGoods", ignoreCase: true) == 0 && Regex.IsMatch(((Def)d).defName, "^MedTimes_"));
			Define(Category.MortarShell, (ThingDef d) => Regex.IsMatch(((Def)d).defName, "^MedTimes_Resource_(ExplosivePot|Quicklime)", RegexOptions.IgnoreCase));
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return Regex.IsMatch(((object)thing.FirstThingCategory).ToString(), "^WeaponsMelee_(Leg|Nan)", RegexOptions.IgnoreCase);
		}
	}
	internal class MiscForbid : ModDefinition
	{
		private static readonly HashSet<string> DEFS_TO_BAN = new HashSet<string>
		{
			"DeployableTent", "DeployableTentMedium", "DeployableTentBig", "DeployableTentLong", "Genepack", "HumanEmbryo", "Xenogerm", "PW_OddKeystone", "VBE_Blueprint", "VBE_Map",
			"VBE_Newspaper"
		};

		private static readonly HashSet<string> CATEGORIES_TO_BAN = new HashSet<string> { "PW_Eggs", "PW_PokeBall" };

		internal MiscForbid()
		{
		}

		internal override void Init()
		{
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			if (DEFS_TO_BAN.Contains(((Def)thing).defName))
			{
				return true;
			}
			if (CATEGORIES_TO_BAN.Contains(((object)thing.FirstThingCategory).ToString()))
			{
				return true;
			}
			return false;
		}
	}
	internal class AestheticMaterials : ModDefinition
	{
		internal AestheticMaterials()
		{
		}

		internal override void Init()
		{
			Define(Category.Resource, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^(ExoticWood|AestheticMetals)$", RegexOptions.IgnoreCase));
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class RimCuisine2 : ModDefinition
	{
		internal static HashSet<string> BROKEN_MEALS_SHOULD_BE_FOOD = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CaramelBalls", "HotCocoa", "HotTea", "RC2_Hardtack" };

		internal RimCuisine2()
		{
		}

		internal override void Init()
		{
			Define(Category.Food, delegate(ThingDef d)
			{
				string input = ((object)d.FirstThingCategory).ToString();
				return Regex.IsMatch(input, "^RC2_(Vegetables|Meat|AnimalProduct|Fruit)Processed$", RegexOptions.IgnoreCase) || Regex.IsMatch(input, "^RC2_(Drink|Food)MealsJoy$", RegexOptions.IgnoreCase) || (Regex.IsMatch(input, "^FoodMeals$", RegexOptions.IgnoreCase) && BROKEN_MEALS_SHOULD_BE_FOOD.Contains(((Def)d).defName));
			});
			Define(Category.RawFoodPlant, delegate(ThingDef d)
			{
				string input = ((object)d.FirstThingCategory).ToString();
				return Regex.IsMatch(input, "^RC2_(Fruits|Grains|Vegetables|Sweets)Raw$", RegexOptions.IgnoreCase) || Regex.IsMatch(input, "^RC2_(Grains|Sweets)Processed$");
			});
			Define(Category.Drug, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^RC2_Alcohol$", RegexOptions.IgnoreCase));
			Define(Category.Manufactured, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^RC2_AlcoholPrecursors$", RegexOptions.IgnoreCase));
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class RimWorld : ModDefinition
	{
		internal RimWorld()
		{
		}

		internal override void Init()
		{
			Define(Category.SmallVolumeResource, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "ResourcesRaw", ignoreCase: true) == 0 && d.smallVolume);
			Define(Category.Resource, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "ResourcesRaw", ignoreCase: true) == 0);
			Define(Category.RawFoodMeat, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^(AnimalProductRaw|Eggs|MeatRaw)", RegexOptions.IgnoreCase));
			Define(Category.RawFoodPlant, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "PlantFoodRaw", ignoreCase: true) == 0);
			Define(Category.PlantMatter, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "PlantMatter", ignoreCase: true) == 0);
			Define(Category.Meal, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "FoodMeals", ignoreCase: true) == 0);
			Define(Category.FoodForAnimal, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Foods", ignoreCase: true) == 0 && d.ingestible != null && d.ingestible.optimalityOffsetFeedingAnimals > 0f);
			Define(Category.Food, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Foods", ignoreCase: true) == 0);
			Define(Category.Item, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Items", ignoreCase: true) == 0);
			Define(Category.BodyPartOrImplant, (ThingDef d) => d.isTechHediff);
			Define(Category.Leather, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Leathers", ignoreCase: true) == 0);
			Define(Category.Textile, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^(Textiles|Wools)", RegexOptions.IgnoreCase));
			Define(Category.StoneBlock, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "StoneBlocks", ignoreCase: true) == 0);
			Define(Category.Manufactured, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Manufactured", ignoreCase: true) == 0);
			Define(Category.Drug, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Drugs", ignoreCase: true) == 0);
			Define(Category.Medicine, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Medicine", ignoreCase: true) == 0);
			Define(Category.MortarShell, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "MortarShells", ignoreCase: true) == 0);
			Define(Category.Artifact, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "Artifacts", ignoreCase: true) == 0);
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class RimWorldOfMagic : ModDefinition
	{
		internal static readonly Regex RX_MAGICYTE_STONE = new Regex("^TM_EStone_", RegexOptions.IgnoreCase);

		internal static readonly Regex RX_SCROLLS_BOOKS = new Regex("^TM_(SkillBooks|Scrolls)$", RegexOptions.IgnoreCase);

		internal RimWorldOfMagic()
		{
		}

		internal override void Init()
		{
			Define(Category.Resource, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "TM_Magicyte", ignoreCase: true) == 0 && RX_MAGICYTE_STONE.IsMatch(((Def)d).defName));
			Define(Category.SmallVolumeResource, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "TM_Magicyte", ignoreCase: true) == 0 && string.Compare(((Def)d).defName, "RawMagicyte", ignoreCase: true) == 0);
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return RX_SCROLLS_BOOKS.IsMatch(((object)thing.FirstThingCategory).ToString());
		}
	}
	internal class TiberiumRim : ModDefinition
	{
		internal TiberiumRim()
		{
		}

		internal override void Init()
		{
			Define(Category.Resource, (ThingDef d) => string.Compare(((Def)d).defName, "TiberiumPlantTree", ignoreCase: true) == 0);
			Define(Category.Resource, (ThingDef d) => Regex.IsMatch(((Def)d).defName, "^Book(Riparius|Rutila|Vinifera)$", RegexOptions.IgnoreCase));
			Define(Category.PlantMatter, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "TiberiumItems", ignoreCase: true) == 0 && Regex.IsMatch(((Def)d).defName, "^(TiberiumPlant)|(TiberiumRaw)$", RegexOptions.IgnoreCase));
			Define(Category.Manufactured, delegate(ThingDef d)
			{
				bool flag = string.Compare(((Def)d).defName, "TiberiumFuel", ignoreCase: true) == 0;
				if (flag)
				{
					d.stackLimit = 400;
				}
				return flag;
			});
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class VanillaExpanded : ModDefinition
	{
		internal VanillaExpanded()
		{
		}

		internal override void Init()
		{
			Define(Category.Food, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^VCE_(Condiments|UncookedSoups)$", RegexOptions.IgnoreCase));
			Define(Category.RawFoodPlant, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "VCE_Fruit", ignoreCase: true) == 0);
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
	internal class VegetableGarden : ModDefinition
	{
		internal VegetableGarden()
		{
		}

		internal override void Init()
		{
			Define(Category.RawFoodPlant, (ThingDef d) => Regex.IsMatch(((object)d.FirstThingCategory).ToString(), "^(FruitFoodRaw|CookingSupplies)", RegexOptions.IgnoreCase));
			Define(Category.FoodForAnimal, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "AnimalFeed", ignoreCase: true) == 0);
			Define(Category.Food, (ThingDef d) => string.Compare(((object)d.FirstThingCategory).ToString(), "SweetMeals", ignoreCase: true) == 0);
		}

		internal override bool IsNonStackable(ThingDef thing)
		{
			return false;
		}
	}
}
namespace OgreStack.PersistentData
{
	internal static class DataUtil
	{
		internal static readonly string _OVERRIDES_FILE_NAME = "Overrides.xml";

		internal static string GenerateFilePath(string fileName)
		{
			string text = Path.Combine(GenFilePaths.SaveDataFolderPath, "OgreStack");
			DirectoryInfo directoryInfo = new DirectoryInfo(text);
			if (!directoryInfo.Exists)
			{
				directoryInfo.Create();
			}
			return Path.Combine(text, fileName);
		}

		internal static void WriteFisherPriceCSV(List<string[]> data, string fileName)
		{
			if (data == null || !data.Any())
			{
				return;
			}
			string path = GenerateFilePath(fileName);
			using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
			using StreamWriter streamWriter = new StreamWriter(stream);
			foreach (string[] datum in data)
			{
				int length = datum.GetLength(0);
				for (int i = 0; i < length - 1; i++)
				{
					streamWriter.Write(datum[i]);
					streamWriter.Write(",");
				}
				streamWriter.WriteLine(datum[length - 1]);
			}
		}

		internal static XDocument GetUserOverrides()
		{
			string text = GenerateFilePath(_OVERRIDES_FILE_NAME);
			if (!File.Exists(text))
			{
				WriteDefaultOverrides();
			}
			return XDocument.Load(text);
		}

		internal static void WriteDefaultOverrides()
		{
			string path = GenerateFilePath(_OVERRIDES_FILE_NAME);
			if (File.Exists(path))
			{
				return;
			}
			using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
			using StreamWriter streamWriter = new StreamWriter(stream);
			streamWriter.Write(IndividualOverrides.GenerateDefaultOverridesXml());
		}
	}
	public static class Presets
	{
		private static List<Preset> _OGRE_PRESETS;

		private static List<Preset> _SCALAR_PRESETS;

		private static Dictionary<string, Preset> _REFERENCE;

		static Presets()
		{
			_OGRE_PRESETS = new List<Preset>
			{
				new Preset("OgreStack.Presets.OgreDefault")
				{
					Modify = delegate(OgreStackSettings a)
					{
						foreach (Category key in OgreStackMod._DEFAULTS.Keys)
						{
							a.Values[key].Value = OgreStackMod._DEFAULTS[key].Value;
							a.Values[key].Mode = OgreStackMod._DEFAULTS[key].Mode;
							a.Values[key].Buffer = OgreStackMod._DEFAULTS[key].Buffer;
						}
					}
				},
				new Preset("OgreStack.Presets.OgreMid")
				{
					Modify = delegate(OgreStackSettings a)
					{
						a.Values[Category.SmallVolumeResource].Modify(MultiplierMode.Scalar, 20f, "20");
						a.Values[Category.Resource].Modify(MultiplierMode.Fixed, 750f, "750");
						a.Values[Category.RawFoodMeat].Modify(MultiplierMode.Fixed, 750f, "750");
						a.Values[Category.RawFoodPlant].Modify(MultiplierMode.Fixed, 750f, "750");
						a.Values[Category.PlantMatter].Modify(MultiplierMode.Fixed, 1000f, "1000");
						a.Values[Category.Meal].Modify(MultiplierMode.Fixed, 100f, "100");
						a.Values[Category.FoodForAnimal].Modify(MultiplierMode.Fixed, 1500f, "1500");
						a.Values[Category.Food].Modify(MultiplierMode.Fixed, 750f, "750");
						a.Values[Category.Item].Modify(MultiplierMode.Fixed, 10f, "10");
						a.Values[Category.BodyPartOrImplant].Modify(MultiplierMode.Fixed, 5f, "5");
						a.Values[Category.Leather].Modify(MultiplierMode.Fixed, 750f, "750");
						a.Values[Category.Textile].Modify(MultiplierMode.Fixed, 750f, "750");
						a.Values[Category.StoneBlock].Modify(MultiplierMode.Fixed, 1500f, "1500");
						a.Values[Category.Manufactured].Modify(MultiplierMode.Scalar, 10f, "10");
						a.Values[Category.Drug].Modify(MultiplierMode.Fixed, 2000f, "2000");
						a.Values[Category.Medicine].Modify(MultiplierMode.Fixed, 50f, "50");
						a.Values[Category.MortarShell].Modify(MultiplierMode.Fixed, 25f, "25");
						a.Values[Category.Artifact].Modify(MultiplierMode.Fixed, 10f, "10");
						a.Values[Category.Other].Modify(MultiplierMode.Scalar, 5f, "5");
					}
				},
				new Preset("OgreStack.Presets.OgreLow")
				{
					Modify = delegate(OgreStackSettings a)
					{
						a.Values[Category.SmallVolumeResource].Modify(MultiplierMode.Scalar, 15f, "15");
						a.Values[Category.Resource].Modify(MultiplierMode.Fixed, 500f, "500");
						a.Values[Category.RawFoodMeat].Modify(MultiplierMode.Fixed, 500f, "500");
						a.Values[Category.RawFoodPlant].Modify(MultiplierMode.Fixed, 500f, "500");
						a.Values[Category.PlantMatter].Modify(MultiplierMode.Fixed, 750f, "750");
						a.Values[Category.Meal].Modify(MultiplierMode.Fixed, 50f, "50");
						a.Values[Category.FoodForAnimal].Modify(MultiplierMode.Fixed, 1000f, "1000");
						a.Values[Category.Food].Modify(MultiplierMode.Fixed, 500f, "500");
						a.Values[Category.Item].Modify(MultiplierMode.Fixed, 5f, "5");
						a.Values[Category.BodyPartOrImplant].Modify(MultiplierMode.Fixed, 5f, "5");
						a.Values[Category.Leather].Modify(MultiplierMode.Fixed, 500f, "500");
						a.Values[Category.Textile].Modify(MultiplierMode.Fixed, 500f, "500");
						a.Values[Category.StoneBlock].Modify(MultiplierMode.Fixed, 1000f, "1000");
						a.Values[Category.Manufactured].Modify(MultiplierMode.Scalar, 5f, "5");
						a.Values[Category.Drug].Modify(MultiplierMode.Fixed, 1000f, "1000");
						a.Values[Category.Medicine].Modify(MultiplierMode.Fixed, 35f, "35");
						a.Values[Category.MortarShell].Modify(MultiplierMode.Fixed, 25f, "25");
						a.Values[Category.Artifact].Modify(MultiplierMode.Fixed, 5f, "5");
						a.Values[Category.Other].Modify(MultiplierMode.Scalar, 5f, "5");
					}
				}
			};
			_SCALAR_PRESETS = new List<Preset>();
			for (int num = 1; num < 11; num++)
			{
				int scaler = num;
				_SCALAR_PRESETS.Add(new Preset("OgreStack.Presets.Scalar." + num + "x")
				{
					Modify = delegate(OgreStackSettings a)
					{
						foreach (Category key2 in OgreStackMod._DEFAULTS.Keys)
						{
							float value = float.Parse(scaler.ToString());
							a.Values[key2].Modify(MultiplierMode.Scalar, value, scaler.ToString());
						}
					}
				});
			}
			_REFERENCE = new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase);
			foreach (Preset oGRE_PRESET in _OGRE_PRESETS)
			{
				_REFERENCE.Add(oGRE_PRESET.NameKey, oGRE_PRESET);
			}
			foreach (Preset sCALAR_PRESET in _SCALAR_PRESETS)
			{
				_REFERENCE.Add(sCALAR_PRESET.NameKey, sCALAR_PRESET);
			}
		}

		public static List<Preset> GetOgrePresets()
		{
			return _OGRE_PRESETS;
		}

		public static List<Preset> GetScalarPresets()
		{
			return _SCALAR_PRESETS;
		}

		public static Dictionary<string, Preset> GetPresetReferences()
		{
			return _REFERENCE;
		}
	}
	public class OgreStackSettings : ModSettings
	{
		private bool isLoaded = false;

		private Dictionary<Category, string> _preSettingsWindow = null;

		internal Action ReModify = null;

		internal Vector2 ScrollPosition = new Vector2(0f, 0f);

		public bool IsDebug;

		private Dictionary<Category, CategorySetting> _values = null;

		public Dictionary<Category, CategorySetting> Values
		{
			get
			{
				if (_values == null)
				{
					_values = new Dictionary<Category, CategorySetting>(OgreStackMod._DEFAULTS.Keys.Count);
					foreach (KeyValuePair<Category, CategorySetting> dEFAULT in OgreStackMod._DEFAULTS)
					{
						_values.Add(dEFAULT.Key, new CategorySetting(dEFAULT.Value.Mode, dEFAULT.Value.Value));
					}
				}
				return _values;
			}
		}

		internal void HashCurrentSettings()
		{
			_preSettingsWindow = new Dictionary<Category, string>();
			foreach (KeyValuePair<Category, CategorySetting> value in Values)
			{
				_preSettingsWindow.Add(value.Key, value.Value.ToString());
			}
		}

		public bool DetermineIfModifyStacksIsNeeded()
		{
			if (_preSettingsWindow == null)
			{
				return false;
			}
			foreach (KeyValuePair<Category, CategorySetting> value in Values)
			{
				string strA = _preSettingsWindow[value.Key];
				string strB = value.Value.ToString();
				if (string.Compare(strA, strB, ignoreCase: true) != 0)
				{
					return true;
				}
			}
			return false;
		}

		public override void ExposeData()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			if ((int)Scribe.mode == 1)
			{
				foreach (KeyValuePair<Category, CategorySetting> value2 in Values)
				{
					Category key = value2.Key;
					CategorySetting value = value2.Value;
					if (OgreStackMod._DEFAULTS[key].Mode != value.Mode || OgreStackMod._DEFAULTS[key].Value != value.Value)
					{
						string text = key.ToString() + "_";
						Scribe_Values.Look<MultiplierMode>(ref value.Mode, text + "Mode", MultiplierMode.Fixed, false);
						Scribe_Values.Look<string>(ref value.Buffer, text + "Value", (string)null, false);
					}
				}
				if (IsDebug)
				{
					Scribe_Values.Look<bool>(ref IsDebug, "IsDebugCSV", false, false);
				}
				if (DetermineIfModifyStacksIsNeeded())
				{
					ReModify();
				}
				HashCurrentSettings();
			}
			else
			{
				if (isLoaded)
				{
					return;
				}
				isLoaded = true;
				foreach (Category key2 in OgreStackMod._DEFAULTS.Keys)
				{
					string text2 = key2.ToString() + "_";
					MultiplierMode mode = MultiplierMode.Fixed;
					string text3 = "0";
					Scribe_Values.Look<MultiplierMode>(ref mode, text2 + "Mode", MultiplierMode.Fixed, false);
					Scribe_Values.Look<string>(ref text3, text2 + "Value", (string)null, false);
					if (!string.IsNullOrEmpty(text3))
					{
						Values[key2] = new CategorySetting(mode, text3);
						Values[key2].ParseBuffer();
					}
					else
					{
						Values[key2] = new CategorySetting(OgreStackMod._DEFAULTS[key2].Mode, OgreStackMod._DEFAULTS[key2].Value.ToString());
					}
				}
				Scribe_Values.Look<bool>(ref IsDebug, "IsDebugCSV", false, false);
				HashCurrentSettings();
			}
		}
	}
}
