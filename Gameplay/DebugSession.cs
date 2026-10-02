using ChaosPrototype.Core;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ChaosPrototype.Gameplay;

internal static class DebugSession
{
    private static Player? _player;
    private static CombatState? _combat;
    private static int _startingHp;
    private static bool _finished;
    internal static bool Opened { get; set; }
    internal static DebugMetrics Metrics { get; private set; } = new();
    internal static string LastSummary { get; private set; } = "暂无上一场记录";
    internal static string ExportStatus { get; private set; } = "";

    internal static void Begin(Player player, CombatState combat)
    {
        _player = player;
        _combat = combat;
        _startingHp = player.Creature.CurrentHp;
        _finished = false;
        Opened = false;
        ExportStatus = "";
        Metrics = new();
    }

    internal static void Record(Player player, int consumed, bool supercompute)
    {
        if (!_finished && ReferenceEquals(player, _player)) Metrics.Record(consumed, supercompute);
    }

    internal static string Summary => _player == null ? "暂无战斗" :
        $"卡俄斯调试 · {(_finished ? "已结束" : "进行中")}\n回合 {_combat?.RoundNumber ?? 0}\n生命 {_startingHp} → {_player.Creature.CurrentHp}（净变化 {_player.Creature.CurrentHp - _startingHp}，非累计伤害）\n{Metrics.Summary}\n{(Metrics.Interventions > 0 ? "本场使用过调试，不作为自然强度样本" : "未使用本面板修改资源；外部控制台操作不计入")}";

    internal static void Finish(PlayerCombatState state)
    {
        if (_finished || _player?.PlayerCombatState != state) return;
        _finished = true;
        LastSummary = Summary;
        if (Opened) Export(LastSummary);
    }

    internal static void Export(string summary)
    {
        try
        {
            var directory = System.IO.Path.Combine(OS.GetUserDataDir(), "ChaosPrototype-debug");
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, $"combat-{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt");
            System.IO.File.WriteAllText(path, summary);
            ExportStatus = $"已导出：{path}";
        }
        catch (Exception e) { ExportStatus = $"导出失败：{e.Message}"; }
    }
}
