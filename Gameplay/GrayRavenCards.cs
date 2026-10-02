using BaseLib.Abstracts;
using BaseLib.Utils;
using ChaosPrototype.Core;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public abstract class BurstCard : ChaosCard
{
    protected BurstCard(int charge, CardType type, TargetType target, CardRarity rarity = CardRarity.Rare)
        : base(0, type, rarity, target) => CustomResources<Charge>.SetCanonicalCost(this, charge);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsAutoPlay || CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead) return;
        if (TargetType == TargetType.AnyEnemy && (play.Target == null || !play.Target.IsAlive)) return;
        await BurstEffect(context, play);
    }
    protected abstract Task BurstEffect(PlayerChoiceContext context, CardPlay play);
}

public sealed class FrostBlade() : SignalCard(CardType.Attack, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Red;
    protected override string ArtName => "red";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move), new DamageVar("TripleDamage", 12, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("霜刃·红信号", "鸦羽 · 红球\n造成 {Damage:diff()} 点伤害。\n三消：改为 {TripleDamage:diff()} 点伤害，获得 1 充能。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await DamageCmd.Attack(DynamicVars[strength == 3 ? "TripleDamage" : "Damage"].BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
        if (strength == 3 && !CombatManager.Instance.IsOverOrEnding && !Owner.Creature.IsDead)
            CustomResources<Charge>.Get(Owner.PlayerCombatState!).ModifyAmount(1);
    }
}

public sealed class RepulsiveBeam() : SignalCard(CardType.Attack, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Red;
    protected override string ArtName => "red";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move), new DamageVar("TripleDamage", 10, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("相斥光束", "仰光 · 红球\n造成 {Damage:diff()} 点伤害。\n三消：改为 {TripleDamage:diff()} 点伤害，并施加 2 层虚弱。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await DamageCmd.Attack(DynamicVars[strength == 3 ? "TripleDamage" : "Damage"].BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
        if (strength == 3 && play.Target!.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<WeakPower>(context, play.Target, 2, Owner.Creature, this);
    }
}

public sealed class RetreatShot() : SignalCard(CardType.Attack, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override string ArtName => "yellow";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move), new DamageVar("TripleDamage", 8, ValueProp.Move), new BlockVar(3, ValueProp.Move), new BlockVar("TripleBlock", 6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("后跳射击", "乱数 · 黄球\n造成 {Damage:diff()} 点伤害，获得 {Block:diff()} 点格挡。\n三消：改为 {TripleDamage:diff()} 点伤害和 {TripleBlock:diff()} 点格挡。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await DamageCmd.Attack(DynamicVars[strength == 3 ? "TripleDamage" : "Damage"].BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
        if (!CombatManager.Instance.IsOverOrEnding && !Owner.Creature.IsDead)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars[strength == 3 ? "TripleBlock" : "Block"].BaseValue, ValueProp.Move, play);
    }
}

public sealed class GlacialForm() : ChaosCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "blue";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4, ValueProp.Move)];
    public override List<(string, string)> Localization => [..(List<(string, string)>)new CardLoc("极寒形态", "鸦羽 · 技能\n获得 {Block:diff()} 点格挡。选择一颗手中信号球，将所有同色球移到最右侧，各组内部顺序不变。"), ("selectionPrompt", "选择一颗球：将所有同色球移到最右侧")];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["Block"].BaseValue, ValueProp.Move, play);
        var selected = (await CardSelectCmd.FromHand(context, Owner, new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionPrompt"), 0, 1), c => c is SignalCard, this)).FirstOrDefault();
        if (selected is not SignalCard signal || CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead) return;
        var hand = Owner.PlayerCombatState!.Hand;
        var order = RavenTurnRules.Gather(hand.Cards, signal.SignalColor, SignalRuntime.Color);
        foreach (var card in order) hand.MoveToBottomInternal(card);
        hand.InvokeContentsChanged();
        if (LocalContext.IsMe(Owner) && NPlayerHand.Instance is { } node)
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

public sealed class GoddessConnection() : ChaosCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "core";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("女神连接系统", "仰光 · 技能\n获得 {Block:diff()} 点格挡。本回合下一次自然三消额外获得 7 点格挡。奖励不可叠加；超算单消不触发。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["Block"].BaseValue, ValueProp.Move, play);
        if (!CombatManager.Instance.IsOverOrEnding && !Owner.Creature.IsDead) RavenRuntime.Get(Owner).ArmConnection();
    }
}

public sealed class TacticalCalculation() : ChaosCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "super";
    public override List<(string, string)> Localization => [..(List<(string, string)>)new CardLoc("战术运算", "乱数 · 技能\n从抽牌堆依次选择至多 2 张信号球加入手牌，然后弃置 1 张手牌。未取牌则不弃牌。手牌满时停止取牌。"), ("selectionPrompt", "选择下一颗球加入手牌最右侧（可不选）")];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        int taken = 0;
        for (int i = 0; i < 2; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead || Owner.PlayerCombatState is not { } state) return;
            if (state.Hand.Cards.Count >= CardPile.MaxCardsInHand) break;
            var available = state.DrawPile.Cards.Where(c => c is SignalCard).ToArray();
            if (available.Length == 0) break;
            var chosen = (await CardSelectCmd.FromSimpleGrid(context, available, Owner, new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionPrompt"), 0, 1))).FirstOrDefault();
            if (chosen == null) break;
            if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead) return;
            if (chosen.Pile != state.DrawPile) break;
            await CardPileCmd.Add(chosen, PileType.Hand);
            taken++;
        }
        if (taken == 0 || CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead) return;
        var discard = await CardSelectCmd.FromHandForDiscard(context, Owner, new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1), null, this);
        await CardCmd.Discard(context, discard);
    }
}

public sealed class GlacialBloom() : BurstCard(4, CardType.Attack, TargetType.AllEnemies)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "ultimate";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(18, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("刹那冰华", "鸦羽 · 大招\n保留。消耗 4 充能。对所有敌人造成 {Damage:diff()} 点伤害。本回合每次自然三消增加 6 点基础伤害，最多增加 12。不可自动打出。");
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play) =>
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue + RavenRuntime.Get(Owner).IceBonus).FromCard(this).TargetingAllOpponents(CombatState!).Execute(context);
}

public sealed class ArcadiaGate() : BurstCard(3, CardType.Skill, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "core";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(18, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("阿卡狄亚之门", "仰光 · 大招\n保留。消耗 3 充能。获得 {Block:diff()} 点格挡，回复 3 点生命。消耗。不可自动打出。");
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["Block"].BaseValue, ValueProp.Move, play);
        if (!CombatManager.Instance.IsOverOrEnding && !Owner.Creature.IsDead) await CreatureCmd.Heal(Owner.Creature, 3);
    }
}

public sealed class OrbitalStrike() : BurstCard(3, CardType.Attack, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "ultimate";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("轨道打击", "乱数 · 大招\n保留。消耗 3 充能。对同一敌人造成 {Damage:diff()} 点伤害，共 4 次。不可自动打出。");
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play) =>
        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue).FromCard(this).Targeting(play.Target!).WithHitCount(4).Execute(context);
}
