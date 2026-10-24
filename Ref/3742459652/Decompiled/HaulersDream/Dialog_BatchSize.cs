using HaulersDream.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulersDream;

public class Dialog_BatchSize : Window
{
	private const int SizeMin = 1;

	private const int SizeMax = 200;

	private readonly Bill bill;

	private int size;

	private readonly int initialSize;

	private string sizeBuf;

	public override Vector2 InitialSize => new Vector2(420f, 210f);

	public Dialog_BatchSize(Bill bill)
		: base((IWindowDrawing)null)
	{
		this.bill = bill;
		size = Mathf.Clamp(Mathf.Max(1, HaulersDreamGameComponent.Instance?.BatchSizeOf(bill) ?? 10), 1, 200);
		initialSize = size;
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
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		Listing_Standard val = new Listing_Standard();
		((Listing)val).Begin(inRect);
		Text.Font = (GameFont)2;
		RecipeDef recipe = bill.recipe;
		val.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.Batch.SizeDialogTitle", NamedArgument.op_Implicit(((recipe == null) ? null : ((Def)(recipe.ProducedThingDef?)).label) ?? bill.LabelCap)), -1f, (string)null);
		Text.Font = (GameFont)1;
		((Listing)val).Gap(8f);
		val.Label(TranslatorFormattedStringExtensions.Translate("HaulersDream.Batch.SizeLabel", NamedArgument.op_Implicit(size)), -1f, (string)null);
		Rect rect = ((Listing)val).GetRect(22f, 1f);
		((Listing)val).Gap(2f);
		HDNumberField.SplitRow(rect, out var sliderRect, out var boxRect);
		int num = Mathf.Clamp(size, 1, 200);
		int num2 = Mathf.RoundToInt(Widgets.HorizontalSlider(sliderRect, (float)num, 1f, 200f, true, (string)null, (string)null, (string)null, -1f));
		if (num2 != num)
		{
			size = num2;
			sizeBuf = null;
		}
		HDNumberField.Int(boxRect, "HD_BatchSize", ref size, ref sizeBuf, 1, 200, TaggedString.op_Implicit(TranslatorFormattedStringExtensions.Translate("HaulersDream.Common.NumberBoxTip", NamedArgument.op_Implicit(1), NamedArgument.op_Implicit(200))));
		((Listing)val).Gap(6f);
		GUI.color = Color.gray;
		Text.Font = (GameFont)0;
		val.Label(Translator.Translate("HaulersDream.Batch.SizeDesc"), -1f, (string)null);
		Text.Font = (GameFont)1;
		GUI.color = Color.white;
		((Listing)val).End();
	}

	public override void PreClose()
	{
		((Window)this).PreClose();
		size = NumberEntryPolicy.ClampInt(size, 1, 200);
		if (size != initialSize)
		{
			MultiplayerCompat.SetBillBatch(bill, on: true, size);
		}
	}
}
