using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public abstract class ExpandedSignal(CardType type, TargetType target, CardRarity rarity) : SignalCard(type, target, rarity)
{
    protected override string ArtName => $"cards/{GetType().Name}.png";
    protected decimal Damage(int strength) => DynamicVars[strength == 3 ? "TripleDamage" : "Damage"].BaseValue;
    protected async Task Hit(PlayerChoiceContext context, CardPlay play, decimal damage, int hits = 1)
    {
        if (OathflameRuntime.CanAct(Owner) && play.Target is { IsAlive: true })
            await DamageCmd.Attack(damage).FromCard(this).Targeting(play.Target).WithHitCount(hits).Execute(context);
    }
    protected async Task HitAll(PlayerChoiceContext context, decimal damage, int hits = 1)
    {
        if (OathflameRuntime.CanAct(Owner))
            await DamageCmd.Attack(damage).FromCard(this).TargetingAllOpponents(CombatState!).WithHitCount(hits).Execute(context);
    }
}

public sealed class ShatteringImpact() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Common)
{
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move), new DamageVar("TripleDamage", 7, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("破碎冲击·黄", "鸦羽 · 黄球\n造成 {Damage:diff()} 点伤害 2 次。三消：每次改为 {TripleDamage:diff()} 点。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await Hit(context, play, Damage(strength), 2);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(1); DynamicVars["TripleDamage"].UpgradeValueBy(1); }
}

public sealed class GlacialCirculation() : ExpandedSignal(CardType.Attack, TargetType.AllEnemies, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(2, ValueProp.Move), new DamageVar("TripleDamage", 3, ValueProp.Move), new DamageVar("TripleFinalDamage", 4, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("冰魄环流·蓝", "鸦羽 · 蓝球\n对所有敌人造成 {Damage:diff()} 点伤害 3 次。三消：前两次各 {TripleDamage:diff()} 点，最后一次 {TripleFinalDamage:diff()} 点。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await HitAll(context, Damage(strength), 2);
        await HitAll(context, DynamicVars[strength == 3 ? "TripleFinalDamage" : "Damage"].BaseValue);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(1); DynamicVars["TripleDamage"].UpgradeValueBy(1); DynamicVars["TripleFinalDamage"].UpgradeValueBy(1); }
}

public sealed class RadiantDaybreak() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Red;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move), new DamageVar("TripleDamage", 10, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("旋光灿昼·红", "誓焰 · 红球\n造成 {Damage:diff()} 点伤害。三消：改为 {TripleDamage:diff()} 点，生成一张飞光续斩。出牌开始时处于飞行则额外造成 3 点伤害。\n满手时续斩进入弃牌堆；回合结束移除未使用的续斩。");
    private bool _flying;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromCard<SoaringContinuation>()];
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        if (play.PlayIndex == 0) _flying = Owner.Creature.GetPower<OathflameFlightPower>() != null;
        await Hit(context, play, Damage(strength) + (_flying ? 3 : 0));
        if (strength != 3 || !OathflameRuntime.CanAct(Owner)) return;
        if (Owner.Creature.GetPower<ContinuationCleanupPower>() == null)
            await PowerCmd.Apply<ContinuationCleanupPower>(context, Owner.Creature, 1, Owner.Creature, this);
        if (OathflameRuntime.CanAct(Owner))
            await CardPileCmd.AddGeneratedCardsToCombat([CombatState!.CreateCard<SoaringContinuation>(Owner)], PileType.Hand, Owner);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(4); }
}

public sealed class OpeningStance() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Common)
{
    public override SignalColor SignalColor => SignalColor.Red;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7, ValueProp.Move), new DamageVar("TripleDamage", 11, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("起手式·红", "红莲 · 红球\n造成 {Damage:diff()} 点伤害 1 次。三消：改为 {TripleDamage:diff()} 点伤害 1 次。\n若本回合在打出此牌前已经三消，追加 2 点伤害 2 次。本次三消不满足自身条件；重放继承该条件。");
    private bool _priorTriple;
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        if (play.PlayIndex == 0) _priorTriple = SignalRuntime.PriorTriple(this);
        await Hit(context, play, Damage(strength), strength == 3 ? 1 : 1);
        if (_priorTriple) await Hit(context, play, 2, 2);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(4); }
}

public sealed class ContinuousFire() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Red;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3, ValueProp.Move), new DamageVar("TripleDamage", 3, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("连续射击·红", "乱数 · 红球\n造成 {Damage:diff()} 点伤害 2 次。三消：改为 {TripleDamage:diff()} 点伤害 4 次。\n若本回合在打出此牌前已经三消，追加 2 点伤害 2 次。本次三消不满足自身条件；重放继承该条件。");
    private bool _priorTriple;
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        if (play.PlayIndex == 0) _priorTriple = SignalRuntime.PriorTriple(this);
        await Hit(context, play, Damage(strength), strength == 3 ? 4 : 2);
        if (_priorTriple) await Hit(context, play, 2, 2);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(1); DynamicVars["TripleDamage"].UpgradeValueBy(1); }
}

