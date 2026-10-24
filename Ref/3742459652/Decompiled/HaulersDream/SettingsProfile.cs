using System;
using Verse;

namespace HaulersDream;

public class SettingsProfile : IExposable
{
	public string name;

	public HaulersDreamSettings snapshot;

	public SettingsProfile()
	{
	}

	public SettingsProfile(string name, HaulersDreamSettings snapshot)
	{
		this.name = name;
		this.snapshot = snapshot;
	}

	public void ExposeData()
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Invalid comparison between Unknown and I4
		Scribe_Values.Look<string>(ref name, "name", (string)null, false);
		bool serializingSnapshot = HaulersDreamSettings.SerializingSnapshot;
		HaulersDreamSettings.SerializingSnapshot = true;
		try
		{
			Scribe_Deep.Look<HaulersDreamSettings>(ref snapshot, "snapshot", Array.Empty<object>());
		}
		finally
		{
			HaulersDreamSettings.SerializingSnapshot = serializingSnapshot;
		}
		if ((int)Scribe.mode == 2 && snapshot == null)
		{
			snapshot = new HaulersDreamSettings();
		}
	}
}
