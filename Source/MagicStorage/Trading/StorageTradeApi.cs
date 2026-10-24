using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MagicStorage
{
    public enum StorageTradeMode { Ground = 0, Orbital = 1 }
    public enum StorageTradeNetworkScope { AllEligible = 0, SpecifiedCores = 1 }
    public enum StorageTradeOpenStatus
    {
        Opened = 0,
        TradeWindowAlreadyOpen = 1,
        OpeningInProgress = 2,
        InvalidRequest = 3,
        TraderUnavailable = 4,
        NoEligibleNetwork = 5,
        OpenFailed = 6
    }

    /// <summary>Settings for an immediate trade window. Call on the game's main thread after the trade job arrives.</summary>
    public sealed class StorageTradeOptions
    {
        public Pawn Negotiator { get; set; }
        public ITrader Trader { get; set; }
        public StorageTradeMode Mode { get; set; } = StorageTradeMode.Ground;
        /// <summary>Ground interaction target. Defaults to Trader when it is a Pawn.</summary>
        public Thing GroundTarget { get; set; }
        public Building_CommsConsole CommsConsole { get; set; }
        /// <summary>Optional side-effect-free check, repeated during refresh and settlement. Exceptions invalidate the session.</summary>
        public Func<bool> IsStillValid { get; set; }
        public StorageTradeNetworkScope NetworkScope { get; set; } = StorageTradeNetworkScope.AllEligible;
        /// <summary>Required and nonempty with SpecifiedCores. Every selected core must remain eligible.</summary>
        public ICollection<Building_StorageCore> Cores { get; set; }

        internal StorageTradeOptions Snapshot() => new StorageTradeOptions
        {
            Negotiator = Negotiator, Trader = Trader, Mode = Mode,
            GroundTarget = GroundTarget, CommsConsole = CommsConsole, IsStillValid = IsStillValid,
            NetworkScope = NetworkScope, Cores = Cores?.ToArray()
        };
    }

    public sealed class StorageTradeOpenResult
    {
        public StorageTradeOpenStatus Status { get; }
        public bool Opened => Status == StorageTradeOpenStatus.Opened;
        /// <summary>True only when the caller may safely invoke its ordinary trade opener.</summary>
        public bool CanFallbackToVanilla { get; }
        /// <summary>Diagnostic text for mod authors; use Status to select localized player messages.</summary>
        public string Reason { get; }

        internal StorageTradeOpenResult(StorageTradeOpenStatus status, string reason = null, bool canFallback = false)
        { Status = status; Reason = reason; CanFallbackToVanilla = canFallback; }
    }

    /// <summary>Public trade API shared by RimWorld 1.6 and 1.7. Retains the caller's original ITrader callbacks.</summary>
    public static class StorageTradeApi
    {
        public static int ApiVersion => 1;
        private static bool opening;

        /// <summary>Opens a network trade window, or returns an explicit refusal. Never automatically opens ordinary trade.</summary>
        public static StorageTradeOpenResult Open(StorageTradeOptions options)
        {
            if (opening) return new StorageTradeOpenResult(StorageTradeOpenStatus.OpeningInProgress);
            if (StorageTradeSession.HasTradeWindow())
                return new StorageTradeOpenResult(StorageTradeOpenStatus.TradeWindowAlreadyOpen);
            opening = true;
            try
            {
                var snapshot = options?.Snapshot();
                string error = Validate(snapshot);
                if (error != null) return new StorageTradeOpenResult(StorageTradeOpenStatus.InvalidRequest, error);
                return StorageTradeSession.OpenValidated(snapshot);
            }
            catch (Exception error)
            {
                Log.Error("[MagicStorage] Trade API request failed: " + error);
                // Unexpected validation/collection failures are not an invitation to open another window.
                return new StorageTradeOpenResult(StorageTradeOpenStatus.OpenFailed, error.Message);
            }
            finally { opening = false; }
        }

        private static string Validate(StorageTradeOptions options)
        {
            if (options == null) return "Options are required.";
            if (options.Negotiator == null || options.Trader == null) return "Negotiator and Trader are required.";
            if (!options.Negotiator.Spawned || options.Negotiator.Destroyed || options.Negotiator.Map == null)
                return "Negotiator must be spawned on a map.";
            if (options.Mode != StorageTradeMode.Ground && options.Mode != StorageTradeMode.Orbital)
                return "Unknown trade mode.";
            if (options.Mode == StorageTradeMode.Orbital)
            {
                if (!(options.Trader is TradeShip) || options.CommsConsole == null || options.GroundTarget != null)
                    return "Orbital trade requires a TradeShip and CommsConsole, and no GroundTarget.";
            }
            else
            {
                if (options.CommsConsole != null) return "Ground trade cannot use a CommsConsole.";
                if (options.GroundTarget == null) options.GroundTarget = options.Trader as Pawn;
                if (options.GroundTarget == null) return "Custom ground traders require a GroundTarget.";
            }
            if (options.NetworkScope == StorageTradeNetworkScope.SpecifiedCores)
            {
                if (options.Cores == null || options.Cores.Count == 0 || options.Cores.Any(core => core == null))
                    return "SpecifiedCores requires a nonempty collection without null entries.";
                options.Cores = options.Cores.Distinct().ToArray();
            }
            else if (options.NetworkScope != StorageTradeNetworkScope.AllEligible)
                return "Unknown network scope.";
            else if (options.Cores != null && options.Cores.Count != 0)
                return "Set NetworkScope to SpecifiedCores when supplying Cores.";
            return null;
        }
    }
}
