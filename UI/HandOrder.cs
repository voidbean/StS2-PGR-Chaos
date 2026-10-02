using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ChaosPrototype.UI;

internal static class HandOrder
{
    internal static void Apply(Player player, IReadOnlyList<CardModel> order)
    {
        var hand = player.PlayerCombatState!.Hand;
        foreach (var card in order) hand.MoveToBottomInternal(card);
        hand.InvokeContentsChanged();
        if (LocalContext.IsMe(player) && NPlayerHand.Instance is { } node)
        {
            // Selection has finished: synchronize live holders with the backend order.
            var holders = node.CardHolderContainer.GetChildren().OfType<NHandCardHolder>().ToArray();
            int index = 0;
            foreach (var card in order)
            {
                var holder = holders.FirstOrDefault(h => ReferenceEquals(h.CardModel, card));
                if (holder != null) node.CardHolderContainer.MoveChild(holder, index++);
            }
            AccessTools.Method(typeof(NPlayerHand), "RefreshLayout").Invoke(node, null);
        }
    }
}
