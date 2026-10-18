using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageSelectionTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }

    public static int Main()
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(typeof(StorageSelectionPatches).TypeHandle);
            foreach (TestContainer holder in new TestContainer[]
                { new Building_StorageUnit(), new Building_StorageCore(), new Building_TransportSupplier(), new Building_StorageApparelAdapter() })
                StorageRules(holder);
            UnrelatedContainers();
            Console.WriteLine("PASS: " + checks + " selection assertions (production Harmony prefix executed; native iterator boundaries stubbed).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void StorageRules(TestContainer holder)
    {
        var item = new Thing(); var pawn = new Pawn();
        for (int i = 0; i < 10000; i++) holder.Contents.Add(new Thing());
        holder.Contents.Add(item); holder.Contents.Add(pawn);
        ContainingSelectionUtility.Visits = 0;
        for (int i = 0; i < 10; i++)
            Equal(0, ContainingSelectionUtility.SelectableContainedThings(holder).Count(), "repeated target queries return no storage contents");
        Equal(0, holder.Reads, "disabled selection never accesses container inventory");
        Equal(0, ContainingSelectionUtility.Visits, "ten thousand stacks never inspected");
        Equal(10002, holder.Contents.Count, "selection filtering preserves physical inventory");

        holder.Contents.Clear();
        holder.Contents.Add(item); holder.Contents.Add(pawn);
        holder.Contents.Add(new Thing { def = new ThingDef { selectable = false } });
        holder.def.containedItemsSelectable = true;
        Equal(item, ContainingSelectionUtility.SelectableContainedThings(holder).Single(), "enabling item selection preserves vanilla rules");
        holder.def.containedItemsSelectable = false; holder.def.containedPawnsSelectable = true;
        Equal(pawn, ContainingSelectionUtility.SelectableContainedThings(holder).Single(), "enabling pawn selection preserves vanilla rules");
        holder.def.containedItemsSelectable = true;
        Equal(2, ContainingSelectionUtility.SelectableContainedThings(holder).Count(), "both selection flags preserve native selectable filtering");
        holder.PlayerOwned = false;
        Equal(0, ContainingSelectionUtility.SelectableContainedThings(holder).Count(), "enabled selection still respects native faction restriction");
        holder.PlayerOwned = true;
        holder.def.containedItemsSelectable = holder.def.containedPawnsSelectable = false;
        holder.Reads = 0;
        Equal(0, ContainingSelectionUtility.SelectableContainedThings(holder).Count(), "turning selection off reactivates shortcut");
        Equal(0, holder.Reads, "shortcut sees current flags without cached selection state");
        Equal(3, holder.GetDirectlyHeldThings().Count, "ordinary inventory queries still see all contents");
    }

    private static void UnrelatedContainers()
    {
        var foreign = new TestContainer(); var item = new Thing(); foreign.Contents.Add(item);
        Equal(0, ContainingSelectionUtility.SelectableContainedThings(foreign).Count(), "foreign container retains native result with flags disabled");
        Equal(1, foreign.Reads, "prefix does not replace foreign container traversal");
        foreign.def.containedItemsSelectable = true;
        Equal(item, ContainingSelectionUtility.SelectableContainedThings(foreign).Single(), "foreign selectable contents unchanged");
        var pawn = new Pawn { CarriedThing = item, def = new ThingDef { containedItemsSelectable = true } };
        Equal(item, ContainingSelectionUtility.SelectableContainedThings(pawn).Single(), "pawn carried-item selection unchanged");
        var building = new Building(); building.def.containedItemsSelectable = true; building.Comps.Add(foreign);
        Equal(item, ContainingSelectionUtility.SelectableContainedThings(building).Single(), "foreign component-owned container unchanged");
        Equal(0, ContainingSelectionUtility.SelectableContainedThings(new MinifiedThing()).Count(), "minified object selection unchanged");
        Equal(0, ContainingSelectionUtility.SelectableContainedThings(new Thing()).Count(), "noncontainer selection unchanged");
    }
}
