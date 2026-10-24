using Verse;

namespace HaulersDream;

public class QuestPawnSnapshotItem : IExposable
{
	public ThingDef def;

	public ThingDef stuff;

	public int count;

	public QuestPawnSnapshotItem()
	{
	}

	public QuestPawnSnapshotItem(ThingDef def, ThingDef stuff, int count)
	{
		this.def = def;
		this.stuff = stuff;
		this.count = count;
	}

	public void ExposeData()
	{
		Scribe_Defs.Look<ThingDef>(ref def, "def");
		Scribe_Defs.Look<ThingDef>(ref stuff, "stuff");
		Scribe_Values.Look<int>(ref count, "count", 0, false);
	}
}
