using System.Collections.Generic;
using UnityEngine;

namespace HaulersDream;

public sealed class SettingsCtx
{
	public readonly float Width;

	public float CurY;

	public bool Collecting;

	public List<OptionEntry> Sink;

	public int CurrentCatId;

	public string CurrentHeader;

	public int Ordinal;

	public HashSet<int> RenderOrdinals;

	public SettingsCtx(float width)
	{
		Width = width;
		CurY = 0f;
	}

	public Rect Row(float h, float indent = 0f)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Rect result = new Rect(indent, CurY, Width - indent, h);
		CurY += h;
		return result;
	}

	public void Gap(float h = 8f)
	{
		CurY += h;
	}
}
