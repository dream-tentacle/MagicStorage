using HaulersDream.Core;

namespace HaulersDream;

public sealed class QuarantinedWork
{
	public readonly string Work;

	public readonly QuarantineNaming Naming;

	public readonly string Source;

	public QuarantinedWork(string work, QuarantineNaming naming, string source)
	{
		Work = work;
		Naming = naming;
		Source = source;
	}
}
