using BaseLib.Abstracts;
using BaseLib.Utils;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

[Pool(typeof(ChaosCardPool))]
public abstract class ChaosCard(int cost, CardType type, CardRarity rarity, TargetType target) : CustomCardModel(cost, type, rarity, target)
{
    protected abstract string ArtName { get; }
    public override string CustomPortraitPath => Main.Art(ArtName);
    public override string PortraitPath => Main.Art(ArtName);
    public override string BetaPortraitPath => Main.Art(ArtName);
}

public abstract class SignalCard(CardType type, TargetType target) : ChaosCard(1, type, CardRarity.Common, target)
{
    public abstract SignalColor SignalColor { get; }
    protected sealed override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var enhanced = await SignalRuntime.Resolve(context, this, play);
        if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead) return;
        if (TargetType == TargetType.AnyEnemy && (play.Target == null || !play.Target.IsAlive)) return;
        await Effect(context, play, enhanced);
    }
    protected abstract Task Effect(PlayerChoiceContext context, CardPlay play, bool enhanced);
}
public sealed class RedSignal() : SignalCard(CardType.Attack, TargetType.AnyEnemy)
{
    public override SignalColor SignalColor => SignalColor.Red;
    protected override string ArtName => "red";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move), new DamageVar("TripleDamage", 12, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("红色信号球", "造成 {Damage:diff()} 点伤害。\n三消：改为 {TripleDamage:diff()} 点伤害。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, bool enhanced) =>
        await DamageCmd.Attack(DynamicVars[enhanced ? "TripleDamage" : "Damage"].BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(6); }
}
public sealed class YellowSignal() : SignalCard(CardType.Skill, TargetType.Self)
{
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override string ArtName => "yellow";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new BlockVar("TripleBlock", 10, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("黄色信号球", "获得 {Block:diff()} 点格挡。\n三消：改为 {TripleBlock:diff()} 点格挡。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, bool enhanced) =>
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars[enhanced ? "TripleBlock" : "Block"].BaseValue, ValueProp.Move, play);
    protected override void OnUpgrade() { DynamicVars["Block"].UpgradeValueBy(3); DynamicVars["TripleBlock"].UpgradeValueBy(6); }
}
public sealed class BlueSignal() : SignalCard(CardType.Attack, TargetType.AnyEnemy)
{
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override string ArtName => "blue";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3, ValueProp.Move), new DamageVar("TripleDamage", 6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("蓝色信号球", "造成 {Damage:diff()} 点伤害，获得 1 充能。\n三消：改为 {TripleDamage:diff()} 点伤害和 2 充能。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, bool enhanced)
    {
        await DamageCmd.Attack(DynamicVars[enhanced ? "TripleDamage" : "Damage"].BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
        if (!CombatManager.Instance.IsOverOrEnding && !Owner.Creature.IsDead && Owner.PlayerCombatState is { } state)
            CustomResources<Charge>.Get(state).ModifyAmount(enhanced ? 2 : 1);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(2); DynamicVars["TripleDamage"].UpgradeValueBy(4); }
}
public sealed class SupercomputeCard() : ChaosCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string ArtName => "super";
    public override List<(string, string)> Localization => new CardLoc("超算", "本场下一张手动打出的信号球获得三消强化。不可叠层；与自然三消重叠仍消耗且只强化一次。");
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        CustomResources<Supercompute>.Get(Owner.PlayerCombatState!).Amount = 1;
        return Task.CompletedTask;
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
public sealed class Ultimate : ChaosCard
{
    public Ultimate() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) => CustomResources<Charge>.SetCanonicalCost(this, 3);
    protected override string ArtName => "ultimate";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(18, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("信号爆发", "保留。消耗 3 充能，造成 {Damage:diff()} 点伤害。\n原型中不可自动打出。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsAutoPlay || play.Target == null || !play.Target.IsAlive) return;
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).Targeting(play.Target).Execute(context);
    }
    protected override void OnUpgrade() => DynamicVars["Damage"].UpgradeValueBy(6);
}
