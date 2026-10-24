using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using UnityEngine;

namespace HaulersDream;

public static class SteamScreenshots
{
	private const string RimWorldAppId = "294100";

	public static List<ScreenshotEntry> FindRecent(int max = 120)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<ScreenshotEntry> list = new List<ScreenshotEntry>();
		foreach (string item in SteamRoots())
		{
			string path = Path.Combine(item, "userdata");
			if (!Directory.Exists(path))
			{
				continue;
			}
			string[] directories = Directory.GetDirectories(path);
			foreach (string text in directories)
			{
				string text2 = Path.Combine(text, "760", "remote", "294100", "screenshots");
				if (!Directory.Exists(text2))
				{
					continue;
				}
				string path2 = Path.Combine(text2, "thumbnails");
				string[] files = Directory.GetFiles(text2, "*.jpg");
				foreach (string text3 in files)
				{
					if (hashSet.Add(text3))
					{
						string fileName = Path.GetFileName(text3);
						string text4 = Path.Combine(path2, fileName);
						list.Add(new ScreenshotEntry
						{
							fullPath = text3,
							thumbPath = (File.Exists(text4) ? text4 : text3),
							name = fileName,
							modified = File.GetLastWriteTime(text3)
						});
					}
				}
			}
		}
		return list.OrderByDescending((ScreenshotEntry e) => e.modified).Take(Mathf.Max(0, max)).ToList();
	}

	private static IEnumerable<string> SteamRoots()
	{
		if ((int)Application.platform == 2)
		{
			string text = null;
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
			{
				text = registryKey?.GetValue("SteamPath") as string;
			}
			if (!string.IsNullOrEmpty(text) && Directory.Exists(text))
			{
				yield return text;
			}
		}
		for (DirectoryInfo directoryInfo = SafeDir(Application.dataPath); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (Directory.Exists(Path.Combine(directoryInfo.FullName, "userdata")))
			{
				yield return directoryInfo.FullName;
				break;
			}
		}
	}

	private static DirectoryInfo SafeDir(string path)
	{
		if (!string.IsNullOrEmpty(path))
		{
			return new DirectoryInfo(path);
		}
		return null;
	}
}
