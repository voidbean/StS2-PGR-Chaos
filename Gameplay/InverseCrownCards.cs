using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class Crown() : ChaosCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string ArtName => "cards/Crown.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Cycle", 1)];
    public override List<(string, string)> Localization => new CardLoc("王冠",
        "从下回合起，每个自己的回合正常抽牌后，再抽 {Cycle:diff()} 张牌，然后弃置 {Cycle:diff()} 张手牌。\n不可叠加；升级版替换普通版的效果。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        int count = DynamicVars["Cycle"].IntValue;
        if (Owner.Creature.GetPower<CrownPower>() is { } power)
        {
            if (count > power.Amount)
                await PowerCmd.ModifyAmount(context, power, count - power.Amount, Owner.Creature, this);
        }
        else await PowerCmd.Apply<CrownPower>(context, Owner.Creature, count, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars["Cycle"].UpgradeValueBy(1);
}

public sealed class Atheism() : ChaosCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override string ArtName => "cards/Atheism.png";
    public override List<(string, string)> Localization => new CardLoc("无神论",
        "失去 4 点生命。\n从下回合起，每个自己的回合正常抽牌后，再抽 1 张牌，然后消耗 1 张手牌。\n不可叠加；重复打出仍失去生命。");
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner)) return;
        await CreatureCmd.Damage(context, Owner.Creature, 4, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);
        if (OathflameRuntime.CanAct(Owner) && Owner.Creature.GetPower<AtheismPower>() == null)
            await PowerCmd.Apply<AtheismPower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

public sealed class LightlessAbyss() : BurstCard(4, CardType.Attack, TargetType.AnyEnemy)
{
    private bool? _severance;
    public override int MaxUpgradeLevel => 0;
    protected override string ArtName => "cards/LightlessAbyss.png";
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(36, ValueProp.Move), new DamageVar("SeveranceDamage", 6, ValueProp.Move)];
    public override List<(string, string)> Localization => new CardLoc("诸光尽默之渊",
        "逆冕 · 大招\n保留。消耗 4 充能。选择一种模式：\n万劫：造成 {Damage:diff()} 点伤害。\n断念：造成 {SeveranceDamage:diff()} 点伤害，共 5 次。\n不可自动打出。重放沿用所选模式。");
    protected override async Task BurstEffect(PlayerChoiceContext context, CardPlay play)
    {
        if (play.PlayIndex == 0)
        {
            _severance = null;
            var chosen = await CardSelectCmd.FromChooseACardScreen(context,
                [ModelDb.Card<MyriadCalamitiesChoice>(), ModelDb.Card<SeveranceChoice>()], Owner);
            _severance = chosen switch { MyriadCalamitiesChoice => false, SeveranceChoice => true, _ => null };
        }
        if (_severance == null || !OathflameRuntime.CanAct(Owner) || play.Target is not { IsAlive: true }) return;
        await DamageCmd.Attack(DynamicVars[_severance.Value ? "SeveranceDamage" : "Damage"].BaseValue)
            .FromCard(this).Targeting(play.Target).WithHitCount(_severance.Value ? 5 : 1).Execute(context);
    }
}

// Screen-only choices: no reward pool, compendium entry, debug card or generated combat card.
public abstract class AbyssModeChoice() : CustomCardModel(0, CardType.Attack, CardRarity.Token, TargetType.None,
    showInCardLibrary: false, autoAdd: false)
{
    public override int MaxUpgradeLevel => 0;
    public override CardPoolModel Pool => ModelDb.CardPool<ChaosCardPool>();
    protected override bool IsPlayable => false;
    public override string CustomPortraitPath => $"res://ChaosPrototype/cards/{GetType().Name}.png";
    public override string PortraitPath => CustomPortraitPath;
    public override string BetaPortraitPath => CustomPortraitPath;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}

public sealed class MyriadCalamitiesChoice : AbyssModeChoice
{
    public override List<(string, string)> Localization => new CardLoc("万劫", "诸光尽默之渊 · 模式\n对所选敌人造成 36 点基础伤害。\n单次重击，伤害由大招结算。");
}

public sealed class SeveranceChoice : AbyssModeChoice
{
    public override List<(string, string)> Localization => new CardLoc("断念", "诸光尽默之渊 · 模式\n对所选敌人造成 6 点基础伤害，共 5 次。\n每次攻击分别接受伤害修正。");
}
