using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MagicStorage
{
    // Resolve the optional mod at runtime; no reference to its assembly is required.
    internal sealed class DynamicTradeInterfaceCompatibility : IStorageCompatibility
    {
        private sealed class Replacement
        {
            internal Dialog_StorageTrade Source;
            internal StorageTradeSession Session;
            internal Replacement Previous;
            internal Window Target;
            internal bool SourceClosed;
        }

        private static readonly Dictionary<Window, StorageTradeSession> sessions =
            new Dictionary<Window, StorageTradeSession>();
        private static Type windowType;
        private static MethodInfo flashSilver;
        private static Replacement replacing;

        public string PackageId => "zeracronius.dynamictradeinterface";

        public void Apply(Harmony harmony)
        {
            Type patches = AccessTools.TypeByName("DynamicTradeInterface.Patches.Dialog_TradePatches");
            Type resolvedWindow = AccessTools.TypeByName("DynamicTradeInterface.UserInterface.Window_DynamicTrade");
            MethodInfo replace = patches == null ? null : AccessTools.DeclaredMethod(patches, "Prefix");
            MethodInfo preOpen = resolvedWindow == null ? null : AccessTools.DeclaredMethod(resolvedWindow, "PreOpen");
            MethodInfo postClose = resolvedWindow == null ? null : AccessTools.DeclaredMethod(resolvedWindow, "PostClose");
            MethodInfo flash = resolvedWindow == null ? null : AccessTools.DeclaredMethod(resolvedWindow, "FlashSilver");
            if (replace == null || preOpen == null || postClose == null || flash == null ||
                !typeof(Window).IsAssignableFrom(resolvedWindow))
                throw new InvalidOperationException("Dynamic Trade Interface window replacement API was not found.");

            harmony.Patch(replace,
                prefix: new HarmonyMethod(typeof(DynamicTradeInterfaceCompatibility), nameof(BeginReplacement)),
                finalizer: new HarmonyMethod(typeof(DynamicTradeInterfaceCompatibility), nameof(EndReplacement)));
            harmony.Patch(preOpen,
                prefix: new HarmonyMethod(typeof(DynamicTradeInterfaceCompatibility), nameof(AttachWindow)));
            harmony.Patch(postClose,
                finalizer: new HarmonyMethod(typeof(DynamicTradeInterfaceCompatibility), nameof(CloseWindow)));
            windowType = resolvedWindow;
            flashSilver = flash;
        }

        internal static bool HasTradeWindow()
        {
            if (windowType == null) return false;
            foreach (var window in Find.WindowStack.Windows)
                if (windowType.IsInstanceOfType(window)) return true;
            return false;
        }

        private static void BeginReplacement(Dialog_Trade __0, out Replacement __state)
        {
            __state = null;
            if (!(__0 is Dialog_StorageTrade source) || TradeSession.deal == null ||
                !StorageTradeSession.TryGet(TradeSession.deal, out var session) || session != source.Session) return;
            __state = new Replacement { Source = source, Session = session, Previous = replacing };
            replacing = __state;
        }

        internal static bool DeferClose(Dialog_StorageTrade source)
        {
            if (replacing == null || replacing.Source != source) return false;
            // DTI removes this window before opening its own. Keep the inventory alive
            // through PreOpen, where DTI builds its colony table using CountHeldBy.
            replacing.SourceClosed = true;
            return true;
        }

        private static void AttachWindow(Window __instance)
        {
            if (replacing == null || !replacing.SourceClosed || replacing.Target != null ||
                TradeSession.deal == null || !StorageTradeSession.TryGet(TradeSession.deal, out var session) ||
                session != replacing.Session) return;
            replacing.Target = __instance;
            sessions.Add(__instance, session);
        }

        private static Exception EndReplacement(Replacement __state, Exception __exception)
        {
            if (__state == null) return __exception;
            replacing = __state.Previous;
            if (__state.SourceClosed && (__exception != null || __state.Target == null ||
                !Find.WindowStack.Windows.Contains(__state.Target)))
            {
                // A failed replacement must not leave a session or reservations behind.
                if (__state.Target != null) sessions.Remove(__state.Target);
                __state.Session.Dispose();
            }
            return __exception;
        }

        private static Exception CloseWindow(Window __instance, Exception __exception)
        {
            if (sessions.TryGetValue(__instance, out var session))
            {
                sessions.Remove(__instance);
                session.Dispose();
            }
            return __exception;
        }

        internal static void FlashSilver(StorageTradeSession session)
        {
            foreach (var entry in sessions)
                if (entry.Value == session && Find.WindowStack.Windows.Contains(entry.Key))
                {
                    flashSilver.Invoke(entry.Key, null);
                    return;
                }
        }
    }
}
