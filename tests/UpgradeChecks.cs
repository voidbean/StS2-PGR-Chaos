using BaseLib.Abstracts;
using ChaosPrototype.Core;
using ChaosPrototype.Gameplay;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

static partial class HyperrealChecks
{
    private static readonly List<(decimal Damage, bool Upgraded)> UpgradeChoices = [];
    private static bool CaptureUpgradeChoice(IReadOnlyList<CardModel> cards, ref Task<CardModel?> __result)
    {
        UpgradeChoices.AddRange(cards.Select(c => (c.DynamicVars["Damage"].BaseValue, c.IsUpgraded)));
        __result = Task.FromResult<CardModel?>(cards[AbyssChoice]);
        return false;
    }
    private static bool SkipUpgradeMove(ref Task<IEnumerable<CardModel>> __result)
    {
        __result = Task.FromResult<IEnumerable<CardModel>>([]); return false;
    }
    private static bool TakeUpgradeSignal(IReadOnlyList<CardModel> cardsIn, ref Task<IEnumerable<CardModel>> __result)
    {
        __result = Task.FromResult<IEnumerable<CardModel>>(cardsIn.Take(1).ToArray()); return false;
    }
    private static bool AddUpgradeSignal(CardModel card, PileType newPileType, ref Task<CardPileAddResult> __result)
    {
        card.Pile!.RemoveInternal(card, true);
        card.Owner.PlayerCombatState!.Hand.AddInternal(card, silent: true);
        __result = Task.FromResult(default(CardPileAddResult)!); return false;
    }
    private static bool ChooseUpgradeDiscard(Player player, ref Task<IEnumerable<CardModel>> __result)
    {
        __result = Task.FromResult<IEnumerable<CardModel>>(player.PlayerCombatState!.Hand.Cards.Take(1).ToArray()); return false;
    }

