using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ChaosPrototype.Gameplay;

public abstract class WorldlinePower : ResonancePower
{
    private int _appliedTurn;
    private int _lastTriggeredTurn;
    protected abstract bool Exhausts { get; }
    public override Task AfterApplied(Creature? applier, CardModel? source)
    {
        _appliedTurn = _lastTriggeredTurn = Owner.Player!.PlayerCombatState!.TurnNumber;
        return Task.CompletedTask;
    }

    // Native combat hooks iterate the creature's powers in application order.
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner.Player || !OathflameRuntime.CanAct(player)) return;
        int turn = player.PlayerCombatState!.TurnNumber;
        if (turn <= _appliedTurn || turn <= _lastTriggeredTurn) return;
        _lastTriggeredTurn = turn;
        int count = Amount;
        await CardPileCmd.Draw(context, count, player);
        if (!OathflameRuntime.CanAct(player)) return;
        int available = Math.Min(count, player.PlayerCombatState.Hand.Cards.Count);
        if (available == 0) return;
        var prefs = new CardSelectorPrefs(Exhausts ? CardSelectorPrefs.ExhaustSelectionPrompt : CardSelectorPrefs.DiscardSelectionPrompt, available);
        var selected = (Exhausts
            ? await CardSelectCmd.FromHand(context, player, prefs, null, this)
            : await CardSelectCmd.FromHandForDiscard(context, player, prefs, null, this)).ToArray();
        if (!OathflameRuntime.CanAct(player)) return;
        if (!Exhausts) await CardCmd.Discard(context, selected);
        else foreach (var card in selected)
        {
            if (!OathflameRuntime.CanAct(player)) break;
            await CardCmd.Exhaust(context, card);
        }
    }
}

public sealed class CrownPower : WorldlinePower
{
    protected override bool Exhausts => false;
    public override List<(string, string)> Localization => new PowerLoc("王冠",
        "从施放的下回合起，正常抽牌后再抽 {Amount} 张，然后弃置 {Amount} 张手牌。不叠加，与无神论按施放顺序结算。",
        "正常抽牌后再抽 {Amount} 张，然后弃置 {Amount} 张手牌。不叠加。");
}

public sealed class AtheismPower : WorldlinePower
{
    protected override bool Exhausts => true;
    public override List<(string, string)> Localization => new PowerLoc("无神论",
        "从施放的下回合起，正常抽牌后再抽 1 张，然后消耗 1 张手牌。不叠加，与王冠按施放顺序结算。生命仅在打出能力牌时支付。",
        "正常抽牌后再抽 1 张，然后消耗 1 张手牌。不叠加。");
}
