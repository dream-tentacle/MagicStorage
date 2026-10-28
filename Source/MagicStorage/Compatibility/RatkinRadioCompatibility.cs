using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MagicStorage
{
    // Only the extra button opts into storage. Ratkin owns stock, dialogue, closing and delivery.
    internal sealed class RatkinRadioCompatibility : IStorageCompatibility
    {
        private sealed class TradeChoice { internal bool Storage; }
        private static readonly ConditionalWeakTable<Window, TradeChoice> choices = new ConditionalWeakTable<Window, TradeChoice>();
        [ThreadStatic] private static Window storageOpening;
        private static FieldInfo radioField, readyField, inProgressField;
        private static MethodInfo radioComponent, componentCanTrade, openTrade, triggerDialogue;
        private static readonly ConstructorInfo tradeConstructor = AccessTools.Constructor(typeof(Dialog_Trade),
            new[] { typeof(Pawn), typeof(ITrader), typeof(bool) });
        private static readonly MethodInfo addWindow = AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add),
            new[] { typeof(Window) });

        public string PackageId => "RKU.RatkinUnderground";

        public void Apply(Harmony harmony)
        {
            Type radio = AccessTools.TypeByName("RatkinUnderground.Dialog_RKU_Radio");
            Type dialogue = AccessTools.TypeByName("RatkinUnderground.RKU_DialogueManager");
            var trigger = radio == null || dialogue == null ? null : AccessTools.Method(dialogue, "TriggerDialogueEvents",
                new[] { radio, typeof(string) });
            var thing = radio == null ? null : AccessTools.Field(radio, "radio");
            var ready = radio == null ? null : AccessTools.Field(radio, "tradeReady");
            var progress = radio == null ? null : AccessTools.Field(radio, "tradeInProgress");
            var component = radio == null ? null : AccessTools.Method(radio, "GetRadioComponent", Type.EmptyTypes);
            var available = component == null ? null : AccessTools.PropertyGetter(component.ReturnType, "CanTradeNow");
            var canTrade = radio == null ? null : AccessTools.PropertyGetter(radio, "CanTradeNow");
            var open = radio == null ? null : AccessTools.DeclaredMethod(radio, "OpenTradeWindow", Type.EmptyTypes);
            var draw = radio == null ? null : AccessTools.DeclaredMethod(radio, "DoWindowContents", new[] { typeof(Rect) });
            if (radio == null || !typeof(Window).IsAssignableFrom(radio) || !typeof(ITrader).IsAssignableFrom(radio) ||
                thing?.FieldType != typeof(Thing) || ready?.FieldType != typeof(bool) || progress?.FieldType != typeof(bool) ||
                available?.ReturnType != typeof(bool) || canTrade?.ReturnType != typeof(bool) || open?.ReturnType != typeof(void) ||
                draw?.ReturnType != typeof(void) || trigger?.ReturnType != typeof(void) || !trigger.IsStatic ||
                tradeConstructor == null || addWindow == null)
                throw new InvalidOperationException("Ratkin Underground radio trade API was not found.");

            // Find the delayed opener without binding to a compiler's lambda number.
            var callbacks = AccessTools.GetDeclaredMethods(radio).Where(method =>
                method.Name.StartsWith("<OpenTradeWindow>", StringComparison.Ordinal) &&
                PatchProcessor.GetOriginalInstructions(method).Any(IsTradeConstructor)).ToList();
            if (callbacks.Count != 1)
                throw new InvalidOperationException("Ratkin Underground radio trade opener was not uniquely identified.");

            radioField = thing; readyField = ready; inProgressField = progress;
            radioComponent = component; componentCanTrade = available; openTrade = open; triggerDialogue = trigger;
            var targets = new[] { canTrade, open, callbacks[0], draw };
            try
            {
                harmony.Patch(canTrade, postfix: Hook(nameof(CanContinueTrading)));
                harmony.Patch(open, prefix: Hook(nameof(ChooseInventory)));
                harmony.Patch(callbacks[0], transpiler: Hook(nameof(TradeOpener)));
                harmony.Patch(draw, postfix: Hook(nameof(DrawStorageButton)));
            }
            catch
            {
                foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
                throw;
            }
        }

        private static HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(RatkinRadioCompatibility), name);
        private static bool IsTradeConstructor(CodeInstruction instruction) =>
            instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, tradeConstructor);

        private static bool RadioUsable(Window instance, Thing expectedRadio)
        {
            var radio = radioField.GetValue(instance) as Thing;
            return radio != null && ReferenceEquals(radio, expectedRadio) && radio.Spawned && !radio.Destroyed &&
                radio.Map != null && Find.WindowStack.Windows.Contains(instance);
        }

        private static bool CanUseStorage(Window window) => (bool)readyField.GetValue(window) &&
            !StorageTradeSession.HasTradeWindow() && RadioUsable(window, radioField.GetValue(window) as Thing) &&
            StorageTradeSession.HasCore(((Thing)radioField.GetValue(window)).Map);

        private static void DrawStorageButton(Window __instance, Rect __0)
        {
            if (!(bool)readyField.GetValue(__instance)) return;
            // The bottom row is full; the title's right side has room for this independent button.
            var rect = new Rect(__0.width - 190f, 3f, 180f, 30f);
            if (Widgets.ButtonText(rect, "MS_Trade_RadioCommand".Translate(), active: CanUseStorage(__instance)))
                BeginStorageTrade(__instance);
        }

        private static void BeginStorageTrade(Window window)
        {
            if (!CanUseStorage(window)) return;
            Window previous = storageOpening;
            storageOpening = window;
            try
            {
                // Match Ratkin's ready-to-trade button, including dialogue and deferred negotiator selection.
                triggerDialogue.Invoke(null, new object[] { window, "trade" });
                openTrade.Invoke(window, null);
                readyField.SetValue(window, false);
            }
            finally { storageOpening = previous; }
        }

        private static void ChooseInventory(Window __instance) =>
            choices.GetValue(__instance, _ => new TradeChoice()).Storage = ReferenceEquals(storageOpening, __instance);

        private static void CanContinueTrading(object __instance, ref bool __result)
        {
            if (__result || !(bool)inProgressField.GetValue(__instance)) return;
            object component = radioComponent.Invoke(__instance, null);
            // An active trade remains usable; Ratkin's closing and cooldown lifecycle stays intact.
            __result = (bool)readyField.GetValue(__instance) ||
                (component != null && (bool)componentCanTrade.Invoke(component, null));
        }

        private static IEnumerable<CodeInstruction> TradeOpener(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int constructor = -1, add = -1, matches = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (!IsTradeConstructor(code[i])) continue;
                int next = i + 1;
                while (next < code.Count && code[next].opcode == OpCodes.Nop) next++;
                if (next >= code.Count || !code[next].Calls(addWindow)) continue;
                constructor = i; add = next; matches++;
            }
            if (matches != 1)
                throw new InvalidOperationException("Ratkin Underground radio window insertion changed; no trade replacement installed.");
            // Consume the existing WindowStack, Pawn, ITrader, bool. Preserve all labels and exception regions.
            code[constructor].opcode = OpCodes.Call;
            code[constructor].operand = AccessTools.Method(typeof(RatkinRadioCompatibility), nameof(OpenTrade));
            code[add].opcode = OpCodes.Nop;
            code[add].operand = null;
            return code;
        }

        private static void OpenTrade(WindowStack stack, Pawn negotiator, ITrader trader, bool giftsOnly)
        {
            var window = (Window)trader;
            if (!choices.TryGetValue(window, out var choice) || !choice.Storage || giftsOnly)
            {
                stack.Add(new Dialog_Trade(negotiator, trader, giftsOnly));
                return;
            }
            Thing radio = radioField.GetValue(trader) as Thing;
            var result = StorageTradeApi.Open(new StorageTradeOptions
            {
                Negotiator = negotiator, Trader = trader, Mode = StorageTradeMode.Ground, GroundTarget = radio,
                IsStillValid = () => RadioUsable(window, radio) && (bool)inProgressField.GetValue(trader)
            });
            if (result.Opened) return;
            // An explicit storage request must not silently switch to a different inventory.
            Messages.Message("MS_Trade_Failed".Translate("MS_Trade_Fail_TraderUnavailable".Translate()),
                MessageTypeDefOf.RejectInput, false);
        }
    }
}
