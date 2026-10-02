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
Check(resource.Amount == 4, "charge retention"); resource.PrepForCombat<Charge>(null!);
Check(resource.Amount == 0 && !resource.ApplySharedModification, "charge reset/shared-free exclusion");
var assembly = typeof(ChaosCharacter).Assembly;
Check(assembly.GetTypes().Count(t => !t.IsAbstract && typeof(ChaosCard).IsAssignableFrom(t)) == 5, "five card types");
Check(assembly.GetTypes().Count(t => !t.IsAbstract && typeof(SignalCard).IsAssignableFrom(t)) == 3, "three signal types");
var red = new RedSignal(); var yellow = new YellowSignal(); var blue = new BlueSignal();
var superCard = new SupercomputeCard(); var ultimate = new Ultimate();
Check(red.DynamicVars["Damage"].BaseValue == 6 && red.DynamicVars["TripleDamage"].BaseValue == 12, "red values");
Check(yellow.DynamicVars["Block"].BaseValue == 5 && yellow.DynamicVars["TripleBlock"].BaseValue == 10, "yellow values");
Check(blue.DynamicVars["Damage"].BaseValue == 3 && blue.DynamicVars["TripleDamage"].BaseValue == 6, "blue values");
Check(ultimate.CanonicalKeywords.Contains(CardKeyword.Retain) && CustomResources<Charge>.CanonicalCost(ultimate) == 3, "ultimate retain/cost");
Check(new ChaosCharacter().StartingHp == 70 && new SignalCore().Localization.Count == 3, "character and relic constructors");
Check(red.DynamicVars["DoubleDamage"].BaseValue == 9 && yellow.DynamicVars["DoubleBlock"].BaseValue == 8 && blue.DynamicVars["DoubleDamage"].BaseValue == 5, "double values");
Check(superCard.DynamicVars["Cycle"].BaseValue == 2, "supercompute selection limit");
var visualOrder = new CardModel[] { superCard, ultimate, red, blue, yellow };
Check(MegaCrit.Sts2.Core.Helpers.HandLayoutHelper.GetInsertIndex(visualOrder, new CardModel[] { red, blue }, ultimate) == 0, "functional card displayed before signals");
Check(MegaCrit.Sts2.Core.Helpers.HandLayoutHelper.GetInsertIndex(visualOrder, new CardModel[] { ultimate, blue }, yellow) == 2, "visual insertion excludes selected or dragged holders");
// Populate only this test process's model table to exercise the real StartingDeck getter.
var table = (Dictionary<ModelId, AbstractModel>)AccessTools.Field(typeof(ModelDb), "_contentById").GetValue(null)!;
var character = new ChaosCharacter(); var relic = new SignalCore();
foreach (var model in new AbstractModel[] { red, yellow, blue, superCard, ultimate, character, relic })
{
    var id = ModelDb.GetId(model.GetType());
    table[id] = model;
}
var upgradedSuper = superCard.ToMutable();
AccessTools.Method(typeof(SupercomputeCard), "OnUpgrade").Invoke(upgradedSuper, null);
Check(upgradedSuper.DynamicVars["Cycle"].BaseValue == 3 && upgradedSuper.EnergyCost.Canonical == 1, "upgraded supercompute cycles three but still costs one");
var deck = character.StartingDeck.ToArray();
Check(deck.Length == 11 && deck.Count(c => c is RedSignal) == 3 && deck.Count(c => c is YellowSignal) == 3 && deck.Count(c => c is BlueSignal) == 3, "11-card starting deck composition");
Check(deck.Count(c => c is Ultimate) == 1 && deck.Count(c => c is SupercomputeCard) == 1 && character.StartingRelics.Single() is SignalCore, "starter utility and relic");
// Apply real Harmony patches to the actual installed assembly without executing a combat.
// This catches bad overloads, target methods and injected parameter signatures.
var harmony = new Harmony("ChaosPrototype");
try
{
    ChaosPrototype.Main.Initialize();
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
