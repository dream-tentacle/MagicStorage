using System;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class Dialog_NameProfile : Window
{
	private string curName = "";

	private readonly Action<string> onAccept;

	private bool focusedOnce;

	public override Vector2 InitialSize => new Vector2(380f, 168f);

	public Dialog_NameProfile(Action<string> onAccept, string initial = "")
		: base((IWindowDrawing)null)
	{
		this.onAccept = onAccept;
		curName = initial ?? "";
		base.forcePause = true;
		base.doCloseX = true;
		base.absorbInputAroundWindow = true;
		base.closeOnClickedOutside = true;
		base.draggable = true;
		base.closeOnAccept = false;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Invalid comparison between Unknown and I4
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Invalid comparison between Unknown and I4
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Invalid comparison between Unknown and I4
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		Text.Font = (GameFont)1;
		Widgets.Label(new Rect(0f, 0f, ((Rect)(ref inRect)).width, 28f), Translator.Translate("HaulersDream.Profile.NameDialogTitle"));
		GUI.SetNextControlName("HD_ProfileNameField");
		curName = Widgets.TextField(new Rect(0f, 36f, ((Rect)(ref inRect)).width, 32f), curName);
		if (!focusedOnce)
		{
			UI.FocusControl("HD_ProfileNameField", (Window)(object)this);
			focusedOnce = true;
		}
		bool flag = (int)Event.current.type == 4 && ((int)Event.current.keyCode == 13 || (int)Event.current.keyCode == 271);
		Rect val = new Rect(((Rect)(ref inRect)).width - 120f, ((Rect)(ref inRect)).height - 36f, 120f, 32f);
		if (Widgets.ButtonText(new Rect(((Rect)(ref inRect)).width - 248f, ((Rect)(ref inRect)).height - 36f, 120f, 32f), TaggedString.op_Implicit(Translator.Translate("CancelButton")), true, true, true, (TextAnchor?)null))
		{
			((Window)this).Close(true);
		}
		if ((Widgets.ButtonText(val, TaggedString.op_Implicit(Translator.Translate("OK")), true, true, true, (TextAnchor?)null) || flag) && !GenText.NullOrEmpty(curName.Trim()))
		{
			if (flag)
			{
				Event.current.Use();
			}
			onAccept?.Invoke(curName.Trim());
			((Window)this).Close(true);
		}
	}
}
