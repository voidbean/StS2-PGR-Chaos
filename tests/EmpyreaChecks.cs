using ChaosPrototype.Core;
using ChaosPrototype.Gameplay;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static partial class HyperrealChecks
{
    private static readonly List<decimal> Blocks = [];
    private static readonly List<decimal> Draws = [];
    private static bool SkipMove;
    private static bool GainBlock(Creature creature, decimal amount, ref Task<decimal> __result)
    {
        Blocks.Add(amount);
        AccessTools.Property(typeof(Creature), "Block").SetValue(creature, creature.Block + (int)amount);
        __result = Task.FromResult(amount); return false;
    }
    private static bool DrawCards(decimal count, Player player, ref Task<IEnumerable<CardModel>> __result)
    {
        Draws.Add(count);
        var state = player.PlayerCombatState!;
        var drawn = state.DrawPile.Cards.Take(Math.Min((int)count, CardPile.MaxCardsInHand - state.Hand.Cards.Count)).ToArray();
        foreach (var card in drawn) { state.DrawPile.RemoveInternal(card, true); state.Hand.AddInternal(card, silent: true); }
        __result = Task.FromResult<IEnumerable<CardModel>>(drawn); return false;
    }
    private static bool ChooseHand(Player player, Func<CardModel, bool>? filter, ref Task<IEnumerable<CardModel>> __result)
    {
        if (Draws.LastOrDefault() != 2) throw new Exception("ceremony must draw two before choosing");
        var first = player.PlayerCombatState!.Hand.Cards.FirstOrDefault(c => filter?.Invoke(c) != false);
        __result = Task.FromResult<IEnumerable<CardModel>>(SkipMove || first == null ? [] : [first]); return false;
    }
    private static bool NotLocal(ref bool __result) { __result = false; return false; }
    private static bool PrayerSelfDamage(Creature target, decimal amount, ValueProp props, ref Task<IEnumerable<DamageResult>> __result)
    {
        if (amount != 3 || props != ValueProp.Unpowered) throw new Exception("prayer self-damage must be three blockable nonattack damage");
        int blocked = Math.Min(target.Block, 3);
        AccessTools.Property(typeof(Creature), "Block").SetValue(target, target.Block - blocked);
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(target, target.CurrentHp - (3 - blocked));
        __result = Task.FromResult<IEnumerable<DamageResult>>([new DamageResult(target, props) { BlockedDamage = blocked, UnblockedDamage = 3 - blocked }]); return false;
    }
    private static async Task RunEmpyrea(Action<bool, string> check, Player player, CombatState combat, Creature target, List<PowerModel> powers, Harmony harmony)
    {
        var state = player.PlayerCombatState!;
        target.CombatState = combat;
        powers.Clear(); Attacks.Clear(); Collapses.Clear();
        foreach (var pile in state.AllPiles) foreach (var card in pile.Cards.ToArray()) pile.RemoveInternal(card, true);
        AccessTools.Property(typeof(Creature), "CurrentHp").SetValue(player.Creature, 70);
        AccessTools.Property(typeof(Creature), "Block").SetValue(player.Creature, 0);
        CustomResources<Supercompute>.Get(state).Amount = 0;
        ChaosCard[] cards = [new HymnPrayer(), new FeatherMass(), new FadingGospel(), new GarlandsSea(), new RadiantCeremony()];
        var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
        foreach (var model in cards.Cast<AbstractModel>().Concat([new PrayerPower(), new GarlandsSeaPower(), new WeakPower()])) table[ModelDb.GetId(model.GetType())] = model;
        check(cards.All(c => c.MaxUpgradeLevel == 1) && cards.All(c => c.Type == CardType.Skill), "five empyrea skills support upgrades");
        check(cards.Select(c => c.Rarity).SequenceEqual(new[] { CardRarity.Uncommon, CardRarity.Common, CardRarity.Common, CardRarity.Rare, CardRarity.Uncommon }), "empyrea confirmed rarity distribution");
        check(CustomResources<Charge>.CanonicalCost(cards[3]) == 4 && cards[3].CanonicalKeywords.Contains(CardKeyword.Retain), "sea retains and costs four charge");
        harmony.Patch(AccessTools.Method(typeof(CreatureCmd), "GainBlock", [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(GainBlock)));
        harmony.Patch(AccessTools.Method(typeof(CardPileCmd), "Draw", [typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(DrawCards)));
        harmony.Patch(AccessTools.Method(typeof(CardSelectCmd), "FromHand"), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(ChooseHand)));
        harmony.Patch(AccessTools.Method(typeof(LocalContext), "IsMe", [typeof(Player)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(NotLocal)));
        harmony.Patch(AccessTools.Method(typeof(CreatureCmd), "Damage", [typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature)]), prefix: new HarmonyMethod(typeof(HyperrealChecks), nameof(PrayerSelfDamage)));
        T Card<T>() where T : CardModel { var card = combat.CreateCard<T>(player); state.PlayPile.AddInternal(card, silent: true); return card; }
        var yellow = Card<FeatherMass>();
        foreach (int strength in new[] { 1, 2, 3 })
        {
            await Invoke(yellow, "Effect", null, Play(yellow), strength);
            check(Blocks.Last() == (strength == 3 ? 6 : 3) && Draws.Last() == (strength == 3 ? 2 : 1), "feather mass ordinary double and triple block/draw");
        }
        var blue = Card<FadingGospel>();
        int charge = CustomResources<Charge>.Get(state).Amount;
        await Invoke(blue, "Effect", null, Play(blue, target), 1);
        check(CustomResources<Charge>.Get(state).Amount == charge + 1 && target.GetPower<WeakPower>()!.Amount == 1 && Attacks.Count == 0, "gospel normal charge and weakness without attack");
        ((List<PowerModel>)AccessTools.Field(typeof(Creature), "_powers").GetValue(target)!).Clear();
        await Invoke(blue, "Effect", null, Play(blue, target), 3);
        check(CustomResources<Charge>.Get(state).Amount == charge + 3 && target.GetPower<WeakPower>()!.Amount == 2, "gospel triple two charge and weakness");
        var prayerCard = Card<HymnPrayer>();
        await Invoke(prayerCard, "OnPlay", null, Play(prayerCard));
        var prayer = player.Creature.GetPower<PrayerPower>()!;
        var runtime = typeof(SignalCard).Assembly.GetType("ChaosPrototype.Gameplay.SignalRuntime")!;
        var red = combat.CreateCard<RedSignal>(player); state.Hand.AddInternal(red, silent: true);
        var secondRed = combat.CreateCard<SwiftAssault>(player); state.Hand.AddInternal(secondRed, silent: true);
        state.PlayPile.RemoveInternal(blue, true); state.Hand.AddInternal(blue, silent: true);
        async Task Complete(SignalCard card, int strength, bool capture = true, bool auto = false)
        {
            if (capture) AccessTools.Method(runtime, "Capture").Invoke(null, [card]);
            await (Task)AccessTools.Method(runtime, "AfterEffect").Invoke(null, [null, card, Play(card, auto: auto), strength])!;
        }
        await Complete(red, 1, auto: true);
        check(prayer.FirstColor == SignalColor.None, "autoplay cannot enter prayer");
        await Complete(red, 1); await Complete(red, 1, capture: false);
        check(prayer.FirstColor == SignalColor.Red && Collapses.Count == 0, "ordinary single enters prayer once despite replay");
        await Invoke(prayerCard, "OnPlay", null, Play(prayerCard));
        await prayer.BeforeSideTurnStart(null!, CombatSide.Player, [player.Creature], combat);
        check(prayer.FirstColor == SignalColor.Red && powers.OfType<PrayerPower>().Count() == 1 && Blocks.Last() == 6, "repeat prayer adds immediate block but preserves cross-turn color");
        await Complete(secondRed, 3);
        check(player.Creature.GetPower<PrayerPower>() == null && Collapses.Last() == (6m, ValueProp.Unpowered) && Blocks.Last() == 8, "same-color different-named signals yield pure oath");
        await Invoke(prayerCard, "OnPlay", null, Play(prayerCard));
        AccessTools.Property(typeof(Creature), "Block").SetValue(player.Creature, 3);
        await Complete(red, 1); await Complete(blue, 3);
        check(Collapses.Last() == (12m, ValueProp.Unpowered) && player.Creature.Block == 0 && player.Creature.CurrentHp == 70 && CustomResources<Supercompute>.Get(state).Amount == 1, "mixed oath fully blocked grants supercompute");
        CustomResources<Supercompute>.Get(state).Amount = 0; powers.RemoveAll(p => p is SupercomputePower);
        await Invoke(prayerCard, "OnPlay", null, Play(prayerCard));
        AccessTools.Property(typeof(Creature), "Block").SetValue(player.Creature, 2);
        await Complete(red, 1); await Complete(blue, 1);
        check(player.Creature.CurrentHp == 69 && CustomResources<Supercompute>.Get(state).Amount == 0, "partial block self-damage does not grant supercompute");
        var seaCard = Card<GarlandsSea>();
        await Invoke(seaCard, "OnPlay", null, Play(seaCard));
        var sea = player.Creature.GetPower<GarlandsSeaPower>()!;
        await Complete(red, 3); await Complete(red, 3, capture: false); await Complete(blue, 1); await Complete(secondRed, 3); await Complete(red, 3);
        check(sea.Inputs == 3 && sea.Accumulated == 20, "sea counts three manual inputs with triple eight ordinary four and replay deduplication");
        await Invoke(seaCard, "OnPlay", null, Play(seaCard));
        check(player.Creature.GetPower<GarlandsSeaPower>() == sea && sea.Accumulated == 20 && Blocks.Last() == 10, "repeat sea grants block without resetting or duplicating explosion");
        check(sea.ModifyCardPlayResultPileTypeAndPosition(red, false, default, PileType.Discard, CardPilePosition.Bottom).Item1 == PileType.Exhaust && sea.ModifyCardPlayResultPileTypeAndPosition(red, true, default, PileType.Discard, CardPilePosition.Bottom).Item1 == PileType.Exhaust, "sea exhausts subsequent balls including fourth and autoplay");
        check(sea.ModifyCardPlayResultPileTypeAndPosition(prayerCard, false, default, PileType.Discard, CardPilePosition.Bottom).Item1 == PileType.Discard, "sea does not exhaust non-ball cards");
        await Complete(red, 3, auto: true); check(sea.Inputs == 3, "sea autoplay gives no input");
        int explosions = Collapses.Count;
        await sea.BeforeSideTurnEndVeryEarly(null!, CombatSide.Enemy, [target]);
        check(Collapses.Count == explosions, "sea waits for owner's turn end");
        await sea.BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]); await sea.BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]);
        check(Collapses.Count == explosions + 1 && Collapses.Last() == (32m, ValueProp.Unpowered) && player.Creature.GetPower<GarlandsSeaPower>() == sea, "sea releases once before buffs expire and keeps ball exhaustion through turn-end effects");
        await sea.AfterSideTurnEnd(null!, CombatSide.Player, [player.Creature]);
        check(player.Creature.GetPower<GarlandsSeaPower>() == null, "sea expires after owner's turn ends");
        await Invoke(seaCard, "OnPlay", null, Play(seaCard)); sea = player.Creature.GetPower<GarlandsSeaPower>()!;
        await sea.BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]);
        check(Collapses.Last().Damage == 12, "sea has twelve baseline without inputs");
        await sea.AfterSideTurnEnd(null!, CombatSide.Player, [player.Creature]);
        await Invoke(seaCard, "OnPlay", null, Play(seaCard)); sea = player.Creature.GetPower<GarlandsSeaPower>()!;
        await Complete(red, 3); await Complete(blue, 3); await Complete(secondRed, 3);
        await sea.BeforeSideTurnEndVeryEarly(null!, CombatSide.Player, [player.Creature]);
        check(Collapses.Last().Damage == 36, "sea maximum thirty-six with three triples");
        await sea.AfterSideTurnEnd(null!, CombatSide.Player, [player.Creature]);
        foreach (var c in state.Hand.Cards.ToArray()) state.Hand.RemoveInternal(c, true);
        var functional = combat.CreateCard<RealmTraversal>(player); state.Hand.AddInternal(functional, silent: true);
        state.Hand.AddInternal(red, silent: true); state.Hand.AddInternal(blue, silent: true);
        var newYellow = combat.CreateCard<YellowSignal>(player); var newRed = combat.CreateCard<RedSignal>(player);
        state.DrawPile.AddInternal(newYellow, silent: true); state.DrawPile.AddInternal(newRed, silent: true);
        var ceremony = Card<RadiantCeremony>();
        await Invoke(ceremony, "OnPlay", null, Play(ceremony));
        check(state.Hand.Cards.SequenceEqual(new CardModel[] { functional, blue, newYellow, newRed, red }), "ceremony draws first then moves only one selected ball preserving all other order");
        var before = state.Hand.Cards.ToArray(); SkipMove = true;
        await Invoke(ceremony, "OnPlay", null, Play(ceremony));
        check(state.Hand.Cards.SequenceEqual(before), "ceremony permits skipping movement");
        check(RavenTurnRules.MoveToRight(before, ceremony).SequenceEqual(before), "foreign selection cannot enter hand");
    }
}
