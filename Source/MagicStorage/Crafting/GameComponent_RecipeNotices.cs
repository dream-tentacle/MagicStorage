using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // 玩家在配方提示里选过“不再提示”的配方，随存档保存。
    // 只存 defName：中途卸载某个 mod 后，这里只会留下无害的孤立字符串，不会产生坏引用。
    public sealed class GameComponent_RecipeNotices : GameComponent
    {
        private HashSet<string> silenced = new HashSet<string>();

        public GameComponent_RecipeNotices(Game game) { }

        internal bool IsSilenced(string defName) =>
            !string.IsNullOrEmpty(defName) && silenced.Contains(defName);

        internal void Silence(string defName)
        {
            if (!string.IsNullOrEmpty(defName)) silenced.Add(defName);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref silenced, "silencedRecipeOutputNotices", LookMode.Value);
            if (silenced == null) silenced = new HashSet<string>();
        }
    }
}
