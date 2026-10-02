using BaseLib.Abstracts;
using BaseLib.Patches.UI;
using ChaosPrototype.Gameplay;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ChaosPrototype.UI;

internal static class PreviewPanel
{
    internal static void Register()
    {
        ExtraCombatUi.RegisterCombatUiElement((ui, player, combat) =>
        {
            if (player.Character is not ChaosCharacter) return null;
            SignalRuntime.Reset();
            var panel = new VBoxContainer { Position = new Vector2(25, 380), MouseFilter = Control.MouseFilterEnum.Ignore };
            var status = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
            var preview = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
            panel.AddChild(status); panel.AddChild(preview); ui.AddChild(panel);
            var tinted = new Dictionary<NCard, Color>();
            void Restore()
            {
                foreach (var (node, color) in tinted)
                    if (GodotObject.IsInstanceValid(node)) node.Modulate = color;
                tinted.Clear();
            }
            void Refresh()
            {
                Restore();
                var state = player.PlayerCombatState;
                if (state == null) return;
                int charge = CustomResources<Charge>.Get(state).Amount;
                bool super = CustomResources<Supercompute>.Get(state).Amount > 0;
                status.Text = $"卡俄斯 · 原型\n充能 {charge} / 大招需 3\n超算：{(super ? "就绪" : "未就绪")}";
                var hand = NPlayerHand.Instance;
                CardModel? hovered = hand?.FocusedHolder?.CardModel;
                if (hand?.InCardPlay == true && AccessTools.Field(typeof(NPlayerHand), "_currentCardPlay").GetValue(hand) is NCardPlay active)
                    hovered = active.Holder.CardModel;
                if (SignalRuntime.Pending.ContainsKey(player)) { preview.Text = "结算中：暂不接受连续出牌"; return; }
                if (hovered is not SignalCard signal || hovered.Pile?.Type != PileType.Hand)
                { preview.Text = "悬停或拖拽信号球查看三消\n浅绿色标记将被弃置的两张"; return; }
                var pair = SignalRuntime.Preview(signal);
                bool natural = pair.Length == 2;
                var indices = pair.Select(c => Array.IndexOf(state.Hand.Cards.ToArray(), c) + 1);
                preview.Text = natural ? $"自然三消：弃置第 {string.Join("、", indices)} 张" : "未形成自然三消";
                if (super) preview.Text += natural ? "\n超算重叠：仍消耗，不额外强化" : "\n超算强化：不弃置额外牌";
                preview.Text += natural || super ? "\n使用卡面“三消”数值" : "\n使用卡面普通数值";
                foreach (var card in pair)
                {
                    var node = hand?.GetCard(card);
                    if (node == null) continue;
                    tinted[node] = node.Modulate;
                    node.Modulate = new Color(0.65f, 1f, 0.65f);
                }
            }
            var timer = new Godot.Timer { WaitTime = 0.1, Autostart = true };
            timer.Timeout += Refresh; panel.AddChild(timer);
            panel.TreeExiting += Restore;
            Refresh();
            return panel;
        });
    }
}
