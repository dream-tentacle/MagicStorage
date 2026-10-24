using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class Dialog_CopyProfile : Window
{
	private readonly string token;

	public override Vector2 InitialSize => new Vector2(520f, 320f);

	public Dialog_CopyProfile(string token)
		: base((IWindowDrawing)null)
	{
		this.token = token ?? "";
		base.forcePause = true;
		base.doCloseX = true;
		base.absorbInputAroundWindow = true;
		base.closeOnClickedOutside = true;
		base.draggable = true;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		Text.Font = (GameFont)1;
		Widgets.Label(new Rect(0f, 0f, ((Rect)(ref inRect)).width, 28f), Translator.Translate("HaulersDream.Profile.CopyTitle"));
		GameFont font = Text.Font;
		Color color = GUI.color;
		Text.Font = (GameFont)0;
		GUI.color = new Color(0.72f, 0.72f, 0.76f);
		Widgets.Label(new Rect(0f, 30f, ((Rect)(ref inRect)).width, 44f), Translator.Translate("HaulersDream.Profile.CopyInfo"));
		GUI.color = color;
		Text.Font = font;
		Widgets.TextArea(new Rect(0f, 78f, ((Rect)(ref inRect)).width, ((Rect)(ref inRect)).height - 78f - 44f), token, true);
		Rect val = new Rect(0f, ((Rect)(ref inRect)).height - 36f, 220f, 32f);
		Rect val2 = default(Rect);
		((Rect)(ref val2))._002Ector(((Rect)(ref inRect)).width - 120f, ((Rect)(ref inRect)).height - 36f, 120f, 32f);
		if (Widgets.ButtonText(val, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Profile.CopyToClipboard")), true, true, true, (TextAnchor?)null))
		{
			GUIUtility.systemCopyBuffer = token;
			Messages.Message(TaggedString.op_Implicit(Translator.Translate("HaulersDream.Profile.CopiedMsg")), MessageTypeDefOf.TaskCompletion, false);
		}
		if (Widgets.ButtonText(val2, TaggedString.op_Implicit(Translator.Translate("CloseButton")), true, true, true, (TextAnchor?)null))
		{
			((Window)this).Close(true);
		}
	}
}
