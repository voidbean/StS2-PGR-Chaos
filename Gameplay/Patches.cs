using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ChaosPrototype.Gameplay;

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddDuringManualCardPlay))]
internal static class CapturePatch
{
    [HarmonyPrefix] static void Prefix(CardModel card) => SignalRuntime.Capture(card);
}
[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class CleanupPatch
{
    [HarmonyPostfix] static void Postfix(CardModel __instance, bool isAutoPlay, ref Task __result)
    {
        if (!isAutoPlay && (__instance is SignalCard || __instance.Owner.Character is ChaosCharacter))
            __result = Cleanup(__result, __instance);
    }
    private static async Task Cleanup(Task task, CardModel card)
    {
        try { await task; } finally { SignalRuntime.Cleanup(card); }
    }
}
[HarmonyPatch(typeof(CardModel), nameof(CardModel.TryManualPlay))]
internal static class NoQueuePatch
{
    [HarmonyPrefix] static bool Prefix(CardModel __instance, ref bool __result, out bool __state)
    {
        __state = false;
        if (__instance.Owner.Character is not ChaosCharacter) return true;
        if (UI.DebugPanel.Busy || SignalRuntime.Pending.ContainsKey(__instance.Owner)) { __result = false; return false; }
        SignalRuntime.Pending.Add(__instance.Owner, __instance);
        __state = true;
        return true;
    }
    [HarmonyPostfix] static void Postfix(CardModel __instance, bool __result, bool __state)
    {
        if (__state && !__result) SignalRuntime.Cleanup(__instance);
    }
    [HarmonyFinalizer] static Exception? Finalizer(CardModel __instance, bool __state, Exception? __exception)
    {
        if (__state && __exception != null) SignalRuntime.Cleanup(__instance);
        return __exception;
    }
}
[HarmonyPatch(typeof(PlayCardAction), "ExecuteAction")]
internal static class ActionCleanupPatch
{
    [HarmonyPostfix] static void Postfix(PlayCardAction __instance, ref Task __result)
    {
        if (__instance.Player.Character is ChaosCharacter)
            __result = Finish(__result, __instance.NetCombatCard.ToCardModelOrNull());
    }
    private static async Task Finish(Task task, CardModel? card)
    {
        try { await task; } finally { if (card != null) SignalRuntime.Cleanup(card); }
    }
}
[HarmonyPatch(typeof(PlayCardAction), "CancelAction")]
internal static class CancelPatch
{
    [HarmonyPostfix] static void Postfix(PlayCardAction __instance)
    {
        if (__instance.Player.Character is ChaosCharacter && __instance.NetCombatCard.ToCardModelOrNull() is { } card)
            SignalRuntime.Cleanup(card);
    }
}
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
internal static class AutoPlayPatch
{
    [HarmonyPrefix] static bool Prefix(PlayerChoiceContext choiceContext, CardModel card, ref Task __result)
    {
        if (SignalRuntime.Suppressed.Contains(card)) { __result = Task.CompletedTask; return false; }
        if (card is not ChaosCard { PreventAutoPlay: true }) return true;
        __result = SkipUltimate(choiceContext, card);
        return false;
    }
    private static async Task SkipUltimate(PlayerChoiceContext context, CardModel card)
    {
        await CardPileCmd.Add(card, PileType.Play);
        await card.MoveToResultPileWithoutPlaying(context);
    }
}
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.AfterCombatEnd))]
internal static class EndCombatPatch
{
    [HarmonyPrefix] static void Prefix(PlayerCombatState __instance) => DebugSession.Finish(__instance);
    [HarmonyPostfix] static void Postfix() => SignalRuntime.Reset();
}
