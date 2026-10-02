using BaseLib.Abstracts;
using ChaosPrototype.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ChaosPrototype.Gameplay;

internal static class SignalRuntime
{
    private sealed record Snapshot(CardModel[] Hand, CardModel[] Pair)
    {
        public PlayResolution Resolution { get; } = new();
        public bool Counted { get; set; }
        public bool EffectCompleted { get; set; }
    }
    private static readonly Dictionary<CardModel, Snapshot> Snapshots = [];
    internal static readonly HashSet<CardModel> Suppressed = [];
    // Pending reservations prevent extra manual plays while a Chaos card is queued/executing.
    internal static readonly Dictionary<Player, CardModel> Pending = [];
    internal static bool HasCore(Player player) => player.Relics.Any(r => r is SignalCore);
    internal static SignalColor Color(CardModel card) => card is SignalCard signal ? signal.SignalColor : SignalColor.None;
    internal static CardModel[] Preview(CardModel card) => card.Owner?.PlayerCombatState is { } state && HasCore(card.Owner)
        ? SignalRules.Auxiliary(state.Hand.Cards, card, Color) : [];

    internal static void Capture(CardModel card)
    {
        if (card is not SignalCard || card.Pile?.Type != PileType.Hand) return;
        var hand = card.Owner.PlayerCombatState!.Hand.Cards.ToArray();
        Snapshots[card] = new(hand, HasCore(card.Owner) ? SignalRules.Auxiliary(hand, card, Color) : []);
    }
    internal static void Cleanup(CardModel card)
    {
        Snapshots.Remove(card);
        if (Pending.TryGetValue(card.Owner, out var pending) && ReferenceEquals(card, pending)) Pending.Remove(card.Owner);
    }
    internal static void Reset() { Snapshots.Clear(); Suppressed.Clear(); Pending.Clear(); RavenRuntime.Reset(); }

    internal static async Task AfterEffect(PlayerChoiceContext context, SignalCard card, CardPlay play, int strength)
    {
        if (play.IsAutoPlay || !Snapshots.TryGetValue(card, out var snapshot) || snapshot.EffectCompleted) return;
        snapshot.EffectCompleted = true;
        if (strength != 3 || CombatManager.Instance.IsOverOrEnding || !card.Owner.Creature.IsAlive) return;
        if (card.Owner.Creature.GetPower<HyperdimensionalPower>() is { } space)
            await space.AfterTriple(context);
    }

    internal static async Task<int> Resolve(PlayerChoiceContext context, SignalCard card, CardPlay play)
    {
        if (play.IsAutoPlay) return 1;
        if (!Snapshots.TryGetValue(card, out var snapshot)) return 1;
        if (CombatManager.Instance.IsOverOrEnding || card.Owner.Creature.IsDead) return 1;
        if (card.TargetType == TargetType.AnyEnemy && (play.Target == null || !play.Target.IsAlive)) return 1;
        var state = card.Owner.PlayerCombatState!;
        var natural = HasCore(card.Owner) && snapshot.Pair.Length > 0 &&
            SignalRules.UnchangedAfterRemoval(snapshot.Hand, state.Hand.Cards, card);
        var super = CustomResources<Supercompute>.Get(state);
        var resolution = snapshot.Resolution.Resolve(natural ? snapshot.Pair.Length + 1 : 1, super.Amount > 0);
        bool firstResolution = !snapshot.Counted;
        if (firstResolution)
        {
            DebugSession.Record(card.Owner, natural ? snapshot.Pair.Length + 1 : 1, resolution.ConsumeSupercompute);
            snapshot.Counted = true;
        }
        // Consume the existing charge before discard hooks can create another one.
        if (resolution.ConsumeSupercompute) super.Amount = 0;
        if (resolution.ConsumePair)
        {
            foreach (var auxiliary in snapshot.Pair) Suppressed.Add(auxiliary);
            try { await CardCmd.Discard(context, snapshot.Pair); }
            finally { foreach (var auxiliary in snapshot.Pair) Suppressed.Remove(auxiliary); }
        }
        if (firstResolution && ResonanceRules.IsTriple(resolution.Strength) && !CombatManager.Instance.IsOverOrEnding && !card.Owner.Creature.IsDead)
        {
            int block = RavenRuntime.Get(card.Owner).RecordTriple();
            if (block > 0) await CreatureCmd.GainBlock(card.Owner.Creature, block, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, play);
        }
        return resolution.Strength;
    }
}
