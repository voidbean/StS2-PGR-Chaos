using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ChaosPrototype.Gameplay;

public sealed class WindsGaze() : ChaosCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string ArtName => "cards/WindsGaze.png";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [new HoverTip(new LocString("cards", Id.Entry + ".flavor"))];
    public override List<(string, string)> Localization => new CardLoc("风的视线",
        "虚无。消耗。\n获得超算。本回合下一张手动打出的信号球能量费用为 0。免费次数不叠加。\n超算不可叠层，跨回合保留至使用。",
        ("flavor", "直到无人聆听。"));

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive || Owner.PlayerCombatState == null) return;
        await ResonanceRuntime.GainSupercompute(context, Owner);
        if (Owner.Creature.GetPower<WindsGazePower>() == null)
            await PowerCmd.Apply<WindsGazePower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() { EnergyCost.UpgradeBy(-1); }
}

public sealed class WindsGazePower : ResonancePower
{
    public override List<(string, string)> Localization => new PowerLoc("风的视线",
        "本回合下一张手动打出的信号球能量费用为 0。免费次数不叠加；回合结束失效。",
        "本回合下一张手动打出的信号球能量费用为 0。免费次数不叠加；回合结束失效。");

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        // Change only energy: burst charge costs and other resources are untouched.
        bool applies = card is SignalCard && card.Owner == Owner.Player;
        modifiedCost = applies ? 0 : originalCost;
        return applies;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // Resources have already been paid. Consume before resolving the signal so
        // nested plays cannot reuse the discount; auxiliary discards never reach here.
        if (!cardPlay.IsAutoPlay && cardPlay.PlayIndex == 0 &&
            cardPlay.Card is SignalCard && cardPlay.Card.Owner == Owner.Player)
            await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
