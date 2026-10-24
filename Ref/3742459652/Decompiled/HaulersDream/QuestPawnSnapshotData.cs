using System;
using System.Collections.Generic;
using Verse;

namespace HaulersDream;

public class QuestPawnSnapshotData : IExposable
{
	public Pawn pawn;

	public List<QuestPawnSnapshotItem> items = new List<QuestPawnSnapshotItem>();

	public QuestPawnSnapshotData()
	{
	}

	public QuestPawnSnapshotData(Pawn pawn, List<QuestPawnSnapshotItem> items)
	{
		this.pawn = pawn;
		this.items = items;
	}

	public void ExposeData()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		Scribe_References.Look<Pawn>(ref pawn, "pawn", false);
		Scribe_Collections.Look<QuestPawnSnapshotItem>(ref items, "items", (LookMode)2, Array.Empty<object>());
		if ((int)Scribe.mode == 2 && items == null)
		{
			items = new List<QuestPawnSnapshotItem>();
		}
	}
}
