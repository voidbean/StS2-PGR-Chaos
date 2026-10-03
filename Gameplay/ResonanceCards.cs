using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class PerfectDodge() : ChaosCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/PerfectDodge.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("极限闪避", "获得 {Block:diff()} 点格挡。直到你的下个回合开始，首次用格挡完整挡住一段非零攻击伤害时，获得超算。姿态不可叠加。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["Block"].BaseValue, ValueProp.Move, play);
        if (!CombatManager.Instance.IsOverOrEnding && Owner.Creature.IsAlive && Owner.Creature.GetPower<PerfectDodgePower>() == null)
            await PowerCmd.Apply<PerfectDodgePower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
}

public sealed class DeadlineTimer() : ChaosCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/DeadlineTimer.png";
    public override List<(string, string)> Localization => new CardLoc("死线计时", "获得 2 张随机颜色的临时基础信号球。从下回合开始，隔回合在抽牌后获得 3 张随机同色的临时基础信号球。持续效果不叠加。\n临时球仍为 1 费，打出后消耗，弃置或回合结束时移除；满手停止生成。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var power = Owner.Creature.GetPower<DeadlinePower>() ?? await PowerCmd.Apply<DeadlinePower>(context, Owner.Creature, 1, Owner.Creature, this);
        if (power != null) await power.Generate(context, 2, false);
    }
}

public sealed class GloriousAfterglow() : ChaosCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/GloriousAfterglow.png";
    public override List<(string, string)> Localization => new CardLoc("光耀余晖", "每次三消效果结算后，本回合攻击伤害提高 30%。超算强化也可触发。增伤与能力均不叠加。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (Owner.Creature.GetPower<AfterglowPower>() == null)
            await PowerCmd.Apply<AfterglowPower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
}

public sealed class SupercomputeLightning() : ChaosCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/SupercomputeLightning.png";
    public override List<(string, string)> Localization => new CardLoc("超算闪电", "获得超算时，使所有敌人受到的伤害提高 10%，持续到你下一次结束回合。重复触发刷新持续时间，增伤与能力均不叠加。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (Owner.Creature.GetPower<LightningPower>() == null)
            await PowerCmd.Apply<LightningPower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
}
