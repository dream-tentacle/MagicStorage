using UnityEngine;
using Verse;

namespace HaulersDream;

public class Dialog_PasteProfile : Window
{
	private readonly HaulersDreamSettings settings;

	private string token = "";

	private string profileName = "";

	private bool nameEdited;

	private string lastParsed;

	private bool tokenValid;

	private Vector2 scroll;

	public override Vector2 InitialSize => new Vector2(520f, 380f);

	public Dialog_PasteProfile(HaulersDreamSettings settings)
		: base((IWindowDrawing)null)
	{
		this.settings = settings;
		base.forcePause = true;
		base.doCloseX = true;
		base.absorbInputAroundWindow = true;
		base.closeOnClickedOutside = true;
		base.draggable = true;
		string systemCopyBuffer = GUIUtility.systemCopyBuffer;
		if (!string.IsNullOrEmpty(systemCopyBuffer) && systemCopyBuffer.TrimStart().StartsWith("HDP"))
		{
			token = systemCopyBuffer.Trim();
		}
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_021d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		Text.Font = (GameFont)1;
		float num = 0f;
		Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 28f), Translator.Translate("HaulersDream.Profile.PasteTitle"));
		num += 32f;
		Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 22f), Translator.Translate("HaulersDream.Profile.PasteTokenLabel"));
		num += 24f;
		token = WidgetsCompat.TextAreaScrollable(new Rect(0f, num, ((Rect)(ref inRect)).width, 96f), token, ref scroll);
		num += 100f;
		if (Widgets.ButtonText(new Rect(0f, num, 220f, 28f), TaggedString.op_Implicit(Translator.Translate("HaulersDream.Profile.PasteFromClipboard")), true, true, true, (TextAnchor?)null))
		{
			token = (GUIUtility.systemCopyBuffer ?? "").Trim();
			nameEdited = false;
		}
		num += 34f;
		if (token != lastParsed)
		{
			lastParsed = token;
			tokenValid = settings.TryImportProfileFromString(token, out var name, out var _);
			if (tokenValid && !nameEdited)
			{
				profileName = name ?? "";
			}
		}
		Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 22f), Translator.Translate("HaulersDream.Profile.NameDialogTitle"));
		num += 24f;
		string text = Widgets.TextField(new Rect(0f, num, ((Rect)(ref inRect)).width, 30f), profileName);
		if (text != profileName)
		{
			profileName = text;
			nameEdited = true;
		}
		num += 34f;
		if (!GenText.NullOrEmpty(token) && !tokenValid)
		{
			GameFont font = Text.Font;
			Color color = GUI.color;
			Text.Font = (GameFont)0;
			GUI.color = new Color(0.9f, 0.5f, 0.5f);
			Widgets.Label(new Rect(0f, num, ((Rect)(ref inRect)).width, 20f), Translator.Translate("HaulersDream.Profile.PasteInvalid"));
			GUI.color = color;
			Text.Font = font;
		}
		Rect val = new Rect(((Rect)(ref inRect)).width - 150f, ((Rect)(ref inRect)).height - 36f, 150f, 32f);
		if (Widgets.ButtonText(new Rect(((Rect)(ref inRect)).width - 280f, ((Rect)(ref inRect)).height - 36f, 120f, 32f), TaggedString.op_Implicit(Translator.Translate("CancelButton")), true, true, true, (TextAnchor?)null))
		{
			((Window)this).Close(true);
		}
		bool flag = tokenValid && !GenText.NullOrEmpty(profileName.Trim());
		if (Widgets.ButtonText(val, TaggedString.op_Implicit(Translator.Translate("HaulersDream.Profile.PasteCreate")), true, true, flag, (TextAnchor?)null) && flag && settings.TryImportProfileFromString(token, out var _, out var snapshot2))
		{
			settings.ImportAsProfile(profileName.Trim(), snapshot2);
			((Window)this).Close(true);
		}
	}
}
