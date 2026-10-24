using System.Collections.Generic;
using System.Text;
using Verse;

namespace HaulersDream;

public static class ModReplacements
{
	public readonly struct Replaced(string name, params string[] packageIds)
	{
		public readonly string Name = name;

		public readonly string[] PackageIds = packageIds;
	}

	public static readonly Replaced[] All = new Replaced[10]
	{
		new Replaced("Pick Up And Haul", "Mehni.PickUpAndHaul"),
		new Replaced("Harvest and Haul", "laredson.harvestandhaul"),
		new Replaced("Auto Strip on Haul", "Fuu.AutoStripOnHaul"),
		new Replaced("Haul After Stripping", "Ilarion.HaulAfterStripping"),
		new Replaced("Everyone Hauls", "defi.everyonehauls", "assassinsbro.everyonehauls.unofficial"),
		new Replaced("Haul to Stack", "jkluch.HaulToStack", "com.jkluch.HaulToStack"),
		new Replaced("Bulk Load For Transporters", "Ilarion.BulkLoadForTransporters"),
		new Replaced("While You're Up", "codeoptimist.jobsofopportunity", "hoodie.whileyoureup", "kevlou127.WhileHYouOreHUpHQ1V0S"),
		new Replaced("Meals on Wheels", "Uuugggg.MealsOnWheels", "Memegoddess.MealsOnWheels"),
		new Replaced("Haul After Slaughter", "puremj.mjrimmods.vanillafixhaulafterslaughter")
	};

	private static List<ModMetaData> _active;

	public static List<ModMetaData> ActiveReplaced
	{
		get
		{
			if (_active != null)
			{
				return _active;
			}
			_active = new List<ModMetaData>();
			HashSet<string> hashSet = new HashSet<string>();
			Replaced[] all = All;
			for (int i = 0; i < all.Length; i++)
			{
				Replaced replaced = all[i];
				for (int j = 0; j < replaced.PackageIds.Length; j++)
				{
					ModMetaData activeModWithIdentifier = ModLister.GetActiveModWithIdentifier(replaced.PackageIds[j], true);
					if (activeModWithIdentifier != null && hashSet.Add(activeModWithIdentifier.PackageIdNonUnique))
					{
						_active.Add(activeModWithIdentifier);
					}
				}
			}
			string[] array = new string[All.Length];
			for (int k = 0; k < All.Length; k++)
			{
				array[k] = Norm(All[k].Name);
			}
			foreach (ModMetaData item in ModsConfig.ActiveModsInLoadOrder)
			{
				if (item == null || item.IsCoreMod || hashSet.Contains(item.PackageIdNonUnique) || item.SamePackageId("giwaffed.HaulersDream", true))
				{
					continue;
				}
				string text = Norm(item.Name) + " " + Norm(item.PackageId);
				for (int l = 0; l < array.Length; l++)
				{
					if (array[l].Length > 0 && text.Contains(array[l]))
					{
						if (hashSet.Add(item.PackageIdNonUnique))
						{
							_active.Add(item);
						}
						break;
					}
				}
			}
			return _active;
		}
	}

	public static List<string> ActiveNames
	{
		get
		{
			List<ModMetaData> activeReplaced = ActiveReplaced;
			List<string> list = new List<string>(activeReplaced.Count);
			for (int i = 0; i < activeReplaced.Count; i++)
			{
				list.Add(GenText.NullOrEmpty(activeReplaced[i].Name) ? activeReplaced[i].PackageId : activeReplaced[i].Name);
			}
			return list;
		}
	}

	public static bool AnyActive => ActiveReplaced.Count > 0;

	public static void DisableAllAndRestart()
	{
		List<ModMetaData> activeReplaced = ActiveReplaced;
		for (int i = 0; i < activeReplaced.Count; i++)
		{
			ModsConfig.SetActive(activeReplaced[i], false);
		}
		ModsConfig.Save();
		GenCommandLine.Restart();
	}

	private static string Norm(string s)
	{
		if (GenText.NullOrEmpty(s))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		foreach (char c in s)
		{
			if (c < '\u0080' && char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}
}
