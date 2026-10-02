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
            DebugPanel.Attach(ui, panel, player, combat);
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
                var raven = RavenRuntime.Get(player);
                status.Text = $"卡俄斯 · 原型\n充能 {charge} / 大招需 3–4\n超算：{(super ? "就绪" : "未就绪")}\n本回合三消 {raven.Triples} / 冰华加伤 {raven.IceBonus}\n女神连接：{(raven.ConnectionReady ? "就绪" : "未就绪")}";
                if (player.Creature.GetPower<PerfectDodgePower>() != null) status.Text += "\n极限闪避：等待完整格挡";
                if (player.Creature.GetPower<HyperdimensionalPower>() is { } space) status.Text += $"\n超维空间：视点 {space.Viewpoints} / 3";
                if (player.Creature.GetPower<DeadlinePower>() is { } deadline) status.Text += $"\n死线计时：{deadline.TurnsUntilSupply} 回合后补球";
                if (player.Creature.GetPower<AfterglowPower>() is { } glow) status.Text += $"\n光耀余晖：{(glow.Active ? "增伤 30%" : "等待三消")}";
                if (player.Creature.GetPower<LightningPower>() is { } lightning) status.Text += $"\n超算闪电：{(lightning.Active ? "敌人承伤 +10%" : "等待超算")}";
                var hand = NPlayerHand.Instance;
                CardModel? hovered = hand?.FocusedHolder?.CardModel;
                if (hand?.InCardPlay == true && AccessTools.Field(typeof(NPlayerHand), "_currentCardPlay").GetValue(hand) is NCardPlay active)
                    hovered = active.Holder.CardModel;
                if (SignalRuntime.Pending.ContainsKey(player)) { preview.Text = "结算中：暂不接受连续出牌"; return; }
                if (hovered is not SignalCard signal || hovered.Pile?.Type != PileType.Hand)
                { preview.Text = "悬停或拖拽信号球查看双消／三消\n浅绿色标记将被弃置的辅牌"; return; }
                var pair = SignalRuntime.Preview(signal);
                bool natural = pair.Length > 0;
                string match = pair.Length == 2 ? "三消" : "双消";
                var indices = pair.Select(c => Array.IndexOf(state.Hand.Cards.ToArray(), c) + 1);
                preview.Text = natural ? $"{match}：弃置第 {string.Join("、", indices)} 张" : "单消：没有相邻同色球";
                if (super) preview.Text += natural ? "\n消耗超算：按三消数值结算" : "\n超算强化：不弃置额外牌";
                preview.Text += super || pair.Length == 2 ? "\n使用卡面“三消”数值" : natural ? "\n双消仅弃辅牌，使用普通数值" : "\n使用卡面普通数值";
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
