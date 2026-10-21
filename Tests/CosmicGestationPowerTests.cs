using System;
using System.Runtime.CompilerServices;
using MagicStorage;
using RimWorld;
using Verse;

internal static class CosmicGestationPowerTests
{
    internal static int Run()
    {
        var cosmic = new Building_CosmicMechGestator { Faction = Faction.OfPlayer,
            node = new CompStorageNode { Network = new StorageNetwork() } };
        bool reproduced = false;
        try { cosmic.TickForTest(); }
        catch (NullReferenceException) { reproduced = true; }
        Check(reproduced, "The native electrical update requires a component before adaptation.");

        RuntimeHelpers.RunClassConstructor(typeof(CosmicGestationPower).TypeHandle);
        cosmic.TickForTest();
        Check(cosmic.Power == null && cosmic.PowerUpdates == 0,
            "A cosmic gestator ticks without creating or touching an electrical component.");
        Check(cosmic.CycleBranches == 1 && cosmic.PoweredOn, "Reach native cycle handling without an electrical grid.");
        cosmic.node.Network.CanWork = false;
        cosmic.TickForTest();
        Check(cosmic.CycleBranches == 1 && !cosmic.PoweredOn, "An unavailable storage network still pauses gestation.");
        cosmic.node.Network.CanWork = true; cosmic.IntervalDue = false;
        cosmic.TickForTest();
        Check(cosmic.CycleBranches == 2 && cosmic.PowerUpdates == 0, "Native cycle handling does not depend on the power-update interval.");

        var ordinary = new Building_MechGestator { Power = new CompPowerTrader { PowerOn = true } };
        ordinary.TickForTest();
        Check(ordinary.PowerUpdates == 1 && ordinary.CycleBranches == 1, "Retain ordinary gestator electrical updates.");
        ordinary.Power.PowerOn = false; ordinary.TickForTest();
        Check(ordinary.PowerUpdates == 2 && ordinary.CycleBranches == 1, "Ordinary gestators still require power for their powered cycle branch.");
        ordinary.IntervalDue = false; ordinary.TickForTest();
        Check(ordinary.PowerUpdates == 2, "Retain the ordinary electrical update interval.");
        return 8;
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}

namespace RimWorld
{
    public class CompPowerTrader
    { public bool PowerOn; public float PowerOutput; }
}
