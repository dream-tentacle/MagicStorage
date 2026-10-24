using System;
using System.Linq;
using RavenRace.Features.CentralHub.UI;
using RavenRace.Features.FusangOrganization.Live2D;
using RavenRace.Features.FusangOrganization.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RavenRace;

public sealed class Dialog_FusangComm : FusangTerminalWindow
{
	private const float ScreenMargin = 16f;

	private const float LetterAreaWidth = 96f;

	private readonly RadioSession session;

	private readonly RadioShell shell = new RadioShell();

	private readonly RadioPortrait portrait = new RadioPortrait();

	private readonly RavenCentralUIAnimationState animation = new RavenCentralUIAnimationState();

	private bool opened;

	private bool transitioning;

	private bool pageChanged;

	private bool disposed;

	public override Vector2 InitialSize => new Vector2(Mathf.Min(1600f, (float)UI.screenWidth - 16f - 96f), Mathf.Min(960f, (float)UI.screenHeight - 64f));

	public Dialog_FusangComm(Thing radioSource)
	{
		session = new RadioSession(this, radioSource);
		((Window)this).closeOnClickedOutside = false;
		((Window)this).closeOnAccept = false;
		((Window)this).absorbInputAroundWindow = true;
		((Window)this).draggable = true;
		((Window)this).preventCameraMotion = true;
	}

	protected override void SetInitialSizeAndPosition()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		Vector2 initialSize = ((Window)this).InitialSize;
		float num = (float)UI.screenWidth - 16f - 96f;
		((Window)this).windowRect = GenUI.Rounded(new Rect(16f + (num - initialSize.x) * 0.5f, ((float)UI.screenHeight - initialSize.y) * 0.5f, initialSize.x, initialSize.y));
	}

	public static void Open(Thing radio, RadioPageId page = RadioPageId.Home)
	{
		Dialog_FusangComm dialog_FusangComm = Find.WindowStack.Windows.OfType<Dialog_FusangComm>().FirstOrDefault();
		if (dialog_FusangComm == null)
		{
			dialog_FusangComm = new Dialog_FusangComm(radio);
			Find.WindowStack.Add((Window)(object)dialog_FusangComm);
		}
		dialog_FusangComm.session.Navigate(page);
	}

	public override void PostOpen()
	{
		((Window)this).PostOpen();
		if (!opened)
		{
			opened = true;
			session.Dialogue.Subscribe();
			animation.PlayOpen();
		}
	}

	public override void WindowUpdate()
	{
		base.WindowUpdate();
		session.Dialogue.Update();
		if (transitioning && pageChanged && animation.TransitionProgress <= 0f)
		{
			transitioning = false;
		}
		if (!Find.WindowStack.GetsInput((Window)(object)this))
		{
			portrait.Release();
		}
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		GameFont font = Text.Font;
		TextAnchor anchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		bool enabled = GUI.enabled;
		Color color = GUI.color;
		Matrix4x4 matrix = GUI.matrix;
		GUISkin skin = GUI.skin;
		try
		{
			Text.Font = (GameFont)1;
			Text.Anchor = (TextAnchor)0;
			Text.WordWrap = true;
			GUI.color = Color.white;
			GUI.enabled = enabled && !transitioning && Find.WindowStack.GetsInput((Window)(object)this);
			RavenCentralUIStyle.DrawBackground(inRect, animation.WindowAlpha);
			shell.Draw(GenUI.ContractedBy(inRect, 18f), session, portrait);
			RavenCentralUIShaderRenderer.DrawTransition(inRect, animation.TransitionProgress);
		}
		finally
		{
			Text.Font = font;
			Text.Anchor = anchor;
			Text.WordWrap = wordWrap;
			GUI.color = color;
			GUI.matrix = matrix;
			GUI.skin = skin;
			GUI.enabled = enabled;
		}
	}

	public override void OnCancelKeyPressed()
	{
		if (!transitioning)
		{
			session.Back();
		}
		Event current = Event.current;
		if (current != null)
		{
			current.Use();
		}
	}

	public void Transition(Action change)
	{
		if (transitioning || disposed)
		{
			return;
		}
		transitioning = true;
		pageChanged = false;
		portrait.Release();
		animation.PlayPageTransition(delegate
		{
			if (!disposed)
			{
				change();
			}
			pageChanged = true;
		});
	}

	public void SelectCell(Action<LocalTargetInfo> selected)
	{
		if (session.Map == null)
		{
			session.Report(session.MapReason);
			return;
		}
		Map map = session.Map;
		Current.Game.CurrentMap = map;
		portrait.Release();
		((Window)this).Close(true);
		Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), (Action<LocalTargetInfo>)delegate(LocalTargetInfo target)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			if (Find.CurrentMap == map && ((LocalTargetInfo)(ref target)).IsValid && GenGrid.InBounds(((LocalTargetInfo)(ref target)).Cell, map))
			{
				selected(target);
			}
			else
			{
				Messages.Message("选点无效，未消耗资源。", MessageTypeDefOf.RejectInput, false);
			}
		}, (Pawn)null, (Action)delegate
		{
		}, (Texture2D)null, true);
	}

	public void SelectPawn(Action<Pawn> selected)
	{
		if (session.Map == null)
		{
			session.Report(session.MapReason);
			return;
		}
		Map map = session.Map;
		Current.Game.CurrentMap = map;
		portrait.Release();
		((Window)this).Close(true);
		Find.Targeter.BeginTargeting(TargetingParameters.ForPawns(), (Action<LocalTargetInfo>)delegate(LocalTargetInfo target)
		{
			if (Find.CurrentMap == map && ((LocalTargetInfo)(ref target)).Pawn != null)
			{
				selected(((LocalTargetInfo)(ref target)).Pawn);
			}
			else
			{
				Messages.Message("选人无效，未消耗声望。", MessageTypeDefOf.RejectInput, false);
			}
		}, (Pawn)null, (Action)null, (Texture2D)null, true);
	}

	public override void PostClose()
	{
		((Window)this).PostClose();
		disposed = true;
		portrait.Release();
		session.Dialogue.Unsubscribe();
		animation.Dispose();
		ZuoYaoRadioLive2D.ResetScene();
	}
}
