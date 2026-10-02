using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public abstract class ResonancePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override string CustomPackedIconPath => Main.Art("core");
    public override string CustomBigIconPath => Main.Art("core");
    public override string CustomBigBetaIconPath => Main.Art("core");
}

public sealed class SupercomputePower : ResonancePower
{
    public override string CustomPackedIconPath => Main.Art("super");
    public override string CustomBigIconPath => Main.Art("super");
    public override string CustomBigBetaIconPath => Main.Art("super");
    public override List<(string, string)> Localization => new PowerLoc("超算",
        "下一张手动打出的信号球获得三消效果。不可叠加，跨回合保留，使用后消失。自动打出的信号球不消耗超算。",
        "下一张手动打出的信号球获得三消效果。不可叠加，跨回合保留，使用后消失。自动打出的信号球不消耗超算。");
}

public sealed class PerfectDodgePower : ResonancePower
{
    public override List<(string, string)> Localization => new PowerLoc("极限闪避", "完整格挡下一段非零攻击伤害后获得超算。下个自己的回合开始时失效。", "完整格挡下一段非零攻击伤害后获得超算。下个自己的回合开始时失效。");
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || !ResonanceRules.PerfectBlock(props.IsPoweredAttack(), result.BlockedDamage, result.UnblockedDamage)) return;
        await PowerCmd.Remove(this);
        await ResonanceRuntime.GainSupercompute(context, Owner.Player!);
    }
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

public sealed class AfterglowPower : ResonancePower
{
    public bool Active { get; internal set; }
    public override List<(string, string)> Localization => new PowerLoc("光耀余晖", "三消效果结算后，本回合攻击伤害提高 30%，不可叠加。", "三消效果结算后，本回合攻击伤害提高 30%，不可叠加。");
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => Active && dealer == Owner && props.IsPoweredAttack() ? 1.3m : 1m;
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) Active = false;
        return Task.CompletedTask;
    }
}

public sealed class LightningPower : ResonancePower
{
    public bool Active { get; internal set; }
    public override List<(string, string)> Localization => new PowerLoc("超算闪电", "获得超算时，所有敌人受到伤害提高 10%，持续到你下次结束回合。不可叠加。", "获得超算时，所有敌人受到伤害提高 10%，持续到你下次结束回合。不可叠加。");
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => Active && target != null && target.Side != Owner.Side ? 1.1m : 1m;
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) Active = false;
        return Task.CompletedTask;
    }
}

public sealed class DeadlinePower : ResonancePower
{
    private int _appliedTurn;
    public int TurnsUntilSupply => Math.Max(0, 1 + ((Owner.Player!.PlayerCombatState!.TurnNumber - _appliedTurn) % 2));
    public override List<(string, string)> Localization => new PowerLoc("死线计时", "从下回合开始，隔回合在抽牌后补充 3 张随机同色临时球。满手停止生成。", "从下回合开始，隔回合在抽牌后补充 3 张随机同色临时球。满手停止生成。");
    public override Task AfterApplied(Creature? applier, CardModel? source)
    {
        _appliedTurn = Owner.Player!.PlayerCombatState!.TurnNumber;
        return Task.CompletedTask;
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player == Owner.Player && ResonanceRules.DeadlineDue(_appliedTurn, player.PlayerCombatState!.TurnNumber))
            await Generate(context, 3, true);
    }
    internal async Task Generate(PlayerChoiceContext context, int count, bool sameColor)
    {
        var player = Owner.Player!;
        int color = player.RunState.Rng.CombatCardSelection.NextInt(3);
        await TemporarySignals.Generate(context, player, count, i =>
        {
            if (!sameColor && i > 0) color = player.RunState.Rng.CombatCardSelection.NextInt(3);
            return color switch { 0 => SignalColor.Red, 1 => SignalColor.Yellow, _ => SignalColor.Blue };
        });
    }
}

// Both supply cards share cleanup; this power does not grant Deadline's recurring supply.
public sealed class TemporarySignalsPower : ResonancePower
{
    private HashSet<CardModel>? _temporaryCards;
    private HashSet<CardModel> Temporary => _temporaryCards ??= [];
    public override List<(string, string)> Localization => new PowerLoc("临时信号球", "临时球打出后消耗；弃置或回合结束时移出战斗。", "临时球打出后消耗；弃置或回合结束时移出战斗。");
    internal void Track(CardModel card) => Temporary.Add(card);
    public override async Task AfterCardDiscarded(PlayerChoiceContext context, CardModel card)
    {
        if (Temporary.Remove(card)) await CardPileCmd.RemoveFromCombat(card);
    }
    public override async Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        var cards = Temporary.Where(c => c.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard).ToArray();
        foreach (var card in cards) { Temporary.Remove(card); await CardPileCmd.RemoveFromCombat(card); }
    }
}

internal static class ResonanceRuntime
{
    internal static async Task GainSupercompute(PlayerChoiceContext context, Player player)
    {
        if (player.PlayerCombatState == null || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding) return;
        if (player.Creature.GetPower<SupercomputePower>() == null)
            await PowerCmd.Apply<SupercomputePower>(context, player.Creature, 1, player.Creature, null);
        CustomResources<Supercompute>.Get(player.PlayerCombatState).Amount = 1;
        if (player.Creature.GetPower<LightningPower>() is { } lightning) lightning.Active = true;
    }

    internal static async Task ClearSupercompute(Player player)
    {
        if (player.PlayerCombatState != null)
            CustomResources<Supercompute>.Get(player.PlayerCombatState).Amount = 0;
        await PowerCmd.Remove(player.Creature.GetPower<SupercomputePower>());
    }
}

internal static class TemporarySignals
{
    internal static async Task Generate(PlayerChoiceContext context, Player player, int count, Func<int, SignalColor> chooseColor)
    {
        if (CombatManager.Instance.IsOverOrEnding || !player.Creature.IsAlive || player.PlayerCombatState == null || player.PlayerCombatState.Hand.Cards.Count >= CardPile.MaxCardsInHand) return;
        var cleanup = player.Creature.GetPower<TemporarySignalsPower>() ?? await PowerCmd.Apply<TemporarySignalsPower>(context, player.Creature, 1, player.Creature, null);
        if (cleanup == null) return;
        for (int i = 0; i < count; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding || !player.Creature.IsAlive || player.PlayerCombatState.Hand.Cards.Count >= CardPile.MaxCardsInHand) break;
            CardModel canonical = chooseColor(i) switch
            {
                SignalColor.Red => ModelDb.Card<RedSignal>(),
                SignalColor.Yellow => ModelDb.Card<YellowSignal>(),
                SignalColor.Blue => ModelDb.Card<BlueSignal>(),
                _ => throw new ArgumentOutOfRangeException(nameof(chooseColor))
            };
            var card = player.Creature.CombatState!.CreateCard(canonical, player);
            card.AddKeyword(CardKeyword.Exhaust);
            card.AddKeyword(CardKeyword.Ethereal);
            cleanup.Track(card);
            await CardPileCmd.AddGeneratedCardsToCombat([card], PileType.Hand, player);
        }
    }
}