    private static async Task RunUpgrades(Action<bool, string> check, Player player, CombatState combat, Creature target, List<PowerModel> powers, Harmony harmony)
    {
        var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
        var cards = typeof(ChaosCard).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ChaosCard).IsAssignableFrom(t))
            .Select(t => (ChaosCard)(table.TryGetValue(ModelDb.GetId(t), out var known) ? known : Activator.CreateInstance(t)!)).ToArray();
        foreach (var card in cards) table[ModelDb.GetId(card.GetType())] = card;
        var permanent = cards.Where(c => c.Rarity != CardRarity.Token).ToArray();
        check(permanent.Length == 56 && cards.Count(c => c.Rarity == CardRarity.Token) == 3, "upgrades preserve permanent and token card counts");
        check(cards.Where(c => c.Rarity == CardRarity.Token).All(c => c.MaxUpgradeLevel == 0), "generated tokens do not gain speculative upgrades");
        foreach (var original in permanent)
        {
            string name = original.GetType().Name;
            foreach (var (_, text) in original.Localization!)
            foreach (System.Text.RegularExpressions.Match token in System.Text.RegularExpressions.Regex.Matches(text, @"\{(\w+):diff\(\)\}"))
                check(original.DynamicVars.ContainsKey(token.Groups[1].Value), $"{name} card text uses a registered dynamic variable: {token.Groups[1].Value}");
            var card = original.ToMutable(); card.Owner = player;
            var before = card.DynamicVars.ToDictionary(v => v.Key, v => v.Value.BaseValue);
            int cost = card.EnergyCost.GetWithModifiers(CostModifiers.All);
            var keywords = card.Keywords.ToHashSet();
            check(card.MaxUpgradeLevel == 1 && !card.IsUpgraded, $"{name} offers one native upgrade");
            card.UpgradeInternal(); card.FinalizeUpgradeInternal();
            check(card.IsUpgraded && (card.EnergyCost.GetWithModifiers(CostModifiers.All) != cost || !keywords.SetEquals(card.Keywords) ||
                card.DynamicVars.Any(v => v.Value.BaseValue != before[v.Key])), $"{name} upgrade changes an actual effect, cost or keyword");
            check(card.Rarity == original.Rarity && card.Type == original.Type && card.TargetType == original.TargetType &&
                ((ChaosCard)card).PreventAutoPlay == original.PreventAutoPlay, $"{name} preserves pool identity and play restrictions");
            var copy = (CardModel)card.ClonePreservingMutability();
            check(copy.IsUpgraded && copy.DynamicVars.All(v => v.Value.BaseValue == card.DynamicVars[v.Key].BaseValue) &&
                copy.EnergyCost.GetWithModifiers(CostModifiers.All) == card.EnergyCost.GetWithModifiers(CostModifiers.All), $"{name} copied upgrade retains its values");
            var restored = CardModel.FromSerializable(card.ToSerializable()); restored.Owner = player;
            check(restored.IsUpgraded && restored.DynamicVars.All(v => v.Value.BaseValue == card.DynamicVars[v.Key].BaseValue) &&
                restored.EnergyCost.GetWithModifiers(CostModifiers.All) == card.EnergyCost.GetWithModifiers(CostModifiers.All) &&
                restored.Keywords.ToHashSet().SetEquals(card.Keywords), $"{name} native card serialization restores upgraded values and keywords");
            card.DowngradeInternal();
            check(!card.IsUpgraded && card.DynamicVars.All(v => v.Value.BaseValue == before[v.Key]) &&
                card.EnergyCost.GetWithModifiers(CostModifiers.All) == cost && keywords.SetEquals(card.Keywords), $"{name} downgrade restores base values without mutating its canonical model");
        }

        var state = player.PlayerCombatState!;
        ExpansionTargets = [target, new Creature(null!, 100, 100) { CombatState = combat }];
        var runtime = typeof(SignalCard).Assembly.GetType("ChaosPrototype.Gameplay.SignalRuntime")!;
        void Reset()
        {
            powers.Clear(); Attacks.Clear(); Blocks.Clear(); Draws.Clear(); Generated.Clear(); Collapses.Clear(); EnergyGains.Clear(); UpgradeChoices.Clear();
            foreach (var enemy in ExpansionTargets) ((List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(enemy)!).Clear();
            foreach (var pile in state.AllPiles) foreach (var card in pile.Cards.ToArray()) pile.RemoveInternal(card, true);
            AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 70);
            AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(target, 100);
            CustomResources<Charge>.Get(state).Amount = 0;
            CustomResources<Supercompute>.Get(state).Amount = 0;
            AccessTools.Method(runtime, "Reset").Invoke(null, null);
        }
        CardModel Plus(Type type)
        {
            var card = ((CardModel)table[ModelDb.GetId(type)]).ToMutable(); card.Owner = player;
            card.UpgradeInternal(); card.FinalizeUpgradeInternal(); return card;
        }
        T Up<T>() where T : CardModel => (T)Plus(typeof(T));
        (Type Type, int Cost)[] reducedCosts = [
            (typeof(DeadlineTimer),1), (typeof(GloriousAfterglow),0), (typeof(SupercomputeLightning),0),
            (typeof(HyperdimensionalSpace),1), (typeof(FlowingThunder),0), (typeof(WindsGaze),0),
            (typeof(MoltenQuench),1), (typeof(Atheism),1)
        ];
        foreach (var spec in reducedCosts)
            check(Plus(spec.Type).EnergyCost.GetWithModifiers(CostModifiers.All) == spec.Cost, $"{spec.Type.Name} receives exactly its intended one-energy discount");
        (Type Type, string Variable, decimal Value)[] scalarValues = [
            (typeof(PerfectDodge),"Block",10), (typeof(GlacialForm),"Block",7), (typeof(GoddessConnection),"Block",10),
            (typeof(HymnPrayer),"Block",9), (typeof(GarlandsSea),"Block",15), (typeof(ArcadiaGate),"Block",24),
            (typeof(GlacialBloom),"Damage",24), (typeof(OrbitalStrike),"Damage",7),
            (typeof(CausalConvergence),"Damage",9), (typeof(TemporalFinality),"Damage",34),
            (typeof(CoordinatedSlash),"Damage",24), (typeof(EclipseDawn),"Damage",30), (typeof(EclipseDawn),"FlyingDamage",40)
        ];
        foreach (var spec in scalarValues)
            check(Plus(spec.Type).DynamicVars[spec.Variable].BaseValue == spec.Value, $"{spec.Type.Name} upgraded {spec.Variable} is {spec.Value}");
        // All 27 upgraded signals go through the real resolver, including natural and supercompute triples.
        (Type Type, decimal NormalDamage, decimal TripleDamage, decimal NormalBlock, decimal TripleBlock)[] signals = [
            (typeof(RedSignal),9,18,0,0), (typeof(YellowSignal),0,0,8,16), (typeof(BlueSignal),5,10,0,0),
            (typeof(FrostBlade),9,16,0,0), (typeof(RepulsiveBeam),8,14,0,0), (typeof(RetreatShot),6,10,5,8),
            (typeof(SwiftAssault),10,19,0,0), (typeof(BlazingFeathers),9,14,0,0), (typeof(FlowingThunder),0,0,0,0),
            (typeof(FeatherMass),0,0,6,9), (typeof(FadingGospel),0,0,0,0),
            (typeof(ShatteringImpact),10,16,0,0), (typeof(GlacialCirculation),9,13,0,0),
            (typeof(RadiantDaybreak),10,14,0,0), (typeof(OpeningStance),10,15,0,0), (typeof(ContinuousFire),8,16,0,0),
            (typeof(WaveSlash),7,10,0,0), (typeof(TransferCharge),0,0,0,0), (typeof(GrayRavenField),6,9,0,0),
            (typeof(EclipticGrace),8,12,0,0), (typeof(ExemptionSpace),5,9,0,0), (typeof(FictionalBarrier),0,0,9,12),
            (typeof(ElectricInduction),7,11,0,0), (typeof(ShadowManeuver),3,7,8,11), (typeof(FallingFire),11,16,0,0),
            (typeof(PrecisionVolley),9,16,0,0), (typeof(ThermalShot),6,9,0,0)
        ];
        check(signals.Select(x => x.Type).ToHashSet().SetEquals(permanent.OfType<SignalCard>().Select(c => c.GetType())), "every permanent signal is covered by upgraded resolution checks");
        var relics = (List<RelicModel>)AccessTools.Field(typeof(Player), "_relics").GetValue(player)!;
        relics.Add(ModelDb.Relic<SignalCore>());
        foreach (var spec in signals)
        foreach (var scenario in new[] { (0,false), (1,false), (2,false), (0,true), (1,true), (2,true) })
        {
            Reset(); var card = (SignalCard)Plus(spec.Type);
            state.Hand.AddInternal(card, silent: true);
            for (int i = 0; i < scenario.Item1; i++)
            {
                var auxiliary = card.SignalColor switch {
                    SignalColor.Red => (CardModel)combat.CreateCard<RedSignal>(player),
                    SignalColor.Yellow => combat.CreateCard<YellowSignal>(player),
                    _ => combat.CreateCard<BlueSignal>(player)
                };
                if (i == 0) auxiliary.UpgradeInternal();
                state.Hand.AddInternal(auxiliary, silent: true);
            }
            AccessTools.Method(runtime, "Capture").Invoke(null, [card]);
            state.Hand.RemoveInternal(card, true); state.PlayPile.AddInternal(card, silent: true);
            CustomResources<Supercompute>.Get(state).Amount = scenario.Item2 ? 1 : 0;
            await Invoke(card, "OnPlay", null, Play(card, target));
            bool triple = scenario.Item1 == 2 || scenario.Item2;
            check(Attacks.Sum(a => a.Damage * a.Hits) == (triple ? spec.TripleDamage : spec.NormalDamage) &&
                Blocks.Sum() == (triple ? spec.TripleBlock : spec.NormalBlock), $"{spec.Type.Name} upgraded damage and block: auxiliaries={scenario.Item1}, supercompute={scenario.Item2}");
            check(state.DiscardPile.Cards.Count == scenario.Item1 && CustomResources<Supercompute>.Get(state).Amount == 0,
                $"{spec.Type.Name} auxiliaries discard without executing their upgraded effects");
            if (card is FadingGospel)
                check(target.GetPower<WeakPower>()?.Amount == (triple ? 3 : 2) && CustomResources<Charge>.Get(state).Amount == (triple ? 2 : 1), "gospel upgrades weakness while preserving charge");
            if (card is FeatherMass) check(Draws.Single() == (triple ? 2 : 1), "mass upgrades block without increasing draw");
            if (card is TransferCharge)
                check(player.Creature.GetPower<SignalSupportPower>()!.Shots == (triple ? 4 : 3), "transfer upgrades future attack count");
        }
        relics.Clear();

        // Pure support cards use their upgraded variables in commands, not merely in card text.
        Reset(); var meal = Up<LuciasCooking>(); await Invoke(meal, "OnPlay", null, Play(meal));
        check(EnergyGains.Single() == 3 && Generated.Count == 1 && Generated[0].Pile == state.DiscardPile, "cooking upgrades energy and keeps one delayed dazed");
        harmony.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Random.Rng), "NextInt", [typeof(int)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(RollEveryday)));
        foreach (int roll in new[] { 0, 7 })
        {
            Reset(); EverydayRoll = roll; var chance = Up<SeventyPercent>(); await Invoke(chance, "OnPlay", null, Play(chance));
            check(Blocks.SequenceEqual(roll == 0 ? new[] { 10m } : [10m,10m]) && (roll == 7 || Draws.Single() == 2), "chance upgrades both block branches without changing draw");
        }
        Reset(); var support = Up<AllOutTogether>(); await Invoke(support, "OnPlay", null, Play(support));
        check(ExpansionTargets.Where(t => t.IsAlive).All(t => t.GetPower<VulnerablePower>()?.Amount == 3), "upgraded support applies three vulnerable to every enemy");
        harmony.Unpatch(AccessTools.Method(typeof(CardSelectCmd), "FromHand"), AccessTools.Method(typeof(HyperrealChecks), nameof(SelectWorldline)));
        harmony.Patch(AccessTools.Method(typeof(CardSelectCmd), "FromHand"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(SkipUpgradeMove)));
        Reset(); var ceremony = Up<RadiantCeremony>(); await Invoke(ceremony, "OnPlay", null, Play(ceremony));
        check(Draws.Single() == 3, "ceremony upgrade requests three cards before optional rearrangement");

        harmony.Patch(AccessTools.Method(typeof(CardSelectCmd), "FromSimpleGrid"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(TakeUpgradeSignal)));
        harmony.Patch(AccessTools.Method(typeof(CardSelectCmd), "FromHandForDiscard"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(ChooseUpgradeDiscard)));
        harmony.Patch(AccessTools.Method(typeof(CardPileCmd), "Add", [typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(AddUpgradeSignal)));
        Reset();
        for (int i = 0; i < 4; i++) state.DrawPile.AddInternal(combat.CreateCard<RedSignal>(player), silent: true);
        var tactical = Up<TacticalCalculation>(); await Invoke(tactical, "OnPlay", null, Play(tactical));
        check(state.DrawPile.Cards.Count == 1 && state.Hand.Cards.Count == 2 && state.DiscardPile.Cards.Count == 1, "tactical upgrade retrieves three signals then discards exactly one");
        Reset();
        for (int i = 0; i < CardPile.MaxCardsInHand; i++) state.Hand.AddInternal(combat.CreateCard<RedSignal>(player), silent: true);
        state.DrawPile.AddInternal(combat.CreateCard<BlueSignal>(player), silent: true);
        await Invoke(tactical, "OnPlay", null, Play(tactical));
        check(state.DrawPile.Cards.Count == 1 && state.DiscardPile.Cards.Count == 0, "full hand stops upgraded retrieval without forcing a discard");

        harmony.Unpatch(AccessTools.Method(typeof(CardSelectCmd), "FromChooseACardScreen"), AccessTools.Method(typeof(HyperrealChecks), nameof(SelectAbyss)));
        harmony.Patch(AccessTools.Method(typeof(CardSelectCmd), "FromChooseACardScreen"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(CaptureUpgradeChoice)));
        for (int mode = 0; mode < 2; mode++)
        {
            Reset(); AbyssChoice = mode; var abyss = Up<LightlessAbyss>();
            await Invoke(abyss, "OnPlay", null, Play(abyss, target));
            await Invoke(abyss, "OnPlay", null, new CardPlay { Card = abyss, Target = target, IsAutoPlay = false, PlayIndex = 1, PlayCount = 2, ResultPile = PileType.Discard, Resources = default });
            check(UpgradeChoices.SequenceEqual(new[] { (45m,true), (8m,true) }) && Attacks.Count == 2 &&
                Attacks.All(a => a.Damage == (mode == 0 ? 45 : 8) && a.Hits == (mode == 0 ? 1 : 5)), "upgraded abyss choice preview and replay use the same damage");
            Reset(); var normal = combat.CreateCard<LightlessAbyss>(player); await Invoke(normal, "OnPlay", null, Play(normal, target));
            check(UpgradeChoices.SequenceEqual(new[] { (36m,false), (6m,false) }), "upgraded options do not contaminate later unupgraded choices");
        }
        Reset();
    }
}
