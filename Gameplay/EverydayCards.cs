using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class LuciasCooking() : ChaosCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string ArtName => "cards/LuciasCooking.png";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromCard<Dazed>()];
    public override List<(string, string)> Localization => new CardLoc("露西亚的手作料理",
        "获得 {Energy:diff()} 点能量。将 1 张晕眩放入弃牌堆。消耗。");

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        if (!OathflameRuntime.CanAct(Owner)) return;
        var dazed = Owner.Creature.CombatState!.CreateCard<Dazed>(Owner);
        await CardPileCmd.AddGeneratedCardsToCombat([dazed], PileType.Discard, Owner);
    }
    protected override void OnUpgrade() { DynamicVars["Energy"].UpgradeValueBy(1); }
}

public sealed class SeventyPercent() : ChaosCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override string ArtName => "cards/SeventyPercent.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(7, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("百分之七十",
        "获得 {Block:diff()} 点格挡。70% 概率抽 2 张牌；否则额外获得 {Block:diff()} 点格挡。");

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Move, play);
        if (!OathflameRuntime.CanAct(Owner)) return;
        // Use the run's saved RNG stream, never wall-clock or presentation randomness.
        if (Owner.RunState.Rng.CombatCardSelection.NextInt(10) < 7)
            await CardPileCmd.Draw(context, 2, Owner);
        else
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Move, play);
    }
    protected override void OnUpgrade() { DynamicVars["Block"].UpgradeValueBy(3); }
}

public sealed class AllOutTogether() : ChaosCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override string ArtName => "cards/AllOutTogether.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<VulnerablePower>(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VulnerablePower>()];
    public override List<(string, string)> Localization => new CardLoc("一口气上吧",
        "对所有敌人施加 {VulnerablePower:diff()} 层易伤。");

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        foreach (var enemy in Owner.Creature.CombatState!.HittableEnemies.ToArray())
        {
            if (!OathflameRuntime.CanAct(Owner)) return;
            if (enemy.IsAlive)
                await PowerCmd.Apply<VulnerablePower>(context, enemy, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        }
    }
    protected override void OnUpgrade() => DynamicVars.Vulnerable.UpgradeValueBy(1);
}
