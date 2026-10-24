using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HaulersDream;

public class LoadTransportersAdapter : IManagedLoadable
{
	private readonly CompTransporter primary;

	private readonly Map map;

	private readonly List<CompTransporter> group;

	public CompTransporter Primary => primary;

	public IReadOnlyList<CompTransporter> Group => group;

	public bool HasMassCap => true;

	public bool HandlesAbstractDemands => false;

	public LoadableKind Kind => LoadableKind.Transporter;

	private LoadTransportersAdapter(CompTransporter primary, Map map, List<CompTransporter> group)
	{
		this.primary = primary;
		this.map = map;
		this.group = group;
	}

	public static LoadTransportersAdapter TryCreate(CompTransporter transporter)
	{
		if (((ThingComp)(transporter?)).parent == null || !((Thing)((ThingComp)transporter).parent).Spawned)
		{
			return null;
		}
		Map val = transporter.Map;
		if (val == null)
		{
			return null;
		}
		List<CompTransporter> list = transporter.TransportersInGroup(val);
		List<CompTransporter> list2 = new List<CompTransporter>(list?.Count ?? 1);
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null)
				{
					list2.Add(list[i]);
				}
			}
		}
		if (list2.Count == 0)
		{
			list2.Add(transporter);
		}
		return new LoadTransportersAdapter(transporter, val, list2);
	}

	public int GetUniqueLoadID()
	{
		return primary.groupID;
	}

	public Map GetMap()
	{
		return map;
	}

	public Thing GetParentThing()
	{
		return (Thing)(object)((ThingComp)primary).parent;
	}

	public List<TransferableOneWay> GetTransferables()
	{
		List<TransferableOneWay> list = new List<TransferableOneWay>();
		for (int i = 0; i < group.Count; i++)
		{
			List<TransferableOneWay> list2 = group[i]?.leftToLoad;
			if (list2 == null)
			{
				continue;
			}
			for (int j = 0; j < list2.Count; j++)
			{
				TransferableOneWay val = list2[j];
				if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
				{
					list.Add(val);
				}
			}
		}
		return list;
	}

	public bool AnythingToLoad()
	{
		for (int i = 0; i < group.Count; i++)
		{
			List<TransferableOneWay> list = group[i]?.leftToLoad;
			if (list == null)
			{
				continue;
			}
			for (int j = 0; j < list.Count; j++)
			{
				TransferableOneWay val = list[j];
				if (val != null && ((Transferable)val).HasAnyThing && ((Transferable)val).CountToTransfer > 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	public ThingOwner GetInnerContainerFor(Thing depositTarget)
	{
		if (depositTarget?.def == null)
		{
			return primary.innerContainer;
		}
		if (WantsDef(primary, depositTarget.def))
		{
			return primary.innerContainer;
		}
		for (int i = 0; i < group.Count; i++)
		{
			CompTransporter val = group[i];
			if (val != null && val != primary && WantsDef(val, depositTarget.def))
			{
				return val.innerContainer;
			}
		}
		return primary.innerContainer;
	}

	public CompTransporter ActiveMemberFor(ThingDef def)
	{
		if (def == null)
		{
			return null;
		}
		if (WantsDef(primary, def))
		{
			return primary;
		}
		for (int i = 0; i < group.Count; i++)
		{
			if (group[i] != null && group[i] != primary && WantsDef(group[i], def))
			{
				return group[i];
			}
		}
		return null;
	}

	public CompTransporter ActiveMemberFor(Thing item)
	{
		if (item?.def == null)
		{
			return null;
		}
		if (WantsThing(primary, item))
		{
			return primary;
		}
		for (int i = 0; i < group.Count; i++)
		{
			if (group[i] != null && group[i] != primary && WantsThing(group[i], item))
			{
				return group[i];
			}
		}
		return null;
	}

	public static int MemberRemainingFor(CompTransporter member, Thing item)
	{
		List<TransferableOneWay> list = member?.leftToLoad;
		if (list == null || item?.def == null)
		{
			return 0;
		}
		TransferableOneWay obj = TransferableUtility.TransferableMatchingDesperate(item, list, (TransferAsOneMode)1);
		int num = ((obj != null) ? ((Transferable)obj).CountToTransfer : 0);
		if (num <= 0)
		{
			return 0;
		}
		return num;
	}

	private static bool WantsDef(CompTransporter t, ThingDef def)
	{
		List<TransferableOneWay> list = t?.leftToLoad;
		if (list == null)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			TransferableOneWay val = list[i];
			if (val != null && ((Transferable)val).CountToTransfer > 0 && ((Transferable)val).ThingDef == def)
			{
				return true;
			}
		}
		return false;
	}

	private static bool WantsThing(CompTransporter t, Thing item)
	{
		List<TransferableOneWay> list = t?.leftToLoad;
		if (list == null || item == null)
		{
			return false;
		}
		TransferableOneWay val = TransferableUtility.TransferableMatchingDesperate(item, list, (TransferAsOneMode)1);
		if (val != null)
		{
			return ((Transferable)val).CountToTransfer > 0;
		}
		return false;
	}

	public float GetMassCapacity()
	{
		float num = 0f;
		for (int i = 0; i < group.Count; i++)
		{
			if (group[i] != null)
			{
				num += group[i].MassCapacity;
			}
		}
		return num;
	}

	public float GetMassUsage()
	{
		float num = 0f;
		for (int i = 0; i < group.Count; i++)
		{
			if (group[i] != null)
			{
				num += group[i].MassUsage;
			}
		}
		return num;
	}
}
