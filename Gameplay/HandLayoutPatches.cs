using ChaosPrototype.Core;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ChaosPrototype.Gameplay;

// Insert into the correct section before pile events observe the new card.
// Existing cards never move relative to one another, including during selection.
[HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
internal static class HandSectionPatch
{
    [HarmonyPrefix]
    static void Prefix(CardPile __instance, CardModel card, ref int index)
    {
        if (__instance.Type == PileType.Hand && card.Owner?.Character is ChaosCharacter)
            index = SignalRules.HandInsertIndex(__instance.Cards, card, SignalRuntime.Color);
    }
}

[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand.Add))]
internal static class HandVisualInsertionPatch
{
    // The native helper accounts for holders absent while selected, dragged or queued.
    private static readonly System.Reflection.MethodInfo InsertIndex =
        AccessTools.Method(typeof(NPlayerHand), "GetHandInsertIndex");

    [HarmonyPrefix]
    static void Prefix(NPlayerHand __instance, NCard card, ref int index)
    {
        if (card.Model?.Owner?.Character is ChaosCharacter && card.Model.Pile?.Type == PileType.Hand)
            index = (int)InsertIndex.Invoke(__instance, [card.Model])!;
    }
}
