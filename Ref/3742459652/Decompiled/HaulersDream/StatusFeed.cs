using System.Collections.Generic;
using HaulersDream.Core;

namespace HaulersDream;

internal class StatusFeed
{
	public string latestVersion;

	public long now;

	public List<ReportStatus> reports = new List<ReportStatus>();
}
