using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using ChaosPrototype.Core;
using ChaosPrototype.Gameplay;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using System.Reflection;

int checks = 0;
void Check(bool pass, string label) { checks++; if (!pass) throw new Exception(label); }
var metrics = new DebugMetrics();
metrics.Record(1, false);
metrics.Record(2, false);
metrics.Record(3, false);
metrics.Record(1, true);
metrics.Record(3, true);
Check(metrics.Single == 2 && metrics.Double == 1 && metrics.Triple == 2, "metrics classify by actual consumed cards, not enhanced strength");
Check(metrics.Supercompute == 2, "supercompute counted separately including overlap with natural triple");
metrics.MarkIntervention();
Check(metrics.Interventions == 1 && new DebugMetrics().Triple == 0, "interventions marked and new combat starts empty");
try { metrics.Record(0, false); throw new Exception("invalid consumption accepted"); }
catch (ArgumentOutOfRangeException) { Check(metrics.Single == 2, "invalid consumption leaves statistics unchanged"); }
for (int n = 1; n <= 8; n++)
{
    for (int code = 0; code < 1 << (2 * n); code++)
    {
        var hand = Enumerable.Range(0, n).Select(i => new Token((SignalColor)((code >> (2 * i)) & 3))).ToArray();
        for (int i = 0; i < n; i++)
        {
            var pair = SignalRules.Auxiliary(hand, hand[i], c => c.Color);
            var start = Enumerable.Range(0, Math.Max(0, n - 2)).FirstOrDefault(s => s <= i && i <= s + 2 && hand[s].Color != SignalColor.None && hand[s].Color == hand[s+1].Color && hand[s].Color == hand[s+2].Color, -1);
            var pairStart = Enumerable.Range(0, Math.Max(0, n - 1)).FirstOrDefault(s => s <= i && i <= s + 1 && hand[s].Color != SignalColor.None && hand[s].Color == hand[s+1].Color, -1);
            var expected = start < 0 ? (pairStart < 0 ? Array.Empty<Token>() : new[] { hand[pairStart == i ? i + 1 : pairStart] }) : Enumerable.Range(start, 3).Where(k => k != i).Select(k => hand[k]).ToArray();
            Check(pair.Length == expected.Length && pair.Zip(expected).All(p => ReferenceEquals(p.First, p.Second)), "actual mod matching oracle");
        }
    }
}
var r = new Token(SignalColor.Red); var a = new Token(SignalColor.Red); var b = new Token(SignalColor.Red);
Check(SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { r, b }, a), "snapshot validates removal");
Check(!SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { b, r }, a), "snapshot rejects reorder");
Check(!SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { r }, a), "snapshot rejects missing auxiliary");
Check(!SignalRules.UnchangedAfterRemoval(new[] { r, a, b }, new[] { r, b, new Token(SignalColor.None) }, a), "snapshot rejects draw");
Check(SignalRules.Auxiliary(new[] { r, a, b }, new Token(SignalColor.Red), c => c.Color).Length == 0, "same-color foreign instance rejected");
foreach (int natural in new[] { 1, 2, 3 })
foreach (bool super in new[] { false, true })
{
    var play = new PlayResolution();
    var first = play.Resolve(natural, super);
    Check(first == new Resolution(super ? 3 : natural, natural > 1, super), "natural/super overlap truth table");
    Check(ResonanceRules.IsTriple(first.Strength) == (super || natural == 3), "all super-enhanced plays qualify for triple synergies");
    var replay = play.Resolve(3, true);
    Check(replay == new Resolution(super ? 3 : natural, false, false), "replay never consumes new pair/super");
}
// Every arrival is inserted into its section without changing signal order.
var arrivals = new[] { new Token(SignalColor.Red), new Token(SignalColor.None), new Token(SignalColor.Blue), new Token(SignalColor.None), new Token(SignalColor.Red) };
var ordered = new List<Token>();
foreach (var token in arrivals) ordered.Insert(SignalRules.HandInsertIndex(ordered, token, c => c.Color), token);
Check(ordered.SequenceEqual(new[] { arrivals[1], arrivals[3], arrivals[0], arrivals[2], arrivals[4] }), "stable functional-left signal-right insertion");
Check(SignalRules.Auxiliary(ordered, arrivals[0], c => c.Color).Length == 0, "different color still blocks matching");
var resource = new Charge(); resource.ModifyAmount(4); resource.StartOfTurnReset(null!, null!);
Check(ResonanceRules.PerfectBlock(true, 7, 0), "full block triggers dodge");
Check(!ResonanceRules.PerfectBlock(true, 7, 3), "partial block cannot trigger dodge");
Check(!ResonanceRules.PerfectBlock(true, 0, 0), "zero damage cannot trigger dodge");
Check(!ResonanceRules.PerfectBlock(false, 7, 0), "nonattack damage cannot trigger dodge");
Check(Enumerable.Range(1, 7).Where(t => ResonanceRules.DeadlineDue(1, t)).SequenceEqual(new[] { 2, 4, 6 }), "deadline starts next turn then supplies every second turn");
Check(Enumerable.Range(4, 7).Where(t => ResonanceRules.DeadlineDue(4, t)).SequenceEqual(new[] { 5, 7, 9 }), "deadline schedule follows application turn");
Check(resource.Amount == 4, "charge retention"); resource.PrepForCombat<Charge>(null!);
Check(resource.Amount == 0 && !resource.ApplySharedModification, "charge reset/shared-free exclusion");
var assembly = typeof(ChaosCharacter).Assembly;
Check(assembly.GetTypes().Count(t => !t.IsAbstract && typeof(ChaosCard).IsAssignableFrom(t)) == 18, "eighteen card types");
Check(assembly.GetTypes().Count(t => !t.IsAbstract && typeof(SignalCard).IsAssignableFrom(t)) == 6, "six signal types");
var red = new RedSignal(); var yellow = new YellowSignal(); var blue = new BlueSignal();
var superCard = new SupercomputeCard(); var ultimate = new Ultimate(); var dodge = new PerfectDodge();
Check(dodge.Rarity == CardRarity.Basic && dodge.DynamicVars["Block"].BaseValue == 7 && dodge.EnergyCost.Canonical == 1 && !dodge.CanonicalKeywords.Contains(CardKeyword.Exhaust), "starter dodge is one cost seven block and reusable");
Check(superCard.Rarity == CardRarity.Uncommon && superCard.Localization.Any(l => l.Item2 == "主动超算"), "active supercompute renamed while preserving reward rarity");
ChaosCard[] abilities = [new DeadlineTimer(), new GloriousAfterglow(), new SupercomputeLightning()];
Check(abilities.All(c => c.Type == CardType.Power) && abilities[0].Rarity == CardRarity.Rare && abilities[0].EnergyCost.Canonical == 2 && abilities.Skip(1).All(c => c.Rarity == CardRarity.Uncommon && c.EnergyCost.Canonical == 1), "resonance power card rarities and costs");
ChaosCard[] ravens = [new FrostBlade(), new GlacialForm(), new GlacialBloom(), new RepulsiveBeam(), new GoddessConnection(), new ArcadiaGate(), new RetreatShot(), new TacticalCalculation(), new OrbitalStrike()];
Check(ravens.Count(c => c.Rarity == CardRarity.Common) == 4 && ravens.Count(c => c.Rarity == CardRarity.Uncommon) == 2 && ravens.Count(c => c.Rarity == CardRarity.Rare) == 3, "new reward rarity distribution includes three distinct rares");
Check(new ChaosCard[] { red, yellow, blue, ultimate }.All(c => c.Rarity == CardRarity.Basic), "starter-only cards excluded from normal reward rarity rolls");
Check(ravens.All(c => c.MaxUpgradeLevel == 0), "first testing batch has no upgrades");
Check(ravens.OfType<BurstCard>().All(c => c.CanonicalKeywords.Contains(CardKeyword.Retain) && c.EnergyCost.Canonical == 0 && CustomResources<Charge>.CanonicalCost(c) == (c is GlacialBloom ? 4 : 3)), "all three bursts retain and require their charge cost");
Check(ravens.Single(c => c is ArcadiaGate).CanonicalKeywords.Contains(CardKeyword.Exhaust), "healing burst exhausts");
Check(ravens.OfType<SignalCard>().All(c => c.EnergyCost.Canonical == 1 && !c.CanonicalKeywords.Contains(CardKeyword.Retain)), "new signals cost one and do not retain");
Check(SignalRules.Auxiliary(new CardModel[] { red, ravens[0], ravens[3] }, ravens[0], c => c is SignalCard signal ? signal.SignalColor : SignalColor.None).Length == 2, "different named red balls combine");
var turn = new RavenTurnRules(); turn.EnterTurn(1); turn.ArmConnection(); turn.ArmConnection();
Check(turn.RecordTriple() == 7 && !turn.ConnectionReady && turn.IceBonus == 6, "connection arms once without stacking");
Check(turn.RecordTriple() == 0 && turn.IceBonus == 12, "connection consumed by first triple");
turn.RecordTriple(); Check(turn.IceBonus == 12 && turn.Triples == 3, "ice bonus capped after two triples");
turn.ArmConnection(); turn.EnterTurn(1); Check(turn.ConnectionReady && turn.Triples == 3, "same turn reads preserve state");
turn.EnterTurn(2); Check(!turn.ConnectionReady && turn.Triples == 0 && turn.IceBonus == 0, "new turn expires bonus and connection");
var gathered = RavenTurnRules.Gather(ordered, SignalColor.Red, c => c.Color);
Check(gathered.SequenceEqual(new[] { arrivals[1], arrivals[3], arrivals[2], arrivals[0], arrivals[4] }), "gather preserves functional section and relative order");
Check(RavenTurnRules.Gather(gathered, SignalColor.Yellow, c => c.Color).SequenceEqual(gathered), "absent selected color leaves order unchanged");
Check(red.DynamicVars["Damage"].BaseValue == 6 && red.DynamicVars["TripleDamage"].BaseValue == 12, "red values");
Check(yellow.DynamicVars["Block"].BaseValue == 5 && yellow.DynamicVars["TripleBlock"].BaseValue == 10, "yellow values");
Check(blue.DynamicVars["Damage"].BaseValue == 3 && blue.DynamicVars["TripleDamage"].BaseValue == 6, "blue values");
Check(ultimate.CanonicalKeywords.Contains(CardKeyword.Retain) && CustomResources<Charge>.CanonicalCost(ultimate) == 3, "ultimate retain/cost");
Check(new ChaosCharacter().StartingHp == 70 && new SignalCore().Localization.Count == 3, "character and relic constructors");
Check(new ChaosCard[] { red, yellow, blue }.All(c => c.DynamicVars.All(v => !v.Key.StartsWith("Double")) && c.Localization!.All(loc => !loc.Item2.Contains("双消："))), "basic signals have no double bonus or bonus description");
Check(superCard.DynamicVars["Cycle"].BaseValue == 2, "supercompute selection limit");
var visualOrder = new CardModel[] { superCard, ultimate, red, blue, yellow };
Check(MegaCrit.Sts2.Core.Helpers.HandLayoutHelper.GetInsertIndex(visualOrder, new CardModel[] { red, blue }, ultimate) == 0, "functional card displayed before signals");
Check(MegaCrit.Sts2.Core.Helpers.HandLayoutHelper.GetInsertIndex(visualOrder, new CardModel[] { ultimate, blue }, yellow) == 2, "visual insertion excludes selected or dragged holders");
// Populate only this test process's model table to exercise the real StartingDeck getter.
var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
var character = new ChaosCharacter(); var relic = new SignalCore();
foreach (var model in new AbstractModel[] { red, yellow, blue, superCard, ultimate, dodge, character, relic, new AfterglowPower(), new LightningPower(), new PerfectDodgePower(), new DeadlinePower() })
{
    var id = ModelDb.GetId(model.GetType());
    table[id] = model;
}
var upgradedSuper = superCard.ToMutable();
// Exercise actual damage and turn-end hooks without loading a Godot scene.
var ownerCreature = new MegaCrit.Sts2.Core.Entities.Creatures.Creature(null!, 70, 70);
var enemyCreature = new MegaCrit.Sts2.Core.Entities.Creatures.Creature(null!, 20, 20);
AccessTools.Field(enemyCreature.GetType(), "<Side>k__BackingField").SetValue(enemyCreature, MegaCrit.Sts2.Core.Combat.CombatSide.Enemy);
var glowPower = (AfterglowPower)ModelDb.Power<AfterglowPower>().ToMutable();
var lightningPower = (LightningPower)ModelDb.Power<LightningPower>().ToMutable();
foreach (var power in new PowerModel[] { glowPower, lightningPower }) AccessTools.Property(typeof(PowerModel), "Owner").SetValue(power, ownerCreature);
AccessTools.Property(typeof(AfterglowPower), "Active").SetValue(glowPower, true);
AccessTools.Property(typeof(LightningPower), "Active").SetValue(lightningPower, true);
Check(glowPower.ModifyDamageMultiplicative(enemyCreature, 10, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, ownerCreature, red) == 1.3m, "afterglow multiplies owner's attacks by 30 percent");
Check(glowPower.ModifyDamageMultiplicative(enemyCreature, 10, MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered, ownerCreature, null) == 1m, "afterglow excludes nonattack damage");
Check(lightningPower.ModifyDamageMultiplicative(enemyCreature, 10, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, ownerCreature, red) == 1.1m && lightningPower.ModifyDamageMultiplicative(ownerCreature, 10, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, enemyCreature, null) == 1m, "lightning increases enemy incoming damage only");
await lightningPower.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Enemy, [enemyCreature]);
Check(lightningPower.Active, "enemy-turn lightning survives to player's next turn");
await lightningPower.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [ownerCreature]);
await glowPower.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [ownerCreature]);
Check(!lightningPower.Active && !glowPower.Active, "damage bonuses expire at owner's turn end");
AccessTools.Method(typeof(SupercomputeCard), "OnUpgrade").Invoke(upgradedSuper, null);
Check(upgradedSuper.DynamicVars["Cycle"].BaseValue == 3 && upgradedSuper.EnergyCost.Canonical == 1, "upgraded supercompute cycles three but still costs one");
var deck = character.StartingDeck.ToArray();
Check(deck.Length == 11 && deck.Count(c => c is RedSignal) == 3 && deck.Count(c => c is YellowSignal) == 3 && deck.Count(c => c is BlueSignal) == 3, "11-card starting deck composition");
Check(deck.Count(c => c is Ultimate) == 1 && deck.Count(c => c is PerfectDodge) == 1 && !deck.Any(c => c is SupercomputeCard) && character.StartingRelics.Single() is SignalCore, "starter utility and relic");
// Apply real Harmony patches to the actual installed assembly without executing a combat.
// This catches bad overloads, target methods and injected parameter signatures.
var harmony = new Harmony("ChaosPrototype");
try
{
    // Mod settings require native Godot; this console harness only validates patch installation.
    harmony.PatchAll(typeof(ChaosPrototype.Main).Assembly);
    Check(Harmony.GetPatchInfo(typeof(CardPileCmd).GetMethod(nameof(CardPileCmd.AddDuringManualCardPlay))!)!.Owners.Contains("ChaosPrototype"), "capture patch installed");
    Check(Harmony.GetPatchInfo(typeof(CardCmd).GetMethod(nameof(CardCmd.AutoPlay))!)!.Owners.Contains("ChaosPrototype"), "autoplay patch installed");
    Check(Harmony.GetPatchInfo(typeof(CardModel).GetMethod(nameof(CardModel.TryManualPlay))!)!.Owners.Contains("ChaosPrototype"), "queue guard installed");
    Check(Harmony.GetPatchInfo(typeof(CardPile).GetMethod(nameof(CardPile.AddInternal))!)!.Owners.Contains("ChaosPrototype"), "hand section insertion installed");
    Check(Harmony.GetPatchInfo(typeof(MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand).GetMethod("Add")!)!.Owners.Contains("ChaosPrototype"), "visual insertion installed");
    Console.WriteLine("PASS actual Harmony patch installation (not in-game execution)");
}
finally { harmony.UnpatchAll("ChaosPrototype"); }
Console.WriteLine($"PASS {checks} checks against actual prototype code");
Console.WriteLine("Not covered: live Godot combat, card previews/animations, save/load, multiplayer.");
record Token(SignalColor Color);
