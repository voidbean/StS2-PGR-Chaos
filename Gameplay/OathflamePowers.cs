using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class OathflameFlightPower : ResonancePower
{
    public override List<(string, string)> Localization => new PowerLoc("飞行", "受到的攻击伤害降低 50%，不叠加。下个自己的回合开始时失效。", "受到的攻击伤害降低 50%，不叠加。下个自己的回合开始时失效。");
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => target == Owner && props.IsPoweredAttack() ? 0.5m : 1m;
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

public sealed class MoltenChasePower : ResonancePower
{
    public int Remaining { get; private set; }
    internal void Refresh() => Remaining = 2;
    public override List<(string, string)> Localization => new PowerLoc("熔金追击", "本回合后续两次手动三消结算后，各造成全体 4 点能力伤害。重复施放刷新至两次，重放不多计。", "本回合后续两次手动三消结算后，各造成全体 4 点能力伤害。重复施放刷新至两次，重放不多计。");
    internal async Task AfterTriple(PlayerChoiceContext context)
    {
        if (Remaining == 0 || !OathflameRuntime.CanAct(Owner.Player!)) return;
        Remaining--;
        await CreatureCmd.Damage(context, CombatState.HittableEnemies.ToArray(), 4, ValueProp.Unpowered, Owner);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

public sealed class ContinuationCleanupPower : ResonancePower
{
    public override List<(string, string)> Localization => new PowerLoc("飞光续斩", "回合结束移除手牌、抽牌堆、弃牌堆中未使用的飞光续斩。", "回合结束移除手牌、抽牌堆、弃牌堆中未使用的飞光续斩。");
    public override async Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        var cards = Owner.Player!.PlayerCombatState!.AllCards.Where(c => c is SoaringContinuation && c.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard).ToArray();
        foreach (var card in cards) await CardPileCmd.RemoveFromCombat(card);
    }
}

public sealed class OmegaCorePower : ResonancePower
{
    private int _appliedTurn;
    private int _lastSupplyTurn;
    public override List<(string, string)> Localization => new PowerLoc("Ω 核心", "从启动后的下个自己的回合开始，每回合获得 1 能量、1 充能。不可叠加；引爆后本场不可重启。", "从启动后的下个自己的回合开始，每回合获得 1 能量、1 充能。不可叠加；引爆后本场不可重启。");
    public override Task AfterApplied(Creature? applier, CardModel? source)
    {
        _appliedTurn = Owner.Player!.PlayerCombatState!.TurnNumber;
        return Task.CompletedTask;
    }
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combatState)
    {
        if (player != Owner.Player || !OathflameRuntime.CanAct(player) || OathflameRuntime.Omega(player).Burned) return;
        int turn = player.PlayerCombatState!.TurnNumber;
        if (turn <= _appliedTurn || turn == _lastSupplyTurn) return;
        _lastSupplyTurn = turn;
        await PlayerCmd.GainEnergy(1, player);
        if (OathflameRuntime.CanAct(player)) CustomResources<Charge>.Get(player.PlayerCombatState!).ModifyAmount(1);
    }
}

internal static class OathflameRuntime
{
    internal sealed class OmegaState { internal bool Generated; internal bool Burned; }
    private static readonly Dictionary<Player, OmegaState> States = [];
    internal static OmegaState Omega(Player player)
    {
        if (!States.TryGetValue(player, out var state)) States[player] = state = new();
        return state;
    }
    internal static void Reset() => States.Clear();
    internal static bool CanAct(Player player) => !CombatManager.Instance.IsOverOrEnding && player.Creature.IsAlive && player.PlayerCombatState != null;
    internal static bool CanBurn(Player player) => player.Creature.CurrentHp > 10 && player.Creature.GetPower<OmegaCorePower>() != null && !Omega(player).Burned;
    internal static async Task Fly(PlayerChoiceContext context, CardModel source)
    {
        if (CanAct(source.Owner) && source.Owner.Creature.GetPower<OathflameFlightPower>() == null)
            await PowerCmd.Apply<OathflameFlightPower>(context, source.Owner.Creature, 1, source.Owner.Creature, source);
    }
}
