using BaseLib.Abstracts;
using ChaosPrototype.Core;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

// One turn-scoped coordinator keeps per-card allowances stable across native replays.
public sealed class SignalSupportPower : ResonancePower
{
    private sealed class PlayState(bool flame, bool shot, int explosions)
    {
        internal readonly bool Flame = flame, Shot = shot;
        internal int Explosions = explosions;
        internal readonly HashSet<Creature> Hit = [];
    }
    private Dictionary<CardModel, PlayState> _plays = [];
    private Dictionary<Creature, int> _marks = [];
    private List<(Creature Target, int Damage)> _delayed = [];
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _plays = []; _marks = []; _delayed = [];
    }
    public int Flames { get; private set; }
    public int Shots { get; private set; }
    public int Explosions { get; private set; }
    public bool TraversalGenerated { get; private set; }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Flames", 0), new DynamicVar("Shots", 0), new DynamicVar("Explosions", 0), new DynamicVar("Marks", 0), new DynamicVar("Delayed", 0)];
    public override List<(string, string)> Localization => new PowerLoc("灰鸦战术支援",
        "本回合剩余：火焰附伤 {Flames} 次、转移充能 {Shots} 次、爆炎准备 {Explosions} 段；领域标记共 {Marks} 层、待结算延迟伤害 {Delayed} 次。",
        "火焰：后续攻击牌结算后，对其命中过的每名敌人造成 2 点能力伤害。转移：追加一次 3 点攻击伤害。领域：每次攻击命中消费该敌人一层标记，造成 2 点能力伤害。爆炎：下一张手动红色攻击球每段触发全体 1 点能力伤害，整张牌共享上限，未用次数作废。以上均于回合结束清理。原生重放不多消费牌次数。");
    private void RefreshDisplay()
    {
        DynamicVars["Flames"].BaseValue = Flames;
        DynamicVars["Shots"].BaseValue = Shots;
        DynamicVars["Explosions"].BaseValue = Explosions;
        DynamicVars["Marks"].BaseValue = _marks.Values.Sum();
        DynamicVars["Delayed"].BaseValue = _delayed.Count;
    }
    internal static async Task<SignalSupportPower?> Ensure(PlayerChoiceContext context, CardModel card) =>
        !OathflameRuntime.CanAct(card.Owner) ? null : card.Owner.Creature.GetPower<SignalSupportPower>() ??
        await PowerCmd.Apply<SignalSupportPower>(context, card.Owner.Creature, 1, card.Owner.Creature, card);
    internal void PrepareFlames(int count) { Flames = Math.Max(Flames, count); RefreshDisplay(); }
    internal void PrepareShots(int count) { Shots = Math.Max(Shots, count); RefreshDisplay(); }
    internal void PrepareExplosions(int count) { Explosions = Math.Max(Explosions, count); RefreshDisplay(); }
    internal void Mark(IEnumerable<Creature> targets, int count)
    {
        foreach (var target in targets.Where(t => t.IsAlive)) _marks[target] = Math.Max(_marks.GetValueOrDefault(target), count);
        RefreshDisplay();
    }
    internal int MarksOn(Creature target) => _marks.GetValueOrDefault(target);
    internal void Delay(Creature target, int damage) { if (target.IsAlive) _delayed.Add((target, damage)); RefreshDisplay(); }
    internal bool ReserveTraversal()
    {
        if (TraversalGenerated) return false;
        return TraversalGenerated = true;
    }
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || play.PlayIndex != 0 || play.Card.Type != CardType.Attack) return Task.CompletedTask;
        int explosions = !play.IsAutoPlay && play.Card is SignalCard { SignalColor: SignalColor.Red } ? Explosions : 0;
        _plays[play.Card] = new(Flames > 0, Shots > 0, explosions);
        if (Flames > 0) Flames--;
        if (Shots > 0) Shots--;
        if (explosions > 0) Explosions = 0;
        RefreshDisplay();
        return Task.CompletedTask;
    }
    // Called once per damage segment, after all targets in an AoE have been processed.
    internal async Task AfterSegment(PlayerChoiceContext context, CardModel card, IReadOnlyList<Creature> hit)
    {
        if (!OathflameRuntime.CanAct(Owner.Player!) || hit.Count == 0) return;
        foreach (var target in hit.Distinct())
        {
            if (_plays.TryGetValue(card, out var active)) active.Hit.Add(target);
            if (!target.IsAlive || !_marks.TryGetValue(target, out int marks) || marks <= 0) continue;
            _marks[target] = marks - 1;
            await CreatureCmd.Damage(context, new[] { target }, 2, ValueProp.Unpowered, Owner);
            if (!OathflameRuntime.CanAct(Owner.Player!)) return;
        }
        if (_plays.TryGetValue(card, out var state) && state.Explosions > 0)
        {
            state.Explosions--;
            await CreatureCmd.Damage(context, CombatState.HittableEnemies.ToArray(), 1, ValueProp.Unpowered, Owner);
        }
        RefreshDisplay();
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.Owner != Owner.Player || play.PlayIndex != play.PlayCount - 1 || !_plays.TryGetValue(play.Card, out var state)) return;
        // Snapshot: appended attacks cannot extend the set of targets or spend another card allowance.
        foreach (var target in state.Hit.ToArray())
        {
            if (!OathflameRuntime.CanAct(Owner.Player!)) break;
            if (state.Shot && target.IsAlive) await DamageCmd.Attack(3).FromCard(play.Card).Targeting(target).Execute(context);
            if (state.Flame && target.IsAlive && OathflameRuntime.CanAct(Owner.Player!))
                await CreatureCmd.Damage(context, new[] { target }, 2, ValueProp.Unpowered, Owner);
        }
        _plays.Remove(play.Card);
    }
    internal void Cleanup(CardModel card) => _plays.Remove(card);
    public override async Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        var pending = _delayed.ToArray();
        _delayed.Clear();
        foreach (var (target, damage) in pending)
        {
            if (!OathflameRuntime.CanAct(Owner.Player!)) break;
            if (target.IsAlive) await CreatureCmd.Damage(context, new[] { target }, damage, ValueProp.Unpowered, Owner);
        }
        RefreshDisplay();
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        Flames = Shots = Explosions = 0;
        _plays.Clear(); _marks.Clear(); _delayed.Clear();
        RefreshDisplay();
        await PowerCmd.Remove(this);
    }
}

// The engine calls this overload once for each attack segment (including each AoE segment).
// Ability damage is unpowered and must never recurse into support triggers.
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)])]
internal static class SignalSegmentPatch
{
    [HarmonyPostfix] static void Postfix(PlayerChoiceContext choiceContext, ValueProp props, Creature? dealer, CardModel? cardSource, ref Task<IEnumerable<DamageResult>> __result)
    {
        if (props.IsPoweredAttack() && cardSource != null && dealer == cardSource.Owner.Creature && dealer.GetPower<SignalSupportPower>() is { } support)
            __result = Finish(__result, choiceContext, cardSource, support);
    }
    private static async Task<IEnumerable<DamageResult>> Finish(Task<IEnumerable<DamageResult>> task, PlayerChoiceContext context, CardModel card, SignalSupportPower support)
    {
        var results = (await task).ToArray();
        await support.AfterSegment(context, card, results.Select(r => r.Receiver).Distinct().ToArray());
        return results;
    }
}
