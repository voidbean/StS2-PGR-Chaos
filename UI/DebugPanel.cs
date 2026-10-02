using BaseLib.Abstracts;
using ChaosPrototype.Gameplay;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace ChaosPrototype.UI;

internal static class DebugPanel
{
    internal static bool Busy { get; private set; }

    internal static void Attach(Control ui, VBoxContainer statusPanel, Player player, CombatState combat)
    {
        DebugSession.Begin(player, combat);
        Busy = false;
        var toggle = new Button { Text = "调试面板", FocusMode = Control.FocusModeEnum.None };
        statusPanel.AddChild(toggle);
        var panel = new PanelContainer { Visible = false, Position = new Vector2(1050, 130), CustomMinimumSize = new Vector2(480, 0) };
        ui.AddChild(panel);
        var body = new VBoxContainer();
        panel.AddChild(body);
        var header = new HBoxContainer();
        body.AddChild(header);
        header.AddChild(new Label { Text = "卡俄斯 · 调试（仅单人）", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "关闭", FocusMode = Control.FocusModeEnum.None };
        header.AddChild(close);
        close.Pressed += () => panel.Hide();
        toggle.Pressed += () => { panel.Visible = !panel.Visible; if (panel.Visible) DebugSession.Opened = true; };

        var cards = ModelDb.AllCards.OfType<ChaosCard>().OrderBy(c => c.Id.Entry).ToArray();
        var choice = new OptionButton();
        foreach (var card in cards) choice.AddItem(card.Title);
        body.AddChild(choice);
        var notice = new Label { Text = "仅列出已经实现的卡牌。", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
        var actions = new List<Button>();
        bool CanChange() => !Busy && combat.Players.Count == 1 &&
            player.PlayerCombatState != null && player.Creature.IsAlive &&
            CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding &&
            !CombatManager.Instance.PlayerActionsDisabled && CombatManager.Instance.IsPartOfPlayerTurn(player) &&
            !CombatManager.Instance.IsExecutingCardOrPotionEffect(player) &&
            !SignalRuntime.Pending.ContainsKey(player) &&
            NPlayerHand.Instance is { InCardPlay: false, IsInCardSelection: false };

        void AddAction(HBoxContainer row, string title, Func<Task> action)
        {
            var button = new Button { Text = title, FocusMode = Control.FocusModeEnum.None };
            row.AddChild(button);
            actions.Add(button);
            button.Pressed += async () =>
            {
                if (!CanChange()) { notice.Text = "请等待玩家回合、选牌和结算结束。"; return; }
                Busy = true;
                DebugSession.Metrics.MarkIntervention();
                try { await action(); if (GodotObject.IsInstanceValid(notice)) notice.Text = $"已执行：{title}"; }
                catch (Exception e) { if (GodotObject.IsInstanceValid(notice)) notice.Text = $"操作失败：{e.Message}"; }
                finally { Busy = false; }
            };
        }

        var cardRow = new HBoxContainer(); body.AddChild(cardRow);
        AddAction(cardRow, "加入手牌", async () =>
        {
            if (choice.Selected < 0 || choice.Selected >= cards.Length) throw new InvalidOperationException("请先选牌");
            if (player.PlayerCombatState!.Hand.Cards.Count >= CardPile.MaxCardsInHand) throw new InvalidOperationException("手牌已满");
            await CardPileCmd.Add(combat.CreateCard(cards[choice.Selected], player), PileType.Hand);
        });
        AddAction(cardRow, "加入本局牌组", async () =>
        {
            if (choice.Selected < 0 || choice.Selected >= cards.Length) throw new InvalidOperationException("请先选牌");
            var run = RunManager.Instance.DebugOnlyGetState() ?? throw new InvalidOperationException("当前没有进行中的冒险");
            var card = run.CreateCard(cards[choice.Selected], player);
            await CardPileCmd.Add(card, PileType.Deck);
        });
        var resourceRow = new HBoxContainer(); body.AddChild(resourceRow);
        resourceRow.AddChild(new Label { Text = "充能" });
        var amount = new SpinBox { MinValue = 0, MaxValue = 999, Step = 1, Value = 3 };
        resourceRow.AddChild(amount);
        AddAction(resourceRow, "设为此值", () => { CustomResources<Charge>.Get(player.PlayerCombatState!).Amount = (int)amount.Value; return Task.CompletedTask; });
        AddAction(resourceRow, "能量 +3", () => PlayerCmd.GainEnergy(3, player));
        var superRow = new HBoxContainer(); body.AddChild(superRow);
        AddAction(superRow, "开启超算", () => { CustomResources<Supercompute>.Get(player.PlayerCombatState!).Amount = 1; return Task.CompletedTask; });
        AddAction(superRow, "清除超算", () => { CustomResources<Supercompute>.Get(player.PlayerCombatState!).Amount = 0; return Task.CompletedTask; });
        body.AddChild(notice);
        var stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
        body.AddChild(stats);
        var exportRow = new HBoxContainer(); body.AddChild(exportRow);
        var export = new Button { Text = "导出本场小结", FocusMode = Control.FocusModeEnum.None };
        var previous = new Button { Text = "复制上一场小结", FocusMode = Control.FocusModeEnum.None };
        exportRow.AddChild(export); exportRow.AddChild(previous);
        export.Pressed += () => { DebugSession.Export(DebugSession.Summary); notice.Text = DebugSession.ExportStatus; };
        previous.Pressed += () => { DisplayServer.ClipboardSet(DebugSession.LastSummary); notice.Text = "已复制上一场小结"; };
        var timer = new Godot.Timer { WaitTime = 0.2, Autostart = true };
        timer.Timeout += () =>
        {
            if (!panel.Visible) return;
            stats.Text = DebugSession.Summary;
            foreach (var action in actions) action.Disabled = !CanChange();
        };
        panel.AddChild(timer);
    }
}
