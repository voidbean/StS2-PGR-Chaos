using System.Reflection;
using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using ChaosPrototype.Gameplay;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

// Exercise real card effects and lifecycle hooks, replacing only engine command execution.
// These checks do not assert Godot animations or the native damage formula.
static partial class HyperrealChecks
{
    private static readonly List<(decimal Damage, int Hits, Creature? Target, ValueProp Props)> Attacks = [];
    private static readonly List<(decimal Damage, ValueProp Props)> Collapses = [];
    private static readonly List<CardModel> Generated = [];
    private static readonly List<(PileType Pile, CardPilePosition Position)> Destinations = [];
    private static bool ChooseYellow(IReadOnlyList<CardModel> cards, ref Task<CardModel?> __result)
    {
        if (cards.Count != 2 || cards[0] is not RedSignal || cards[1] is not YellowSignal) throw new Exception("causal choice must be red or yellow");
        __result = Task.FromResult<CardModel?>(cards[1]);
        return false;
    }
    private static bool CombatActive(ref bool __result) { __result = false; return false; }
    private static bool CaptureAttack(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        Attacks.Add(((decimal)AccessTools.Field(typeof(AttackCommand), "_damagePerHit").GetValue(__instance)!,
            (int)AccessTools.Field(typeof(AttackCommand), "_hitCount").GetValue(__instance)!,
            (Creature?)AccessTools.Field(typeof(AttackCommand), "_singleTarget").GetValue(__instance), __instance.DamageProps));
        __result = Task.FromResult(__instance);
        return false;
    }
    private static bool CaptureDamage(decimal amount, ValueProp props, ref Task<IEnumerable<DamageResult>> __result)
    {
        Collapses.Add((amount, props));
        __result = Task.FromResult<IEnumerable<DamageResult>>([]);
        return false;
    }
    private static bool CaptureGenerated(IEnumerable<CardModel> cards, PileType newPileType, CardPilePosition position, ref Task<IReadOnlyList<CardPileAddResult>> __result)
    {
        foreach (var card in cards)
        {
            Generated.Add(card);
            Destinations.Add((newPileType, position));
            var state = card.Owner.PlayerCombatState!;
            var pile = newPileType == PileType.Draw ? state.DrawPile : newPileType == PileType.Hand && state.Hand.Cards.Count < CardPile.MaxCardsInHand ? state.Hand : state.DiscardPile;
            pile.AddInternal(card, silent: true);
        }
        __result = Task.FromResult<IReadOnlyList<CardPileAddResult>>([]);
        return false;
    }
    private static bool ApplyPower(PowerModel power, Creature target, decimal amount, ref Task __result)
    {
        AccessTools.Property(typeof(PowerModel), "Owner").SetValue(power, target);
        AccessTools.Property(typeof(PowerModel), "Amount").SetValue(power, (int)amount);
        ((List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(target)!).Add(power);
        __result = power.AfterApplied(null, null);
        return false;
    }
    private static bool RemovePower(PowerModel? power, ref Task __result)
    {
        if (power != null)
            ((List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(power.Owner)!).Remove(power);
        __result = Task.CompletedTask;
        return false;
    }
    private static bool RemoveCard(CardModel card, ref Task __result)
    {
        card.Pile!.RemoveInternal(card, true);
        __result = Task.CompletedTask;
        return false;
    }
    private static CardPlay Play(CardModel card, Creature? target = null, bool auto = false) => new()
    {
        Card = card, Target = target, IsAutoPlay = auto, ResultPile = PileType.Discard,
        Resources = default, PlayIndex = 0, PlayCount = 1
    };
    private static Task Invoke(object target, string method, params object?[] args) => (Task)AccessTools.Method(target.GetType(), method).Invoke(target, args)!;
    private static void Field(object target, string name, object value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);

    internal static async Task Run(Action<bool, string> check)
    {
        ChaosCard[] cards = [new SwiftAssault(), new HyperdimensionalSpace(), new CausalConvergence(), new TemporalFinality(), new RealmTraversal()];
        check(cards.All(c => c.MaxUpgradeLevel == 0), "hyperreal has no invented upgrades");
        check(cards.Select(c => c.Rarity).SequenceEqual(new[] { CardRarity.Common, CardRarity.Rare, CardRarity.Uncommon, CardRarity.Rare, CardRarity.Token }), "four hyperreal rewards and a token");
        check(cards.Select(c => c.EnergyCost.Canonical).SequenceEqual(new[] { 1, 2, 1, 0, 0 }), "hyperreal energy costs");
        check(CustomResources<Charge>.CanonicalCost(cards[3]) == 4 && cards[3].CanonicalKeywords.Contains(CardKeyword.Retain), "finality retains and costs four charge");
        check(cards[4].CanonicalKeywords.ToHashSet().SetEquals([CardKeyword.Retain, CardKeyword.Exhaust]), "traversal retains and exhausts without ethereal cleanup");
        check(cards[1].Localization!.Any(l => l == ("flavor", "你们的时间，由我掌控！")) && cards[3].Localization!.Any(l => l == ("flavor", "在时间的尽头……湮灭吧！")), "selected flavor preserved separately from rules");
        var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
        foreach (var model in cards.Cast<AbstractModel>().Concat([new HyperdimensionalPower(), new TemporarySignalsPower(), new SupercomputePower()])) table[ModelDb.GetId(model.GetType())] = model;
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var creature = new Creature(player, 70, 70);
        Field(player, "<Creature>k__BackingField", creature);
        Field(player, "<Character>k__BackingField", ModelDb.Character<ChaosCharacter>());
        Field(player, "_runState", NullRunState.Instance);
        Field(player, "_runPiles", Array.Empty<CardPile>());
        Field(player, "_relics", new List<RelicModel>());
        var state = new PlayerCombatState(player);
        Field(player, "<PlayerCombatState>k__BackingField", state);
        var resourceField = AccessTools.Property(typeof(CustomResources<Supercompute>), "Resource").GetValue(null)!;
        AccessTools.Method(resourceField.GetType(), "Set").Invoke(resourceField, [state, new Supercompute { Owner = player }]);
        var combat = new CombatState();
        creature.CombatState = combat;
        var target = new Creature(null!, 100, 100);
        var space = (HyperdimensionalPower)ModelDb.Power<HyperdimensionalPower>().ToMutable();
        var cleanup = (TemporarySignalsPower)ModelDb.Power<TemporarySignalsPower>().ToMutable();
        var lightning = (LightningPower)ModelDb.Power<LightningPower>().ToMutable();
        var powers = (List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(creature)!;
        foreach (var power in new PowerModel[] { space, cleanup, lightning })
        {
            AccessTools.Property(typeof(PowerModel), "Owner").SetValue(power, creature);
            powers.Add(power);
        }
        var harmony = new Harmony("ChaosPrototype.Tests.Hyperreal");
        void Patch(MethodBase original, string prefix) => harmony.Patch(original, prefix: new HarmonyMethod(typeof(HyperrealChecks), prefix));
        var progress = AccessTools.Property(typeof(CombatManager), "IsInProgress");
        var wasInProgress = CombatManager.Instance.IsInProgress;
        try
        {
            progress.SetValue(CombatManager.Instance, true);
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CombatActive));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsEnding"), nameof(CombatActive));
            Patch(AccessTools.Method(typeof(AttackCommand), "Execute"), nameof(CaptureAttack));
            Patch(AccessTools.Method(typeof(CreatureCmd), "Damage", [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature)]), nameof(CaptureDamage));
            Patch(AccessTools.Method(typeof(CardPileCmd), "AddGeneratedCardsToCombat"), nameof(CaptureGenerated));
            Patch(AccessTools.Method(typeof(CardSelectCmd), "FromChooseACardScreen"), nameof(ChooseYellow));
            Patch(AccessTools.Method(typeof(CardPileCmd), "RemoveFromCombat", [typeof(CardModel), typeof(bool)]), nameof(RemoveCard));
            Patch(AccessTools.Method(typeof(PowerCmd), "Apply", [typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)]), nameof(ApplyPower));
            Patch(AccessTools.Method(typeof(PowerCmd), "Remove", [typeof(PowerModel)]), nameof(RemovePower));
            var assault = combat.CreateCard<SwiftAssault>(player);
            foreach (int strength in new[] { 1, 2, 3 })
            {
                Attacks.Clear();
                await Invoke(assault, "Effect", null, Play(assault, target), strength);
                check(Attacks.Count == (strength == 3 ? 2 : 1) && Attacks[0] == (7m, 1, target, ValueProp.Move), "assault normal and double begin with seven damage");
                if (strength == 3) check(Attacks[1] == (3m, 3, target, ValueProp.Move), "assault triple follows with three independently powered hits to the same target");
            }
            var runtime = typeof(SignalCard).Assembly.GetType("ChaosPrototype.Gameplay.SignalRuntime")!;
            state.Hand.AddInternal(assault, silent: true);
            async Task Complete(int strength, bool auto = false) => await (Task)AccessTools.Method(runtime, "AfterEffect").Invoke(null, [null, assault, Play(assault, auto: auto), strength])!;
            void Capture() => AccessTools.Method(runtime, "Capture").Invoke(null, [assault]);
            Capture(); await Complete(3, true);
            check(space.Viewpoints == 0, "autoplay cannot earn viewpoints");
            await Complete(3); await Complete(3);
            check(space.Viewpoints == 1, "replay cannot earn a second viewpoint");
            Capture(); await Complete(2);
            check(space.Viewpoints == 1, "ordinary double cannot earn viewpoints");
            await space.BeforeSideTurnStart(null!, CombatSide.Player, [creature], combat);
            check(space.Viewpoints == 1, "viewpoints survive turn change");
            Capture(); await Complete(3); Capture(); await Complete(3);
            check(space.Viewpoints == 0 && Collapses.SequenceEqual(new[] { (10m, ValueProp.Unpowered) }), "third triple spends exactly three and causes ability damage");
            Field(space, "_viewpoints", 4); Capture(); await Complete(3);
            check(space.Viewpoints == 2 && Collapses.Count == 2, "collapse retains overflow");
            check(((HyperdimensionalPower)ModelDb.Power<HyperdimensionalPower>().ToMutable()).Viewpoints == 0, "new combat power starts with zero viewpoints");
            var traversal = combat.CreateCard<RealmTraversal>(player);
            await Invoke(traversal, "OnPlay", null, Play(traversal));
            check(CustomResources<Supercompute>.Get(state).Amount == 1 && lightning.Active, "traversal uses shared supercompute and lightning trigger");
            var buff = creature.GetPower<SupercomputePower>();
            check(buff is { IsVisible: true, Type: MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff }, "supercompute gain exposes a visible native buff");
            await Invoke(traversal, "OnPlay", null, Play(traversal));
            check(powers.OfType<SupercomputePower>().Count() == 1 && buff!.Amount == 1, "repeated supercompute does not stack buff");
            await buff!.AfterSideTurnEnd(null!, CombatSide.Player, [creature]);
            await buff.BeforeSideTurnStart(null!, CombatSide.Player, [creature], combat);
            check(creature.GetPower<SupercomputePower>() == buff && CustomResources<Supercompute>.Get(state).Amount == 1, "unused supercompute buff survives turns");
            Capture();
            async Task<int> Resolve(bool auto = false) => await (Task<int>)AccessTools.Method(runtime, "Resolve").Invoke(null, [null, assault, Play(assault, target, auto)])!;
            check(await Resolve(true) == 1 && creature.GetPower<SupercomputePower>() == buff, "autoplay preserves supercompute buff");
            check(await Resolve() == 3 && creature.GetPower<SupercomputePower>() == null && CustomResources<Supercompute>.Get(state).Amount == 0, "manual signal consumes resource and buff together");
            await Invoke(traversal, "OnPlay", null, Play(traversal));
            check(await Resolve() == 3 && creature.GetPower<SupercomputePower>() != null && CustomResources<Supercompute>.Get(state).Amount == 1, "replay preserves newly gained supercompute");
            var resonance = typeof(SignalCard).Assembly.GetType("ChaosPrototype.Gameplay.ResonanceRuntime")!;
            await (Task)AccessTools.Method(resonance, "ClearSupercompute").Invoke(null, [player])!;
            check(creature.GetPower<SupercomputePower>() == null && CustomResources<Supercompute>.Get(state).Amount == 0, "debug clear removes resource and buff");
            var ability = combat.CreateCard<HyperdimensionalSpace>(player);
            state.PlayPile.AddInternal(ability, silent: true);
            await Invoke(ability, "OnPlay", null, Play(ability));
            await Invoke(ability, "OnPlay", null, Play(ability));
            check(powers.OfType<HyperdimensionalPower>().Count() == 1 && space.Viewpoints == 2 && Generated.Count == 2 && Generated.All(c => c is RealmTraversal), "repeated space generates traversal without stacking or resetting viewpoints");
            await cleanup.BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [creature]);
            check(Generated.All(c => c.Pile == state.Hand), "traversal is not subject to temporary ball cleanup");
            var temporary = typeof(SignalCard).Assembly.GetType("ChaosPrototype.Gameplay.TemporarySignals")!;
            var generate = AccessTools.Method(temporary, "Generate");
            Generated.Clear();
            Attacks.Clear();
            var causal = combat.CreateCard<CausalConvergence>(player);
            await Invoke(causal, "OnPlay", null, Play(causal, target));
            check(Attacks.Single() == (6m, 1, target, ValueProp.Move), "causal attacks for six before selected-color supply");
            check(Generated.Count == 2 && Generated.All(c => c is YellowSignal && c.EnergyCost.Canonical == 1 && c.Keywords.Contains(CardKeyword.Exhaust)), "selected-color supply creates two one-cost basic balls");
            check(!powers.OfType<DeadlinePower>().Any(), "common temporary supply does not grant deadline");
            var discarded = Generated[0]; state.Hand.RemoveInternal(discarded, true); state.DiscardPile.AddInternal(discarded, silent: true);
            await cleanup.AfterCardDiscarded(null!, discarded);
            check(discarded.Pile == null, "temporary auxiliary removed after discard without exhausting");
            var drawn = Generated[1]; state.Hand.RemoveInternal(drawn, true); state.DrawPile.AddInternal(drawn, silent: true);
            await cleanup.BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [creature]);
            check(drawn.Pile == null, "turn-end cleanup finds temporary balls in draw pile");
            while (state.Hand.Cards.Count < CardPile.MaxCardsInHand - 1) state.Hand.AddInternal(combat.CreateCard<RedSignal>(player), silent: true);
            Generated.Clear();
            await (Task)generate.Invoke(null, [null, player, 2, (Func<int, ChaosPrototype.Core.SignalColor>)(_ => ChaosPrototype.Core.SignalColor.Red)])!;
            check(Generated.Count == 1 && state.Hand.Cards.Count == CardPile.MaxCardsInHand, "one remaining slot generates one ball and stops");
            await (Task)generate.Invoke(null, [null, player, 2, (Func<int, ChaosPrototype.Core.SignalColor>)(_ => ChaosPrototype.Core.SignalColor.Red)])!;
            check(Generated.Count == 1, "full hand does not generate or defer temporary balls");
            var finality = combat.CreateCard<TemporalFinality>(player);
            state.PlayPile.AddInternal(finality, silent: true);
            Attacks.Clear();
            await Invoke(finality, "OnPlay", null, Play(finality, auto: true));
            check(Attacks.Count == 0, "finality refuses autoplay");
            await Invoke(finality, "OnPlay", null, Play(finality));
            check(Attacks.Single() == (26m, 1, null, ValueProp.Move), "finality emits one powered all-enemy attack");
            await RunOathflame(check, player, combat, target, powers, harmony);
            await RunEmpyrea(check, player, combat, target, powers, harmony);
            await RunExpansion(check, player, combat, target, powers, harmony);
            await RunEveryday(check, player, combat, target, powers, harmony);
            await RunInverseCrown(check, player, combat, target, powers, harmony);
            AccessTools.Method(runtime, "Reset").Invoke(null, null);
        }
        finally { harmony.UnpatchAll(harmony.Id); progress.SetValue(CombatManager.Instance, wasInProgress); }
    }
}
