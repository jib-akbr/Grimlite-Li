using Grimoire.Game;
using Grimoire.UI;
using System.Threading;
using System;
using System.Threading.Tasks;

public class TauntCycle : IDisposable
{
    private CancellationTokenSource _cts;
    private Task _runningTask;

    private static TauntCycle _instance;
    public static TauntCycle Instance
    {
        get
        {
            if (_instance == null)
                _instance = new TauntCycle();
            return _instance;
        }
    }

    private TauntCycle()
    {
        _cts = new CancellationTokenSource();
    }

    public void StartTaunt(int cycle, string mon, int second, int count)
    {
        // Cegah double-run
        if (_runningTask != null && !_runningTask.IsCompleted)
            return;

        _runningTask = Task.Run(() => Taunt(cycle, mon, second, count));
    }

    public async Task Taunt(int cycle, string mon, int second, int count)
    {
        if (count > cycle)
            count %= cycle;

        string prevTarget = "";

        while (!_cts.IsCancellationRequested)
        {
            if (count <= 0)
            {
                prevTarget = Player.GetTargetName();
                BotManager.Instance.ActiveBotEngine.paused = true;

                Player.AttackMonster(mon);
                await Task.Delay(Player.SkillAvailable("5"));
                Player.UseSkill("5");
                count = cycle;
            }

            BotManager.Instance.ActiveBotEngine.paused = false;

            Player.AttackMonster(prevTarget);
            count--;

            await Task.Delay(second / cycle * 1000, _cts.Token);
        }
    }

    public void Stop()
    {
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        _cts = new CancellationTokenSource(); // siapkan CTS baru
        _runningTask = null;
    }

    public static void Reset()
    {
        if (_instance != null)
        {
            _instance.Dispose();
            _instance = null;
        }
    }
}