public sealed class WaveSlash() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move), new DamageVar("TripleDamage", 6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("波动斩·蓝", "红莲 · 蓝球\n造成 {Damage:diff()} 点伤害。本回合之后 2 张攻击牌结算后，对其命中过的每名敌人造成 2 点能力伤害。\n三消：改为 {TripleDamage:diff()} 点伤害、3 张攻击牌。重复获得取剩余次数与新次数的较大值；重放不补次数。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await Hit(context, play, Damage(strength));
        if (play.PlayIndex == 0 && await SignalSupportPower.Ensure(context, this) is { } support) support.PrepareFlames(strength == 3 ? 3 : 2);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(4); }
}

public sealed class TransferCharge() : ExpandedSignal(CardType.Skill, TargetType.Self, CardRarity.Uncommon)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Attacks", 2), new DynamicVar("TripleAttacks", 3)];
    public override SignalColor SignalColor => SignalColor.Yellow;
    public override List<(string, string)> Localization => new CardLoc("转移充能·黄", "仰光 · 黄球\n本回合之后 {Attacks:diff()} 张攻击牌结算后，对其命中过的每名敌人追加一次 3 点攻击伤害。三消：改为 {TripleAttacks:diff()} 张。\n按整张牌计次，多段与重放不多消费次数。重复获得取剩余次数与新次数的较大值；重放不补次数。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        if (play.PlayIndex == 0 && await SignalSupportPower.Ensure(context, this) is { } support) support.PrepareShots(DynamicVars[strength == 3 ? "TripleAttacks" : "Attacks"].IntValue);
    }
    protected override void OnUpgrade() { DynamicVars["Attacks"].UpgradeValueBy(1); DynamicVars["TripleAttacks"].UpgradeValueBy(1); }
}

public sealed class GrayRavenField() : ExpandedSignal(CardType.Attack, TargetType.AllEnemies, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3, ValueProp.Move), new DamageVar("TripleDamage", 6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("灰鸦领域·蓝", "仰光 · 蓝球\n对所有敌人造成 {Damage:diff()} 点伤害，然后各施加 2 层领域标记。三消：改为 {TripleDamage:diff()} 点伤害、3 层标记。\n本回合你的每段攻击命中时，消费目标一层标记，造成 2 点能力伤害。重复施加取较大值；重放不补标记。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await HitAll(context, Damage(strength));
        if (play.PlayIndex == 0 && await SignalSupportPower.Ensure(context, this) is { } support) support.Mark(CombatState!.HittableEnemies.ToArray(), strength == 3 ? 3 : 2);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(3); }
}

public sealed class EclipticGrace() : ExpandedSignal(CardType.Attack, TargetType.AllEnemies, CardRarity.Common)
{
    public override SignalColor SignalColor => SignalColor.Red;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move), new DamageVar("TripleDamage", 9, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("黄道恩典·红", "极昼 · 红球\n对所有敌人造成 {Damage:diff()} 点伤害。三消：改为 {TripleDamage:diff()} 点。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await HitAll(context, Damage(strength));
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(3); }
}

