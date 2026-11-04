using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    // 选中没有产物定义的配方时提示一次。有些 mod 的配方在代码里产出结果，
    // 定义上却没有 products，我们无法判断它能否在魔法存储里工作，所以只提示、不阻止。
    internal static class RecipeOutputNotice
    {
        // specialProducts（屠宰、熔炼、切石）是定义好的产出，不算“没有产物”。
        internal static bool HasDefinedOutput(RecipeDef recipe) =>
            recipe != null && (!recipe.products.NullOrEmpty() || !recipe.specialProducts.NullOrEmpty());

        internal static bool IsSilenced(string defName) =>
            !string.IsNullOrEmpty(defName) &&
            Current.Game?.GetComponent<GameComponent_RecipeNotices>()?.IsSilenced(defName) == true;

        internal static void Warn(RecipeDef recipe)
        {
            if (recipe == null || HasDefinedOutput(recipe) || IsSilenced(recipe.defName)) return;
            Dialog_RecipeOutputNotice.Warn(recipe);
        }
    }

    // 原版提示框样式：按钮 A 仅关闭，按钮 B 把当前提示涉及的配方全部记入存档后关闭。
    internal sealed class Dialog_RecipeOutputNotice : Dialog_MessageBox
    {
        private static readonly List<RecipeDef> pending = new List<RecipeDef>();
        private static Dialog_RecipeOutputNotice open;

        private Dialog_RecipeOutputNotice()
            : base("", "OK".Translate(), null, "MS_Craft_NoOutputSilence".Translate(), SilencePending,
                "MS_Craft_NoOutputTitle".Translate())
        {
            doCloseX = true;
        }

        internal static void Warn(RecipeDef recipe)
        {
            if (pending.Contains(recipe)) return;
            pending.Add(recipe);
            // 玩家可能在一次 Ctrl 连续添加中选中多个配方，此时只保留一个提示窗。
            if (open != null) return;
            open = new Dialog_RecipeOutputNotice();
            Find.WindowStack.Add(open);
        }

        private static void SilencePending()
        {
            var notices = Current.Game?.GetComponent<GameComponent_RecipeNotices>();
            if (notices == null) return;
            foreach (var recipe in pending) notices.Silence(recipe?.defName);
        }

        private static TaggedString Body()
        {
            TaggedString text = "MS_Craft_NoOutputBody".Translate();
            if (pending.Count <= 1) return text;
            return text + "\n\n" + string.Join("\n", pending.Select(recipe => "  • " + recipe.LabelCap.RawText));
        }

        public override void DoWindowContents(Rect inRect)
        {
            text = Body();
            base.DoWindowContents(inRect);
        }

        public override void PostClose()
        {
            if (open == this) open = null;
            pending.Clear();
            base.PostClose();
        }
    }
}
