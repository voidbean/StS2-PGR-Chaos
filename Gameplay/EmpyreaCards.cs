using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class HymnPrayer() : ChaosCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/HymnPrayer.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".oaths")), new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("颂歌祷告", "获得 {Block:diff()} 点格挡。记录之后两次手动消球的主牌颜色，完成后触发宣誓。\n同色：全体 6 点能力伤害，获得 8 格挡。异色：全体 12 点能力伤害，受到 3 点可格挡自伤；完整格挡则获得超算。", ("oaths", "祷告跨回合保留，普通单消也可输入。主牌结算后记录，重放不多记。重复使用仍获得立即格挡，但不叠加祷告、不重置已记录颜色。自伤不是攻击，不触发极限闪避，不受飞行减免。"), ("flavor", "一切灾厄都将跟随我回归于虚无"));
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["Block"].BaseValue, ValueProp.Move, play);
        if (OathflameRuntime.CanAct(Owner) && Owner.Creature.GetPower<PrayerPower>() == null)
            await PowerCmd.Apply<PrayerPower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
}

public sealed class FeatherMass() : SignalCard(CardType.Skill, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Yellow;
    protected override string ArtName => "cards/FeatherMass.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3, ValueProp.Move), new BlockVar("TripleBlock", 6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("光羽弥撒·黄", "极昼 · 黄球\n获得 {Block:diff()} 点格挡，抽 1 张牌。三消：改为 {TripleBlock:diff()} 点格挡，抽 2 张牌。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars[strength == 3 ? "TripleBlock" : "Block"].BaseValue, ValueProp.Move, play);
        if (OathflameRuntime.CanAct(Owner)) await CardPileCmd.Draw(context, strength == 3 ? 2 : 1, Owner);
    }
}

public sealed class FadingGospel() : SignalCard(CardType.Skill, TargetType.AnyEnemy)
{
    public override int MaxUpgradeLevel => 0;
    public override SignalColor SignalColor => SignalColor.Blue;
    protected override string ArtName => "cards/FadingGospel.png";
    public override List<(string, string)> Localization => new CardLoc("阑珊福音·蓝", "极昼 · 蓝球\n获得 1 充能，对目标施加 1 层虚弱。三消：改为 2 充能、2 层虚弱。");
    protected override async Task Effect(PlayerChoiceContext context, CardPlay play, int strength)
    {
        int amount = strength == 3 ? 2 : 1;
        if (!OathflameRuntime.CanAct(Owner)) return;
        CustomResources<Charge>.Get(Owner.PlayerCombatState!).ModifyAmount(amount);
        if (play.Target is { IsAlive: true }) await PowerCmd.Apply<WeakPower>(context, play.Target, amount, Owner.Creature, this);
    }
}

public sealed class GarlandsSea() : BurstCard(4, CardType.Skill, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/GarlandsSea.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(10, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".limits"))];
    public override List<(string, string)> Localization => new CardLoc("伽蓝之海", "保留。消耗 4 充能。获得 {Block:diff()} 点格挡。本回合后续至多 3 次手动消球各蓄积 4 点伤害，三消改为 8 点。回合结束对所有敌人造成 12＋蓄积值的能力伤害。\n此后本回合打出的信号球主牌结算后消耗。不可自动打出。", ("limits", "球的辅牌仍正常弃置；第四次及以后主牌仍消耗，不再蓄积。自动打出的球不蓄积但仍消耗。原生重放不多计输入。重复使用仍支付充能、获得格挡，只保留一次回合末爆发，不重置蓄积值和次数。"));
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["Block"].BaseValue, ValueProp.Move, play);
        if (OathflameRuntime.CanAct(Owner) && Owner.Creature.GetPower<GarlandsSeaPower>() == null)
            await PowerCmd.Apply<GarlandsSeaPower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
}

public sealed class RadiantCeremony() : ChaosCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/RadiantCeremony.png";
    public override List<(string, string)> Localization => new CardLoc("辉光纪礼", "抽 2 张牌，然后可将手中的 1 张信号球移到球区最右端。其他手牌相对顺序不变。", ("selectionPrompt", "可选择一颗信号球移到最右端"));
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        await CardPileCmd.Draw(context, 2, Owner);
        if (!OathflameRuntime.CanAct(Owner)) return;
        var selected = (await CardSelectCmd.FromHand(context, Owner, new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionPrompt"), 0, 1), c => c is SignalCard, this)).FirstOrDefault();
        if (!OathflameRuntime.CanAct(Owner) || selected is not SignalCard || selected.Pile != Owner.PlayerCombatState!.Hand) return;
        var order = RavenTurnRules.MoveToRight(Owner.PlayerCombatState.Hand.Cards, selected);
        UI.HandOrder.Apply(Owner, order);
    }
}