public sealed class ExemptionSpace() : ExpandedSignal(CardType.Attack, TargetType.AllEnemies, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3, ValueProp.Move), new DamageVar("TripleDamage", 6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("豁免空间·黄", "流光 · 黄球\n对所有敌人造成 {Damage:diff()} 点伤害，施加 1 层虚弱。三消：改为 {TripleDamage:diff()} 点伤害、2 层虚弱。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await HitAll(context, Damage(strength));
        if (OathflameRuntime.CanAct(Owner))
            foreach (var target in CombatState!.HittableEnemies.ToArray())
                await PowerCmd.Apply<WeakPower>(context, target, strength == 3 ? 2 : 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(2); DynamicVars["TripleDamage"].UpgradeValueBy(3); }
}

public sealed class FictionalBarrier() : ExpandedSignal(CardType.Skill, TargetType.AnyEnemy, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move), new BlockVar("TripleBlock", 9, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("虚构障碍·蓝", "流光 · 蓝球\n获得 {Block:diff()} 点格挡，对一名敌人施加 1 层虚弱。三消：改为 {TripleBlock:diff()} 点格挡、2 层虚弱。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars[strength == 3 ? "TripleBlock" : "Block"].BaseValue, ValueProp.Move, play);
        if (OathflameRuntime.CanAct(Owner) && play.Target is { IsAlive: true })
            await PowerCmd.Apply<WeakPower>(context, play.Target, strength == 3 ? 2 : 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() { DynamicVars["Block"].UpgradeValueBy(3); DynamicVars["TripleBlock"].UpgradeValueBy(3); }
}

public sealed class ElectricInduction() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move), new DamageVar("TripleDamage", 7, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("电力感应·蓝", "乱数 · 蓝球\n造成 {Damage:diff()} 点伤害，施加 1 层易伤。本回合结束时，对该敌人造成 3 点能力伤害。三消：改为 {TripleDamage:diff()} 点伤害、5 点延迟伤害。\n多次施放分别结算，目标死亡则对应延迟伤害取消。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await Hit(context, play, Damage(strength));
        if (!OathflameRuntime.CanAct(Owner) || play.Target is not { IsAlive: true }) return;
        await PowerCmd.Apply<VulnerablePower>(context, play.Target, 1, Owner.Creature, this);
        if (await SignalSupportPower.Ensure(context, this) is { } support) support.Delay(play.Target, strength == 3 ? 5 : 3);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(4); }
}

public sealed class ShadowManeuver() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Common)
{
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3, ValueProp.Move), new DamageVar("TripleDamage", 3, ValueProp.Move), new BlockVar(5, ValueProp.Move), new BlockVar("TripleBlock", 8, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("掠影机动·黄", "超刻 · 黄球\n造成 {Damage:diff()} 点伤害，获得 {Block:diff()} 点格挡。三消：格挡改为 {TripleBlock:diff()} 点，然后追加 4 点伤害。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await Hit(context, play, Damage(strength));
        if (!OathflameRuntime.CanAct(Owner)) return;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars[strength == 3 ? "TripleBlock" : "Block"].BaseValue, ValueProp.Move, play);
        if (strength == 3) await Hit(context, play, 4);
    }
    protected override void OnUpgrade() { DynamicVars["Block"].UpgradeValueBy(3); DynamicVars["TripleBlock"].UpgradeValueBy(3); }
}

public sealed class FallingFire() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new DamageVar("TripleDamage", 12, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("坠火爆裂·蓝", "超刻 · 蓝球\n造成 {Damage:diff()} 点伤害。三消：改为 {TripleDamage:diff()} 点。\n每回合首次手动三消此牌，生成一张现界穿越。所有同名牌共享次数，重放不重复生成。满手时放入弃牌堆。");
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromCard<RealmTraversal>()];
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await Hit(context, play, Damage(strength));
        if (strength == 3 && !play.IsAutoPlay && play.PlayIndex == 0 && await SignalSupportPower.Ensure(context, this) is { } support && support.ReserveTraversal())
            await CardPileCmd.AddGeneratedCardsToCombat([CombatState!.CreateCard<RealmTraversal>(Owner)], PileType.Hand, Owner);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(4); }
}

public sealed class PrecisionVolley() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Common)
{
    public override SignalColor SignalColor => SignalColor.Red;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(2, ValueProp.Move), new DamageVar("TripleDamage", 3, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("精准连射·红", "异火 · 红球\n造成 {Damage:diff()} 点伤害 3 次。三消：改为 {TripleDamage:diff()} 点伤害 4 次。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await Hit(context, play, Damage(strength), strength == 3 ? 4 : 3);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(1); DynamicVars["TripleDamage"].UpgradeValueBy(1); }
}

public sealed class ThermalShot() : ExpandedSignal(CardType.Attack, TargetType.AnyEnemy, CardRarity.Uncommon)
{
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3, ValueProp.Move), new DamageVar("TripleDamage", 5, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("热场射击·黄", "异火 · 黄球\n造成 {Damage:diff()} 点伤害，本回合结束时对该敌人造成 4 点能力伤害，获得 3 段爆炎准备。三消：改为 {TripleDamage:diff()} 点直伤、7 点延迟伤害、4 段准备。\n本回合下一张手动红色攻击球，每段攻击后对全体造成 1 点能力伤害，最多触发准备段数；此牌结算后剩余准备作废。重复准备取较大值，重放不补准备。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await Hit(context, play, Damage(strength));
        if (await SignalSupportPower.Ensure(context, this) is not { } support) return;
        if (play.Target is { IsAlive: true }) support.Delay(play.Target, strength == 3 ? 7 : 4);
        if (play.PlayIndex == 0) support.PrepareExplosions(strength == 3 ? 4 : 3);
    }
    protected override void OnUpgrade() { DynamicVars["Damage"].UpgradeValueBy(3); DynamicVars["TripleDamage"].UpgradeValueBy(4); }
}
