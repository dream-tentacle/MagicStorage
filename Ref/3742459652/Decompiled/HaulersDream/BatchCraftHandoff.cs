using System.Collections.Generic;
using Verse.AI;

namespace HaulersDream;

public static class BatchCraftHandoff
{
	private static readonly Dictionary<Job, CraftBatchPlan> pending;

	static BatchCraftHandoff()
	{
		pending = new Dictionary<Job, CraftBatchPlan>();
		CacheRegistry.Register(Clear);
	}

	public static void Set(Job job, CraftBatchPlan plan)
	{
		pending[job] = plan;
	}

	public static void Clear()
	{
		pending.Clear();
	}

	public static CraftBatchPlan Consume(Job job)
	{
		if (job != null && pending.TryGetValue(job, out var value))
		{
			pending.Remove(job);
			return value;
		}
		return null;
	}
}
