using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Match Dialog_BillConfig's target-count controls. Both the UI and counter use
    // these capabilities so an inapplicable (hidden) setting cannot exclude products.
    internal readonly struct CraftingProductFilters
    {
        internal readonly bool IncludeEquipped, IncludeTainted, HitPoints, Quality, AllowedStuff;

        internal CraftingProductFilters(RecipeDef recipe)
        {
            ThingDef product = recipe?.ProducedThingDef;
            IncludeEquipped = product != null && (product.IsWeapon || product.IsApparel);
            IncludeTainted = product != null && product.IsApparel && product.apparel?.careIfWornByCorpse == true;
            HitPoints = product != null && recipe.products.Any(p => p.thingDef.useHitPoints);
            Quality = product != null && product.HasComp(typeof(CompQuality));
            AllowedStuff = product != null && product.MadeFromStuff;
        }
    }
}
