using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class MoltenQuench() : ChaosCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string ArtName => "red";
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("熔金淬火", "获得超算与飞行。本回合后续 2 次手动三消效果结算后，各对所有敌人造成 4 点能力伤害。重复使用将剩余次数刷新为 2。\n飞行：直到下个自己的回合开始，受到的攻击伤害降低 50%，不叠加。", ("flavor", "汇聚，阳炎之光！"));
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        await ResonanceRuntime.GainSupercompute(context, Owner);
        await OathflameRuntime.Fly(context, this);
        if (!OathflameRuntime.CanAct(Owner)) return;
        var chase = Owner.Creature.GetPower<MoltenChasePower>() ?? await PowerCmd.Apply<MoltenChasePower>(context, Owner.Creature, 1, Owner.Creature, this);
        chase?.Refresh();
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class CoordinatedSlash() : BurstCard(4, CardType.Attack, TargetType.AllEnemies)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "ultimate";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(18, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("协行锐斩", "保留。消耗 4 充能。对所有敌人造成 {Damage:diff()} 点伤害，然后获得飞行。不可自动打出。\n飞行：直到下个自己的回合开始，受到的攻击伤害降低 50%，不叠加。", ("flavor", "展翅翱翔吧"));
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play)
    {
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).TargetingAllOpponents(CombatState!).Execute(context);
        await OathflameRuntime.Fly(context, this);
    }
}

public sealed class EclipseDawn() : BurstCard(4, CardType.Attack, TargetType.AllEnemies)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "ultimate";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(24, ValueProp.Move), new DamageVar("FlyingDamage", 32, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("蚀日彻曙", "保留。消耗 4 充能。对所有敌人造成 {Damage:diff()} 点伤害；处于飞行时改为 {FlyingDamage:diff()} 点，结算后结束飞行。不可自动打出。", ("flavor", "越过迷雾与深渊！"));
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play)
    {
        var flight = Owner.Creature.GetPower<OathflameFlightPower>();
        await DamageCmd.Attack(DynamicVars[flight == null ? "Damage" : "FlyingDamage"].BaseValue).FromCard(this).TargetingAllOpponents(CombatState!).Execute(context);
        if (flight != null) await PowerCmd.Remove(flight);
    }
}

public sealed class BlazingFeathers() : SignalCard(CardType.Attack, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override string ArtName => "yellow";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move), new DamageVar("TripleDamage", 10, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromCard<SoaringContinuation>()];
    public override List<(string, string)> Localization => new CardLoc("赫焰飞芒·黄", "誓焰 · 黄球\n造成 {Damage:diff()} 点伤害。三消：改为 {TripleDamage:diff()} 点伤害，生成一张飞光续斩。满手时放入弃牌堆；回合结束移除未使用的续斩。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await DamageCmd.Attack(DynamicVars[strength == 3 ? "TripleDamage" : "Damage"].BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
        if (strength != 3 || !OathflameRuntime.CanAct(Owner)) return;
        if (Owner.Creature.GetPower<ContinuationCleanupPower>() == null)
            await PowerCmd.Apply<ContinuationCleanupPower>(context, Owner.Creature, 1, Owner.Creature, this);
        if (OathflameRuntime.CanAct(Owner))
            await CardPileCmd.AddGeneratedCardsToCombat([CombatState!.CreateCard<SoaringContinuation>(Owner)], PileType.Hand, Owner);
    }
}

public sealed class SoaringContinuation() : ChaosCard(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "red";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("飞光续斩", "造成 {Damage:diff()} 点伤害，获得 2 充能。消耗。\n回合结束时，移除手牌、抽牌堆和弃牌堆中未使用的此牌。", ("flavor", "切裂黑夜！"));
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner) || play.Target is not { IsAlive: true }) return;
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).Targeting(play.Target).Execute(context);
        if (OathflameRuntime.CanAct(Owner)) CustomResources<Charge>.Get(Owner.PlayerCombatState!).ModifyAmount(2);
    }
}

public sealed class FlowingThunder() : SignalCard(CardType.Skill, TargetType.Self, CardRarity.Rare)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override string ArtName => "blue";
    public override List<(string, string)> Localization => new CardLoc("流火鸣雷·蓝", "誓焰 · 蓝球\n获得飞行。三消：另获得 2 充能。\n飞行：直到下个自己的回合开始，受到的攻击伤害降低 50%，不叠加。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await OathflameRuntime.Fly(context, this);
        if (strength == 3 && OathflameRuntime.CanAct(Owner)) CustomResources<Charge>.Get(Owner.PlayerCombatState!).ModifyAmount(2);
    }
}

public sealed class OmegaCore : ChaosCard
{
    public OmegaCore() : base(0, CardType.Power, CardRarity.Rare, TargetType.Self) => CustomResources<Charge>.SetCanonicalCost(this, 3);
    public override bool PreventAutoPlay => true;
    protected override bool IsPlayable => !IsMutable || Owner?.PlayerCombatState == null || !OathflameRuntime.Omega(Owner).Burned;
    protected override string ArtName => "core";
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromCard<CoreBurnout>(), new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("Ω 核心", "消耗 3 充能。从下个自己的回合起，每回合开始获得 1 能量、1 充能。持续效果不叠加。首次启动将一张核心燃尽随机放入本场抽牌堆。引爆后本场不可重启。不可自动打出。", ("flavor", "没关系，我能坚持下去，这副机体也能让我坚持下去"));
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsAutoPlay || !OathflameRuntime.CanAct(Owner) || OathflameRuntime.Omega(Owner).Burned) return;
        var core = Owner.Creature.GetPower<OmegaCorePower>();
        if (core == null) core = await PowerCmd.Apply<OmegaCorePower>(context, Owner.Creature, 1, Owner.Creature, this);
        if (core == null || !OathflameRuntime.CanAct(Owner)) return;
        var state = OathflameRuntime.Omega(Owner);
        if (state.Generated) return;
        state.Generated = true;
        await CardPileCmd.AddGeneratedCardsToCombat([CombatState!.CreateCard<CoreBurnout>(Owner)], PileType.Draw, Owner, CardPilePosition.Random);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

public sealed class CoreBurnout() : ChaosCard(0, CardType.Attack, CardRarity.Token, TargetType.AllEnemies)
{
    public override int MaxUpgradeLevel => 0;
    public override bool PreventAutoPlay => true;
    protected override bool IsPlayable => IsMutable && Owner?.PlayerCombatState != null && OathflameRuntime.CanBurn(Owner);
    protected override string ArtName => "ultimate";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(40, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("核心燃尽", "仅在 Ω 核心生效且生命高于 10 时可用。失去 10 生命，终止 Ω 核心，对所有敌人造成 {Damage:diff()} 点伤害。消耗。\n本场仅可引爆一次，此后不可重启核心。不可自动打出。", ("flavor", "我好怕我想你们。"));
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsAutoPlay || !OathflameRuntime.CanAct(Owner) || !OathflameRuntime.CanBurn(Owner)) return;
        // Reserve before awaiting: copies and replays share this once-per-combat latch.
        OathflameRuntime.Omega(Owner).Burned = true;
        await CreatureCmd.Damage(context, Owner.Creature, 10, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);
        await PowerCmd.Remove(Owner.Creature.GetPower<OmegaCorePower>());
        if (OathflameRuntime.CanAct(Owner))
            await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).TargetingAllOpponents(CombatState!).Execute(context);
    }
}
