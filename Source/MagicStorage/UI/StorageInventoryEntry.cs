using RimWorld;
using Verse;

namespace MagicStorage
{
    // Read-only adapter for vanilla sorter comparers. Our window owns rendering and
    // withdrawal state; no TradeSession or Tradeable is constructed.
    internal sealed class StorageInventoryEntry : TransferableOneWay
    {
        internal readonly StorageItemRow Row;
        internal StorageInventoryEntry(StorageItemRow row) { Row = row; things = row.Stacks; }
        public override Thing AnyThing => Row.DisplayThing;

        internal string Description()
        {
            Thing item = AnyThing;
            string description = item.DescriptionDetailed;
            string result = item is Book ? description : item.LabelCapNoCount.ToString();
            if (!(item is Book) && !description.NullOrEmpty()) result += ": " + description;
            if (item.ContentSource != null && !item.ContentSource.IsCoreMod)
                result += "\n\n" + ("Stat_Source_Label".Translate() + ": " + item.ContentSource.Name).Colorize(ColoredText.SubtleGrayColor);
            CompIngredients ingredients = item.TryGetComp<CompIngredients>();
            if (ingredients != null) result += "\n\n" + ingredients.CompInspectStringExtra();
            return result;
        }
    }
}
