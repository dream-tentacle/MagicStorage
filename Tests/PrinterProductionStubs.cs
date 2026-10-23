// Controlled game object boundary. Tests link the actual printer product generator.
using System;
using System.Collections.Generic;
using RimWorld;

namespace MagicStorage
{
    public sealed class Building_CosmicPrinter { public CosmicPrinterSettings Settings = new CosmicPrinterSettings(); }
}
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public enum LoadSaveMode { Inactive, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Values { public static void Look<T>(ref T value, string key, T fallback) { } }
    public static class ModsConfig { public static bool IdeologyActive; }
    public static class Rand
    {
        public static float Value => .5f;
        public static bool Chance(float chance) => chance > .5f;
    }
    public interface IStrippable { bool AnythingToStrip(); void Strip(); }
    public enum SpecialProductType { Butchery, Smelted, StoneBlocks }
    public sealed class ThingDef
    {
        public bool MadeFromStuff, Minifiable, Quality;
        public int stackLimit = 75;
        public List<StyleChance> randomStyle;
        public float randomStyleChance;
    }
    public sealed class StyleChance { public float Chance = 1; public ThingStyleDef StyleDef; }
    public sealed class ThingDefCountClass { public ThingDef thingDef; public int count; }
    public sealed class RecipeDef
    {
        public bool autoStripCorpses, useIngredientsForColor;
        public List<ThingDefCountClass> products = new List<ThingDefCountClass>();
        public List<SpecialProductType> specialProducts;
    }
    public class Thing
    {
        public ThingDef def, Stuff;
        public int stackCount = 1, DrawColor = 17;
        public ThingStyleDef StyleDef;
        public CompQuality Quality;
        public CompIngredients Ingredients = new CompIngredients();
        public float LastEfficiency;
        public int ButcherCalls, SmeltCalls, StoneCalls;
        public T TryGetComp<T>() where T : class => (Quality as T) ?? (Ingredients as T);
        public void SetColor(int color, bool reportFailure) { DrawColor = color; }
        public void Notify_RecipeProduced(object pawn) { throw new Exception("Machine invoked pawn-only callbacks"); }
        public void SetStyleDef(ThingStyleDef style) { StyleDef = style; }
        public virtual IEnumerable<Thing> ButcherProducts(object worker, float efficiency)
        { ButcherCalls++; LastEfficiency = efficiency; yield return new Thing { def = new ThingDef() }; }
        public IEnumerable<Thing> SmeltProducts(float efficiency)
        { SmeltCalls++; LastEfficiency = efficiency; yield return new Thing { def = new ThingDef() }; }
        public IEnumerable<Thing> StoneBlockProducts(object worker, float efficiency)
        { StoneCalls++; LastEfficiency = efficiency; yield return new Thing { def = new ThingDef() }; }
        public Thing MakeMinified()
        {
            if (stackCount != 1) throw new Exception("Minification would discard extra furniture");
            return new MinifiedThing { InnerThing = this };
        }
    }
    public sealed class MinifiedThing : Thing { public Thing InnerThing; }
    public sealed class Corpse : Thing, IStrippable
    {
        public Thing InnerPawn = new Thing();
        public int Strips;
        public bool AnythingToStrip() => true;
        public void Strip() { Strips++; }
        public override IEnumerable<Thing> ButcherProducts(object worker, float efficiency)
        { throw new Exception("Corpse butcher entry requires a real pawn"); }
    }
    public static class ThingMaker
    {
        public static Thing MakeThing(ThingDef def, ThingDef stuff = null) => new Thing
        { def = def, Stuff = stuff, Quality = def.Quality ? new CompQuality() : null };
    }
    public static class ListHelpers
    {
        public static bool NullOrEmpty<T>(this List<T> list) => list == null || list.Count == 0;
        public static T RandomElementByWeight<T>(this List<T> list, Func<T, float> weight) => list[0];
    }
}
namespace RimWorld
{
    public enum QualityCategory { Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary }
    public enum ArtGenerationContext { Colony }
    public sealed class ThingStyleDef { }
    public sealed class CompQuality
    {
        public QualityCategory Quality;
        public void SetQuality(QualityCategory quality, ArtGenerationContext source) { Quality = quality; }
    }
    public sealed class CompIngredients
    {
        public List<Verse.ThingDef> Registered = new List<Verse.ThingDef>();
        public void RegisterIngredient(Verse.ThingDef def) { Registered.Add(def); }
    }
    public static class QualityUtility
    { public static QualityCategory GenerateQualityCreatedByPawn(int skill, bool inspired) => QualityCategory.Normal; }
    public sealed class Ideo
    {
        public ThingStyleDef Style = new ThingStyleDef();
        public ThingStyleDef GetStyleFor(Verse.ThingDef def) => Style;
    }
    public sealed class FactionIdeos { public Ideo PrimaryIdeo = new Ideo(); }
    public sealed class Faction
    { public static Faction OfPlayer = new Faction(); public FactionIdeos ideos = new FactionIdeos(); }
}
