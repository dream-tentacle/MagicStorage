namespace HaulersDream;

public static class MasterEnable
{
	public static bool Active
	{
		get
		{
			if (HaulersDreamMod.Settings != null)
			{
				return HaulersDreamMod.Settings.masterEnabled;
			}
			return true;
		}
	}
}
