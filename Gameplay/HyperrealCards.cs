using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class SwiftAssault() : SignalCard(CardType.Attack, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Red;
    protected override string ArtName => "red";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move), new DamageVar("FollowupDamage", 3, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("瞬弹急袭·红", "超刻 · 红球\n造成 {Damage:diff()} 点伤害。\n三消：随后对同一目标再造成 {FollowupDamage:diff()} 点伤害，共 3 次。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
        if (strength == 3 && play.Target!.IsAlive && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await DamageCmd.Attack(DynamicVars["FollowupDamage"].BaseValue).FromCard(this).Targeting(play.Target).WithHitCount(3).Execute(context);
    }
}

public sealed class HyperdimensionalSpace() : ChaosCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "core";
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromCard<RealmTraversal>(), new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("超维空间", "生成一张现界穿越，满手时放入弃牌堆。此后每次手动三消效果结算后获得 1 视点（超算也可触发）。每满 3 点扣除 3 点，对所有敌人造成 10 点能力伤害。\n视点跨回合保留，重放不重复累计。能力不叠加；重复打出仍生成现界穿越。", ("flavor", "你们的时间，由我掌控！"));
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive) return;
        if (Owner.Creature.GetPower<HyperdimensionalPower>() == null)
            await PowerCmd.Apply<HyperdimensionalPower>(context, Owner.Creature, 1, Owner.Creature, this);
        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive) return;
        await CardPileCmd.AddGeneratedCardsToCombat([CombatState!.CreateCard<RealmTraversal>(Owner)], PileType.Hand, Owner);
    }
}

public sealed class CausalConvergence() : ChaosCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "yellow";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("因果收束", "造成 {Damage:diff()} 点伤害。选择红色或黄色，在球区右端生成 2 张该颜色的临时基础信号球。\n临时球仍为 1 费，打出后消耗，弃置或回合结束时移除；满手停止生成。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target is not { IsAlive: true } || CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive) return;
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).Targeting(play.Target).Execute(context);
        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive || Owner.PlayerCombatState!.Hand.Cards.Count >= CardPile.MaxCardsInHand) return;
        var chosen = await CardSelectCmd.FromChooseACardScreen(context, [ModelDb.Card<RedSignal>(), ModelDb.Card<YellowSignal>()], Owner);
        if (chosen is SignalCard signal)
            await TemporarySignals.Generate(context, Owner, 2, _ => signal.SignalColor);
    }
}

public sealed class TemporalFinality() : BurstCard(4, CardType.Attack, TargetType.AllEnemies)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "ultimate";
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(26, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("时序终焉", "超刻 · 大招\n保留。消耗 4 充能。对所有敌人造成 {Damage:diff()} 点伤害。不可自动打出。", ("flavor", "在时间的尽头……湮灭吧！"));
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play) =>
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).TargetingAllOpponents(CombatState!).Execute(context);
}

public sealed class RealmTraversal() : ChaosCard(0, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "super";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
    public override List<(string, string)> Localization => new CardLoc("现界穿越", "保留。获得超算。消耗。\n本场下一张手动信号球获得三消效果，超算不可叠层。");
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        return ResonanceRuntime.GainSupercompute(context, Owner);
    }
}

public sealed class HyperdimensionalPower : ResonancePower
{
    private int _viewpoints;
    public int Viewpoints => _viewpoints;
    public override List<(string, string)> Localization => new PowerLoc("超维空间", "手动三消效果结算后获得 1 视点，每 3 点触发全体 10 点能力伤害。视点跨回合保留，重放不重复累计，能力不叠加。", "手动三消效果结算后获得 1 视点，每 3 点触发全体 10 点能力伤害。视点跨回合保留，重放不重复累计，能力不叠加。");
    internal async Task AfterTriple(PlayerChoiceContext context)
    {
        _viewpoints++;
        while (_viewpoints >= 3)
        {
            _viewpoints -= 3;
            if (CombatManager.Instance.IsOverOrEnding || !Owner.IsAlive) return;
            await CreatureCmd.Damage(context, CombatState.HittableEnemies.ToArray(), 10, ValueProp.Unpowered, Owner);
        }
    }
}
