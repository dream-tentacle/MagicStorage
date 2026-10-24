using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace HaulersDream;

public static class ReportScreenshots
{
	public static string EnsureFolder()
	{
		return GenFilePaths.ScreenshotFolderPath;
	}

	public static List<ScreenshotEntry> FindRecent(int max = 120)
	{
		List<ScreenshotEntry> list = SteamScreenshots.FindRecent(max);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < list.Count; i++)
		{
			hashSet.Add(list[i].fullPath);
		}
		string path = EnsureFolder();
		if (Directory.Exists(path))
		{
			string[] files = Directory.GetFiles(path);
			foreach (string text in files)
			{
				if (IsAttachableImage(text) && hashSet.Add(text))
				{
					list.Add(new ScreenshotEntry
					{
						fullPath = text,
						thumbPath = text,
						name = Path.GetFileName(text),
						modified = File.GetLastWriteTime(text)
					});
				}
			}
		}
		return list.OrderByDescending((ScreenshotEntry e) => e.modified).Take(Mathf.Max(0, max)).ToList();
	}

	public static bool TryOpenFolder()
	{
		try
		{
			Application.OpenURL(EnsureFolder());
			return true;
		}
		catch (Exception ex)
		{
			HDLog.Warn("could not open RimWorld's screenshot folder: " + ex);
			return false;
		}
	}

	private static bool IsAttachableImage(string path)
	{
		switch (Path.GetExtension(path ?? string.Empty).ToLowerInvariant())
		{
		case ".png":
		case ".jpg":
		case ".jpeg":
			return true;
		default:
			return false;
		}
	}
}
