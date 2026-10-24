using UnityEngine;

namespace RavenRace.Features.FusangOrganization.UI;

public abstract class RadioPage
{
	protected readonly RadioSession Session;

	public virtual bool Pause => false;

	public abstract string Title { get; }

	protected RadioPage(RadioSession session)
	{
		Session = session;
	}

	public virtual void Enter()
	{
	}

	public virtual bool TryBack()
	{
		return false;
	}

	public abstract void Draw(Rect rect);
}
