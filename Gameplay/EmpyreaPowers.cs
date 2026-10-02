using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ChaosPrototype.Gameplay;

public sealed class PrayerPower : ResonancePower
{
    public SignalColor FirstColor { get; private set; }
    public override List<(string, string)> Localization => new PowerLoc("颂歌祷告", "记录两次手动消球主牌颜色：同色全体 6 点能力伤害、8 格挡；异色全体 12 点能力伤害、3 点可格挡自伤，完整格挡获得超算。跨回合保留，不叠加、不重置记录。", "记录两次手动消球主牌颜色：同色全体 6 点能力伤害、8 格挡；异色全体 12 点能力伤害、3 点可格挡自伤，完整格挡获得超算。跨回合保留，不叠加、不重置记录。");
    internal async Task AfterSignal(PlayerChoiceContext context, SignalColor color, CardPlay play)
    {
        if (!OathflameRuntime.CanAct(Owner.Player!) || color == SignalColor.None) return;
        if (FirstColor == SignalColor.None) { FirstColor = color; return; }
        bool same = FirstColor == color;
        // Finish this prayer before commands can trigger any nested effects.
        await PowerCmd.Remove(this);
        await CreatureCmd.Damage(context, CombatState.HittableEnemies.ToArray(), same ? 6 : 12, ValueProp.Unpowered, Owner);
        if (!OathflameRuntime.CanAct(Owner.Player!)) return;
        if (same) await CreatureCmd.GainBlock(Owner, 8, ValueProp.Move, play);
        else
        {
            var results = (await CreatureCmd.Damage(context, Owner, 3, ValueProp.Unpowered, Owner)).ToArray();
            if (results.Sum(r => r.BlockedDamage) > 0 && results.All(r => r.UnblockedDamage == 0))
                await ResonanceRuntime.GainSupercompute(context, Owner.Player!);
        }
    }
}

public sealed class GarlandsSeaPower : ResonancePower
{
    public int Inputs { get; private set; }
    public int Accumulated { get; private set; }
    private bool _released;
    public override List<(string, string)> Localization => new PowerLoc("伽蓝之海", "本回合后续最多三次手动消球蓄积伤害：普通 4、三消 8。回合结束全体受到 12＋蓄积值的能力伤害。本回合之后打出的球主牌消耗，辅牌仍弃置。", "本回合后续最多三次手动消球蓄积伤害：普通 4、三消 8。回合结束全体受到 12＋蓄积值的能力伤害。本回合之后打出的球主牌消耗，辅牌仍弃置。");
    internal void Record(int strength)
    {
        if (_released || Inputs >= 3) return;
        Inputs++;
        Accumulated += strength == 3 ? 8 : 4;
    }
    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position) =>
        card is SignalCard && card.Owner == Owner.Player ? (PileType.Exhaust, position) : (pileType, position);
    public override async Task BeforeSideTurnEndVeryEarly(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || _released) return;
        _released = true;
        // Resolve before end-turn damage buffs expire. Retain the exhaustion effect during any nested plays.
        if (OathflameRuntime.CanAct(Owner.Player!))
            await CreatureCmd.Damage(context, CombatState.HittableEnemies.ToArray(), 12 + Accumulated, ValueProp.Unpowered, Owner);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
