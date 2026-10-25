// Native UI and knowledge boundaries for the production orbital menu/job provider.
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Verse
{
    public partial class Map { public readonly OrbitalGameConditions gameConditionManager = new OrbitalGameConditions(); }
    public sealed class OrbitalGameConditions
    {
        public bool Disabled;
        public bool ElectricityDisabled(Map map) => Disabled;
    }
    public static class OrbitalNamedBoundary
    {
        public static Pawn Named(this Pawn pawn, string name) => pawn;
    }
}
namespace RimWorld
{
    public abstract class FloatMenuOptionProvider
    {
        protected virtual bool Drafted => false;
        protected virtual bool Undrafted => false;
        protected virtual bool Multiselect => false;
        public virtual IEnumerable<FloatMenuOption> GetOptionsFor(Thing thing, FloatMenuContext context) { yield break; }
    }
    public sealed class FloatMenuContext
    {
        public Map map;
        public Pawn FirstSelectedPawn;
    }
    public enum KnowledgeAmount { Total }
    public static class ConceptDefOf { public static readonly object OpeningComms = new object(); }
    public static class PlayerKnowledgeDatabase { public static void KnowledgeDemonstrated(object concept, KnowledgeAmount amount) { } }
    public static class LetterDefOf { public static readonly object NeutralEvent = new object(); }
    public static class PawnRelationUtility
    {
        public static void Notify_PawnsSeenByPlayer_Letter_Send(IEnumerable<Pawn> pawns, string text, object letter) { }
    }
}
namespace Verse.AI
{
    public partial class Job { public ICommunicable commTarget; }
}
namespace MagicStorage
{
    internal static class StorageTradeDefOf
    {
        public static readonly Verse.AI.JobDef MS_TradeWithStorageOrbital = new Verse.AI.JobDef();
    }
}
