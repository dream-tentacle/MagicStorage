using System.Collections.Generic;

namespace HaulersDream;

internal class ReportThread
{
	public string id;

	public string type;

	public string comment;

	public double createdAt;

	public string status;

	public double issueNumber;

	public string url;

	public bool commentsUnavailable;

	public List<IssueComment> comments = new List<IssueComment>();
}
