using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class Dialog_BatchOvershoot : Window
{
	private const int OvershootMin = 0;

	private const int OvershootMax = 200;

	private readonly Bill bill;

	private int overshoot;

	private readonly int initialOvershoot;

	private string overshootBuf;

	public override Vector2 InitialSize => new Vector2(420f, 210f);

	public Dialog_BatchOvershoot(Bill bill)
		: base((IWindowDrawing)null)
	{
		this.bill = bill;
		overshoot = Mathf.Clamp(Mathf.Max(0, HaulersDreamGameComponent.Instance?.BatchOvershootOf(bill) ?? 0), 0, 200);
		initialOvershoot = overshoot;
		base.forcePause = true;
		base.doCloseX = true;
		base.closeOnClickedOutside = true;
		base.absorbInputAroundWindow = true;
	}

	public override void DoWindowContents(Rect inRect)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		Listing_Standard val = new Listing_Standard();
		((Listing)val).Begin(inRect);
		Text.Font = (GameFont)2;
		RecipeDef recipe = bill.recipe;
		val.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.Batch.OvershootDialogTitle", NamedArgument.op_Implicit(((recipe == null) ? null : ((Def)(recipe.ProducedThingDef?)).label) ?? bill.LabelCap)), -1f, (string)null);
		Text.Font = (GameFont)1;
		((Listing)val).Gap(8f);
		val.Label((overshoot > 0) ? TranslatorFormattedStringExtensions.Translate("HaulersDream.Batch.OvershootLabel", NamedArgument.op_Implicit(overshoot)) : Translator.Translate("HaulersDream.Batch.OvershootLabelOff"), -1f, (string)null);
		Rect rect = ((Listing)val).GetRect(22f, 1f);
		((Listing)val).Gap(2f);
		HDNumberField.SplitRow(rect, out var sliderRect, out var boxRect);
		int num = Mathf.Clamp(overshoot, 0, 200);
		int num2 = Mathf.RoundToInt(Widgets.HorizontalSlider(sliderRect, (float)num, 0f, 200f, true, (string)null, (string)null, (string)null, -1f));
		if (num2 != num)
		{
			overshoot = num2;
			overshootBuf = null;
		}
		HDNumberField.Int(boxRect, "HD_BatchOvershoot", ref overshoot, ref overshootBuf, 0, 200, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Common.NumberBoxTip", NamedArgument.op_Implicit(0), NamedArgument.op_Implicit(200))));
		((Listing)val).Gap(6f);
		GUI.color = Color.gray;
		Text.Font = (GameFont)0;
		val.Label(Translator.Translate("HaulersDream.Batch.OvershootDesc"), -1f, (string)null);
		Text.Font = (GameFont)1;
		GUI.color = Color.white;
		((Listing)val).End();
	}

	public override void PreClose()
	{
		((Window)this).PreClose();
		overshoot = NumberEntryPolicy.ClampInt(overshoot, 0, 200);
		if (overshoot != initialOvershoot)
		{
			MultiplayerCompat.SetBillBatchOvershoot(bill, overshoot);
		}
	}
}
