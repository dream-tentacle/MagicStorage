using System;
using System.Linq;
using MagicStorage;
using RimWorld;
using Verse;

internal static class StorageGuideTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string message)
    {
        checks++;
        if (!Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual);
    }

    public static int Main()
    {
        try { Run(); Console.WriteLine("Storage guide: " + checks + " checks passed."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Run()
    {
        // The gestator is deliberately absent, like a game without its required DLC.
        foreach (string name in GameComponent_StorageGuideNotice.AdvancedDefNames.Where(n => n != "MS_CosmicMechGestator"))
            DefDatabase<ThingDef>.Defs[name] = new ThingDef { defName = name };
        DefDatabase<ThingDef>.Defs["MS_HighStackSupplyShelf"] = new ThingDef { defName = "MS_HighStackSupplyShelf" };
        Current.Game = new Game();
        var progress = Current.Game.Progress = new GameComponent_StorageGuideNotice(Current.Game);
        var first = new Building_StorageCore { Spawned = true, Faction = Faction.OfPlayer, Map = new Map() };
        var second = new Building_StorageCore { Spawned = true, Faction = Faction.OfPlayer, Map = new Map() };
        progress.Complete(0, new Building_StorageCore());
        Equal(false, progress.Learned(0), "invalid cores cannot advance the tutorial");
        for (int i = 0; i < 5; i++) progress.Complete(i, first);
        Equal(StorageGuideStage.Basics, progress.Stage, "partial basics do not advance the stage");
        Equal(0, DropPodUtility.Pods.Count, "partial basics grant no reward");
        progress.ExposeData();
        var loaded = new GameComponent_StorageGuideNotice(Current.Game);
        Scribe_Values.Loading = true;
        loaded.ExposeData();
        Scribe_Values.Loading = false;
        progress = Current.Game.Progress = loaded;
        Equal(true, progress.Learned(4), "partial progress survives loading and core changes");
        progress.Complete(5, second);
        Equal(StorageGuideStage.Basics, progress.Stage, "basics wait for the construction supply tutorial");
        Equal(0, DropPodUtility.Pods.Count, "basic reward waits for the construction supply tutorial");
        int constructionStep = Array.IndexOf(GameComponent_StorageGuideNotice.AdvancedDefNames, "MS_ConstructionSupplyUnit");
        progress.CompleteAdvanced(constructionStep, second);
        Equal(StorageGuideStage.Advanced, progress.Stage, "basics completion advances the global stage");
        Equal(1, DropPodUtility.Pods.Count, "all rewards arrive in one pod");
        Equal(second.Map, DropPodUtility.LastMap, "reward follows the core completing the stage");
        var rewards = DropPodUtility.Pods.Single();
        Equal(100, rewards.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount), "exact steel reward");
        Equal(true, rewards.Where(t => t.def == ThingDefOf.Steel).All(t => t.stackCount <= t.def.stackLimit), "steel respects stack limits");
        Equal(5, rewards.Where(t => t.def == ThingDefOf.ComponentIndustrial).Sum(t => t.stackCount), "exact component reward");
        var minified = rewards.OfType<MinifiedThing>().Single(t => t.InnerThing.def.defName == "MS_ConstructionSupplyUnit");
        Equal("MS_ConstructionSupplyUnit", minified.InnerThing.def.defName, "building is delivered uninstalled");
        Equal(Faction.OfPlayer, minified.InnerThing.Faction, "reward building belongs to player");
        Equal(1, rewards.OfType<MinifiedThing>().Count(t => t.InnerThing.def.defName == "MS_StorageSupplyShelf"), "one uninstalled basic shelf");
        Equal(2, rewards.OfType<MinifiedThing>().Count(), "basic pod contains exactly two buildings");
        Equal("MS_Guide_BasicsCompleteTitle", Find.LetterStack.Titles.Single(), "one tutorial event letter");
        for (int i = 0; i < 6; i++) progress.Complete(i, first);
        progress.Notify_CoreBuilt(second);
        Equal(1, DropPodUtility.Pods.Count, "new cores cannot repeat the reward");
        Equal(1, Find.LetterStack.Titles.Count, "completed basics suppress the first-core reminder");
        progress.ExposeData();
        loaded = new GameComponent_StorageGuideNotice(Current.Game);
        Scribe_Values.Loading = true;
        loaded.ExposeData();
        Scribe_Values.Loading = false;
        progress = Current.Game.Progress = loaded;
        Equal(StorageGuideStage.Advanced, progress.Stage, "global stage survives loading");
        progress.Complete(5, second);
        Equal(1, DropPodUtility.Pods.Count, "loading cannot repeat the reward");
        int productionOutputStep = Array.IndexOf(GameComponent_StorageGuideNotice.AdvancedDefNames, "MS_ProductionOutputPort");
        Equal(true, productionOutputStep >= 0, "production output is part of advanced learning");
        for (int i = 0; i < GameComponent_StorageGuideNotice.AdvancedDefNames.Length; i++)
        {
            if (i == 1 || i == productionOutputStep) continue;
            progress.CompleteAdvanced(i, i % 2 == 0 ? first : second);
        }
        Equal(StorageGuideStage.Advanced, progress.Stage, "remaining production output step prevents early completion");
        Equal(1, DropPodUtility.Pods.Count, "advanced reward waits for the production output tutorial");
        progress.CompleteAdvanced(productionOutputStep, second);
        Equal(StorageGuideStage.Completed, progress.Stage, "advanced learning is shared and excludes absent DLC");
        Equal(true, progress.AdvancedLearned(0), "learned advanced steps remain complete");
        Equal(2, DropPodUtility.Pods.Count, "advanced completion grants one additional pod");
        var advancedRewards = DropPodUtility.Pods[1];
        Equal(200, advancedRewards.Where(t => t.def == ThingDefOf.Steel).Sum(t => t.stackCount), "exact advanced steel reward");
        Equal(10, advancedRewards.Where(t => t.def == ThingDefOf.ComponentIndustrial).Sum(t => t.stackCount), "exact advanced component reward");
        Equal(3, advancedRewards.OfType<MinifiedThing>().Count(t => t.InnerThing.def.defName == "MS_CosmicWorkbench"), "three uninstalled workbenches");
        Equal(1, advancedRewards.OfType<MinifiedThing>().Count(t => t.InnerThing.def.defName == "MS_HighStackSupplyShelf"), "one uninstalled high-stack shelf");
        Equal(4, advancedRewards.OfType<MinifiedThing>().Count(), "advanced pod contains exactly four buildings");
        Equal(true, DropPodUtility.Pods.SelectMany(p => p).OfType<MinifiedThing>().All(t => t.InnerThing.Faction == Faction.OfPlayer), "all reward buildings belong to player");
        Equal("MS_Guide_AdvancedCompleteTitle", Find.LetterStack.Titles.Last(), "advanced completion letter");
        progress.ExposeData();
        loaded = new GameComponent_StorageGuideNotice(Current.Game);
        Scribe_Values.Loading = true;
        loaded.ExposeData();
        Scribe_Values.Loading = false;
        loaded.CompleteAdvanced(0, second);
        loaded.Complete(5, first);
        Equal(StorageGuideStage.Completed, loaded.Stage, "completed stage survives loading");
        Equal(2, DropPodUtility.Pods.Count, "neither stage repeats its reward after loading");
        Equal(2, Find.LetterStack.Titles.Count, "completion letters do not repeat");
        var freshGame = new GameComponent_StorageGuideNotice(new Game());
        Equal(StorageGuideStage.Basics, freshGame.Stage, "new saves start their own tutorial");
        Equal(false, freshGame.Learned(0), "progress does not leak to another save");
    }
}
