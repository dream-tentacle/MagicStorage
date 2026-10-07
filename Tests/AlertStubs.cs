// Native alert rendering/discovery remain engine boundaries. Production report,
// label, explanation and culprit collection are linked into the tests.
using System.Collections.Generic;
using Verse;

namespace Verse
{
    public struct TaggedString
    {
        private string value;
        public static implicit operator TaggedString(string value) => new TaggedString { value = value };
        public override string ToString() => value;
    }
    public partial class ThingDef { public string label = "test item"; public string LabelCap => label; }
    public class MapParent { public string LabelCap => "test map"; }
    public partial class Map { public MapParent Parent = new MapParent(); }
}
namespace RimWorld
{
    public enum AlertPriority { Medium, High, Critical }
    public abstract class Alert
    {
        protected AlertPriority defaultPriority;
        public abstract AlertReport GetReport();
        public virtual string GetLabel() => "";
        public virtual TaggedString GetExplanation() => "";
    }
    public struct AlertReport
    {
        public bool active;
        public List<Thing> culpritsThings;
        public static AlertReport CulpritsAre(List<Thing> things) =>
            new AlertReport { active = things.Count > 0, culpritsThings = things };
    }
}
