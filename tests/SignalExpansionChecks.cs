using BaseLib.Abstracts;
using ChaosPrototype.Core;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static partial class HyperrealChecks
{
    private static Creature[] ExpansionTargets = [];
    private static bool ExpansionEnemies(ref IReadOnlyList<Creature> __result) { __result = ExpansionTargets.Where(t => t.IsAlive).ToArray(); return false; }
    private static bool SegmentDamage(IEnumerable<Creature> targets, ValueProp props, ref Task<IEnumerable<DamageResult>> __result)
    {
        __result = Task.FromResult<IEnumerable<DamageResult>>(targets.Where(t => t.IsAlive).Select(t => new DamageResult(t, props) { BlockedDamage = 1 }).ToArray());
        return false;
    }
    private static bool ExpansionAttack(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        var damage = (decimal)AccessTools.Field(typeof(AttackCommand), "_damagePerHit").GetValue(__instance)!;
        var hits = (int)AccessTools.Field(typeof(AttackCommand), "_hitCount").GetValue(__instance)!;
        var target = (Creature?)AccessTools.Field(typeof(AttackCommand), "_singleTarget").GetValue(__instance);
        Attacks.Add((damage, hits, target, __instance.DamageProps));
        __result = RunSegments(); return false;
        async Task<AttackCommand> RunSegments()
        {
            for (int i = 0; i < hits; i++)
                await CreatureCmd.Damage(null!, target == null ? ExpansionTargets : [target], damage, __instance.DamageProps, __instance.Attacker, (CardModel?)__instance.ModelSource);
            return __instance;
        }
    }
    private static async Task RunExpansion(Action<bool, string> check, Player player, CombatState combat, Creature target, List<PowerModel> powers, Harmony harmony)
    {
        var state = player.PlayerCombatState!;
        var other = new Creature(null!, 100, 100) { CombatState = combat };
        target.CombatState = combat;
        ExpansionTargets = [target, other];
        var models = typeof(ExpandedSignal).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ExpandedSignal).IsAssignableFrom(t)).Select(t => (ExpandedSignal)Activator.CreateInstance(t)!).ToArray();
        var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
        foreach (var model in models.Cast<AbstractModel>().Concat([new SignalSupportPower(), new VulnerablePower()])) table[ModelDb.GetId(model.GetType())] = model;
        check(models.Length == 16 && models.Count(c => c.Rarity == CardRarity.Common) == 5 && models.Count(c => c.Rarity == CardRarity.Uncommon) == 11, "sixteen expansion signals with five common and eleven uncommon");
        check(models.All(c => c.MaxUpgradeLevel == 1 && c.EnergyCost.Canonical == 1 && !c.CanonicalKeywords.Any()), "expansion supports one upgrade with unchanged base cost and keywords");
        var all = typeof(ChaosCard).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ChaosCard).IsAssignableFrom(t)).Select(t => (ChaosCard)(table.TryGetValue(ModelDb.GetId(t), out var known) ? known : (AbstractModel)Activator.CreateInstance(t)!)).ToArray();
        check(all.Length == 59 && all.Count(c => c.Rarity == CardRarity.Token) == 3 && all.Count(c => c.Rarity == CardRarity.Basic) == 5, "fifty-six permanent cards plus three tokens; starter unchanged");
        Console.WriteLine("Reward rarity counts: " + string.Join(", ", all.Where(c => c.Rarity != CardRarity.Token && c.Rarity != CardRarity.Basic).GroupBy(c => c.Rarity).Select(g => $"{g.Key}={g.Count()}")));
        harmony.Unpatch(AccessTools.Method(typeof(AttackCommand), "Execute"), AccessTools.Method(typeof(HyperrealChecks), nameof(CaptureAttack)));
        harmony.Patch(AccessTools.Method(typeof(AttackCommand), "Execute"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(ExpansionAttack)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(CombatState), "HittableEnemies"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(ExpansionEnemies)));
        harmony.Patch(AccessTools.Method(typeof(CreatureCmd), "Damage", [typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(SegmentDamage)));
        harmony.CreateClassProcessor(typeof(SignalCard).Assembly.GetType("ChaosPrototype.Gameplay.SignalSegmentPatch")!).Patch();
        var runtime = typeof(SignalCard).Assembly.GetType("ChaosPrototype.Gameplay.SignalRuntime")!;
        void Reset()
        {
            powers.Clear(); Attacks.Clear(); Collapses.Clear(); Generated.Clear(); Blocks.Clear();
            foreach (var enemy in ExpansionTargets) ((List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(enemy)!).Clear();
            foreach (var pile in state.AllPiles) foreach (var card in pile.Cards.ToArray()) pile.RemoveInternal(card, true);
            CustomResources<Supercompute>.Get(state).Amount = 0;
            AccessTools.Method(runtime, "Reset").Invoke(null, null);
        }
        T Card<T>() where T : CardModel { var c = combat.CreateCard<T>(player); state.PlayPile.AddInternal(c, silent: true); return c; }
        CardPlay Replay(CardModel card, int index, int count, bool auto = false) => new() { Card = card, Target = target, PlayIndex = index, PlayCount = count, IsAutoPlay = auto, ResultPile = PileType.Discard, Resources = default };
        async Task Effect(SignalCard card, int strength, int index = 0, int count = 1, bool auto = false)
        {
            var play = Replay(card, index, count, auto);
            if (player.Creature.GetPower<SignalSupportPower>() is { } before) await before.BeforeCardPlayed(play);
            await Invoke(card, "Effect", null, play, strength);
            if (player.Creature.GetPower<SignalSupportPower>() is { } after) await after.AfterCardPlayed(null!, play);
        }
        SignalSupportPower Support() => player.Creature.GetPower<SignalSupportPower>()!;
        void Call(string name, params object[] args) => AccessTools.Method(typeof(SignalSupportPower), name).Invoke(Support(), args);
        // Card commands at normal, ordinary double and triple strength.
        foreach (int strength in new[] { 1, 2, 3 })
        {
            Reset(); await Effect(Card<ShatteringImpact>(), strength);
            check(Attacks.Single() == (strength == 3 ? 7m : 4m, 2, target, ValueProp.Move), "shattering impact two powered hits");
            Reset(); await Effect(Card<GlacialCirculation>(), strength);
            check(Attacks.Select(a => (a.Damage, a.Hits)).SequenceEqual(strength == 3 ? new[] { (3m, 2), (4m, 1) } : [(2m, 2), (2m, 1)]), "glacial circulation three AoE segments without draw");
            Reset(); await Effect(Card<EclipticGrace>(), strength);
            check(Attacks.Single().Damage == (strength == 3 ? 9 : 5) && Attacks[0].Target == null, "ecliptic grace AoE");
            Reset(); await Effect(Card<PrecisionVolley>(), strength);
            check(Attacks.Single().Damage == (strength == 3 ? 3 : 2) && Attacks[0].Hits == (strength == 3 ? 4 : 3), "precision volley segments");
            Reset(); await Effect(Card<ExemptionSpace>(), strength);
            check(Attacks.Single().Damage == (strength == 3 ? 6 : 3) && ExpansionTargets.All(t => t.GetPower<WeakPower>()?.Amount == (strength == 3 ? 2 : 1)) && CustomResources<Supercompute>.Get(state).Amount == 0, "exemption space one cost AoE weakness and no supercompute");
            Reset(); await Effect(Card<FictionalBarrier>(), strength);
            check(Blocks.Single() == (strength == 3 ? 9 : 6) && target.GetPower<WeakPower>()?.Amount == (strength == 3 ? 2 : 1), "fictional barrier block and targeted weakness");
            Reset(); await Effect(Card<ShadowManeuver>(), strength);
            check(Blocks.Single() == (strength == 3 ? 8 : 5) && Attacks.Count == (strength == 3 ? 2 : 1) && Attacks[0].Damage == 3 && (strength != 3 || Attacks[1].Damage == 4), "shadow maneuver shoot block then kick");
            Reset(); await Effect(Card<ElectricInduction>(), strength);
            check(target.GetPower<VulnerablePower>()?.Amount == 1 && Attacks.Single().Damage == (strength == 3 ? 7 : 4), "induction attack then vulnerable");
            await Support().BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]);
            check(Collapses.Single() == (strength == 3 ? 5m : 3m, ValueProp.Unpowered), "induction delayed unpowered damage");
        }
        // Resolve through the actual signal entry point: own triple cannot qualify, replay cannot change that.
        foreach (var type in new[] { typeof(OpeningStance), typeof(ContinuousFire) })
        {
            Reset(); var card = (SignalCard)((CardModel)table[ModelDb.GetId(type)]).ToMutable(); card.Owner = player;
            if (card.Pile != null) card.Pile.RemoveInternal(card, true); state.Hand.AddInternal(card, silent: true); AccessTools.Method(runtime, "Capture").Invoke(null, [card]); state.Hand.RemoveInternal(card, true); state.PlayPile.AddInternal(card, silent: true);
            CustomResources<Supercompute>.Get(state).Amount = 1;
            await Invoke(card, "OnPlay", null, Replay(card, 0, 2)); await Invoke(card, "OnPlay", null, Replay(card, 1, 2));
            check(Attacks.Count == 2 && CustomResources<Supercompute>.Get(state).Amount == 0, "own supercompute triple and replay cannot qualify own follow-up");
            AccessTools.Method(runtime, "Cleanup").Invoke(null, [card]);
            if (card.Pile != null) card.Pile.RemoveInternal(card, true); state.Hand.AddInternal(card, silent: true); AccessTools.Method(runtime, "Capture").Invoke(null, [card]); state.Hand.RemoveInternal(card, true); state.PlayPile.AddInternal(card, silent: true);
            Attacks.Clear(); await Invoke(card, "OnPlay", null, Replay(card, 0, 1));
            check(Attacks.Count == 2 && Attacks[1].Damage == 2 && Attacks[1].Hits == 2, "earlier triple enables two follow-up hits");
        }
        Reset(); var wave = Card<WaveSlash>(); await Effect(wave, 3);
        check(Support().Flames == 3 && Collapses.Count == 0, "wave does not benefit from its new preparation");
        var volley = Card<PrecisionVolley>(); await Effect(volley, 3, 0, 2); await Effect(volley, 3, 1, 2);
        check(Support().Flames == 2 && Collapses.Count == 1 && Collapses[0].Damage == 2, "four hits and replay consume one flame card and append once");
        await Effect(Card<EclipticGrace>(), 1, auto: true);
        check(Support().Flames == 1 && Collapses.Count == 3, "automatic AoE consumes one allowance and appends per hit enemy");
        await Effect(wave, 3, 1, 2); check(Support().Flames == 1, "wave replay cannot refresh preparation");
        Reset(); await Effect(Card<TransferCharge>(), 3); await Effect(Card<GrayRavenField>(), 1);
        // Field applies two marks after its base attack; appended shot consumes one on each enemy.
        check(Attacks.Count == 3 && Collapses.Count == 2 && (int)AccessTools.Method(typeof(SignalSupportPower), "MarksOn").Invoke(Support(), [target])! == 1, "field marks follow base attack; transfer shots consume new marks");
        await Effect(Card<PrecisionVolley>(), 1);
        check(Support().Shots == 1 && Collapses.Count == 3, "multi-hit volley consumes only remaining target mark and one shot card allowance");
        Call("PrepareShots", 1); check(Support().Shots == 1, "weaker reapplication does not add allowances");
        Call("PrepareShots", 3); check(Support().Shots == 3, "stronger reapplication refreshes to maximum");
        Reset(); var heat = Card<ThermalShot>(); await Effect(heat, 3);
        check(Support().Explosions == 4, "heat grants four prepared segments");
        await Effect(Card<PrecisionVolley>(), 1, auto: true); check(Support().Explosions == 4, "automatic red cannot consume explosion preparation");
        await Effect(Card<TransferCharge>(), 1); // Yellow skill cannot consume preparation either.
        var red = Card<PrecisionVolley>(); Collapses.Clear();
        await Effect(red, 1, 0, 2); await Effect(red, 1, 1, 2);
        check(Collapses.Count == 4 && Collapses.All(c => c == (1m, ValueProp.Unpowered)), "explosion cap shared across multi-hit, replay and appended attack without recursion");
        await Effect(heat, 3, 1, 2); check(Support().Explosions == 0, "heat replay cannot rearm explosions");
        Collapses.Clear(); await Support().BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]);
        check(Collapses.Select(c => c.Damage).SequenceEqual(new[] { 7m, 7m }), "heat replay retains independent delayed damage");
        await Support().BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]); check(Collapses.Count == 2, "delayed queue drains once");
        Reset(); await Effect(Card<ThermalShot>(), 1); Collapses.Clear();
        await Effect(Card<EclipticGrace>(), 1);
        check(Collapses.Count == 1, "one AoE segment triggers one explosion, not once per target");
        await Effect(Card<PrecisionVolley>(), 1); check(Collapses.Count == 1, "unused explosion allowance discarded after first red card");
        Reset(); await Effect(Card<GrayRavenField>(), 3); check(Collapses.Count == 0, "fresh field cannot trigger itself");
        await Effect(Card<PrecisionVolley>(), 1); check(Collapses.Count == 3, "fully blocked powered segments still consume marks");
        await CreatureCmd.Damage(null!, new[] { other }, 2, ValueProp.Unpowered, player.Creature, Card<RedSignal>());
        check((int)AccessTools.Method(typeof(SignalSupportPower), "MarksOn").Invoke(Support(), [other])! == 3, "ability damage never consumes domain marks");
        var instance = (SignalSupportPower)ModelDb.Power<SignalSupportPower>().ToMutable();
        check((int)AccessTools.Method(typeof(SignalSupportPower), "MarksOn").Invoke(instance, [other])! == 0, "mutable support collections never leak across instances");
        Reset(); var fall = Card<FallingFire>();
        await Effect(fall, 3, auto: true); check(Generated.Count == 0, "automatic falling fire cannot generate traversal");
        for (int i = 0; i < CardPile.MaxCardsInHand; i++) state.Hand.AddInternal(Card<RedSignal>(), silent: true);
        await Effect(fall, 3); await Effect(Card<FallingFire>(), 3); await Effect(fall, 3, 1, 2);
        check(Generated.Count == 1 && Generated[0] is RealmTraversal && state.DiscardPile.Cards.Contains(Generated[0]), "falling fire shared turn cap, no replay duplication, full hand to discard");
        await Support().AfterSideTurnEnd(null!, CombatSide.Player, [player.Creature]);
        await Effect(fall, 3); check(Generated.Count == 2, "new turn support permits traversal again");
        Reset(); var radiant = Card<RadiantDaybreak>(); await Effect(radiant, 1); check(Attacks.Single().Damage == 7 && Generated.Count == 0, "radiant normal does not enter flight or generate continuation");
        var flight = (OathflameFlightPower)ModelDb.Power<OathflameFlightPower>().ToMutable(); AccessTools.Property(typeof(PowerModel), "Owner").SetValue(flight, player.Creature); powers.Add(flight);
        await Effect(radiant, 3); check(Attacks.Last().Damage == 13 && Generated.Single() is SoaringContinuation && powers.Contains(flight), "radiant flight bonus preserves flight and generates existing continuation");
        Reset(); await Effect(Card<ThermalShot>(), 1);
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(target, 0); Collapses.Clear();
        await Support().BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]); check(Collapses.Count == 0, "dead delayed target fizzles");
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(target, 100);
        var endingSupport = Support(); await endingSupport.AfterSideTurnEnd(null!, CombatSide.Player, [player.Creature]);
        check(player.Creature.GetPower<SignalSupportPower>() == null && endingSupport.Explosions == 0 && endingSupport.Shots == 0 && endingSupport.Flames == 0, "turn end clears all temporary support");
        Reset(); ExpansionTargets = [];
    }
}
