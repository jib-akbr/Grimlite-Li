using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Grimoire.Game;
using Newtonsoft.Json.Linq;

namespace Grimoire.Networking.Handlers
{
    /// <summary>
    /// Ultra Gramiel taunt handler.
    ///
    /// Stage 1 (an orb is alive): Maid-style side parity — P1/P3 taunt their
    /// side's orb on odd shatters, P2/P4 on even. P1/P2 watch id:2, P3/P4
    /// watch id:3.
    ///
    /// Stage 2 (both orbs dead): cascade on Gramiel itself — every shatter
    /// advances P1 → P2 → P3 → P4 → P1 ... The counter resets on the stage
    /// transition so P1 always goes first. If no shatter packets arrive in
    /// stage 2, a 4s wall-clock timer drives the same cascade instead
    /// (suppressed while shatter-driven taunts are active). If the turn
    /// owner has the Vendetta aura it skips (leaving the slot unclaimed); ~900ms later any
    /// account without Vendetta claims the open slot (atomic file create =
    /// exactly one winner) and taunts Gramiel instead.
    ///
    /// Coordination is file-based in %TEMP%\gramiel_taunt (one tiny board
    /// file per account + one claim file per stage-2 shatter). All logging
    /// goes to the debug log — never the window title.
    ///
    /// All four handlers must be running before the fight so the shatter
    /// counters stay aligned. Do not restart a single handler mid-fight.
    /// </summary>
    public class HandlerUltraGramielTaunt : IJsonMessageHandler, IDisposable
    {
        public enum GramielPreset
        {
            P1 = 0,
            P2 = 1,
            P3 = 2,
            P4 = 3
        }

        // Stage-2 cascade order. Index in this array is the turn number mod 4.
        private static readonly GramielPreset[] CascadeOrder =
            { GramielPreset.P1, GramielPreset.P2, GramielPreset.P3, GramielPreset.P4 };

        private const int SubstituteDelayMs = 900;
        private const int HeartbeatMs = 2000;
        private const long BoardFreshTicks = 12L * 10000000L; // 12s
        private const long BucketTicks = 10000000L;           // 1s claim buckets
        private const long DedupeTicks = 5L * 1000000L;       // 0.5s dup window
        private const long SlotTicks = 4L * 10000000L;        // 4s stage-2 timer slots (Maid uses 5s: 20s cycle / 4)
        private const long RecentTauntTicks = 3L * 10000000L; // 3s shatter-taunt suppression

        private const string GramielTarget = "Gramiel";

        private readonly GramielPreset _preset;

        // Stage 1: shatters of our own side only (Maid crystalCount).
        private int _sideCount;

        // Stage 2: every shatter, all sides (cascade turn).
        private int _gramielCount;
        private bool _wasStage2;

        private int _taunting; // overlap guard: 0 idle, 1 busy

        // Dedupe: identical anims arriving twice (socket + pext) must only
        // count once or the turns flip.
        private string _lastKey;
        private long _lastKeyTicks;

        // Last known own Vendetta state, refreshed on stage-2 shatter events
        // and mirrored into the board file by the heartbeat.
        private volatile bool _lastVendetta;

        private CancellationTokenSource _cts;
        private Task _heartbeatTask;
        private readonly object _fileLock = new object();
        private long _lastPurgeTicks;
        private long _lastWatchSlot = -1;
        private bool _disposed;

        // Anims arrive as JSON "ct" packets (forwarded from pext by
        // Flash.ForwardPextCombat). The proxy routes them to us.
        public string[] HandledCommands { get; } = new[] { "ct" };

        public HandlerUltraGramielTaunt(GramielPreset preset)
        {
            _preset = preset;
            _cts = new CancellationTokenSource();
            WriteBoard();
            _heartbeatTask = Task.Run(() => Heartbeat(_cts.Token));
            Log($"[Gramiel-{_preset}] handler started (stage1 side parity, stage2 cascade P1>P2>P3>P4)");
        }

        public void Handle(JsonMessage message)
        {
            try
            {
                if (_disposed)
                    return;
                if (message?.DataObject?["anims"] == null)
                    return;

                JArray anims = message.DataObject["anims"] as JArray;
                if (anims == null)
                    return;

                foreach (JObject anim in anims)
                {
                    string msg = anim?["msg"]?.ToString()?.ToLower();
                    if (string.IsNullOrEmpty(msg))
                        continue;

                    // Maid matches just "shattering" — the full text varies.
                    if (!msg.Contains("shattering"))
                        continue;

                    int monId = ParseMonId(anim?["tInf"]?.ToString());
                    string tInf = anim?["tInf"]?.ToString();

                    long now = DateTime.UtcNow.Ticks;
                    string key = msg + "|" + monId;
                    if (key == _lastKey && now - _lastKeyTicks < DedupeTicks)
                    {
                        Log($"[Gramiel-{_preset}] dup shatter ignored msg='{msg}' tInf='{tInf}'");
                        continue;
                    }
                    _lastKey = key;
                    _lastKeyTicks = now;

                    bool leftAlive = IsOrbAlive("id:2");
                    bool rightAlive = IsOrbAlive("id:3");
                    bool stage2 = !leftAlive && !rightAlive;

                    if (!stage2)
                        HandleStage1(monId, tInf, msg);
                    else
                        HandleStage2(tInf, now);
                }
            }
            catch
            {
                // Swallow errors to avoid breaking networking pipeline.
            }
        }

        private static bool IsOrbAlive(string orb)
        {
            try { return World.IsMonsterAvailable(orb); }
            catch { return true; } // fail open: assume stage 1
        }

        #region Stage 1 — side parity on orbs

        private void HandleStage1(int monId, string tInf, string msg)
        {
            _wasStage2 = false;

            // Left presets only count id 2, right presets only id 3.
            // Unknown id (0) still counts for our side so a format change
            // degrades to alternating locally instead of never taunting.
            if (monId != 0)
            {
                switch (_preset)
                {
                    case GramielPreset.P1:
                    case GramielPreset.P2:
                        if (monId != 2)
                            return;
                        break;

                    case GramielPreset.P3:
                    case GramielPreset.P4:
                        if (monId != 3)
                            return;
                        break;
                }
            }

            _sideCount++;

            string side = monId == 3 ? "R" : "L";
            string target = monId == 3 ? "id:3" : monId == 2 ? "id:2" : OrbTarget();

            bool act;
            switch (_preset)
            {
                // Gramiel L1 / Gramiel R1: odd counts.
                case GramielPreset.P1:
                case GramielPreset.P3:
                    act = (_sideCount % 2 != 0);
                    break;

                // Gramiel L2 / Gramiel R2: even counts (and > 0).
                case GramielPreset.P2:
                case GramielPreset.P4:
                    act = (_sideCount % 2 == 0 && _sideCount > 0);
                    break;

                default:
                    act = false;
                    break;
            }

            string whose = (_sideCount % 2 != 0)
                ? (side == "R" ? "P3" : "P1")
                : (side == "R" ? "P4" : "P2");

            if (!act)
            {
                Log($"[Gramiel-{_preset}] stage1 SHATTER #{_sideCount} ({side}) msg='{msg}' tInf='{tInf}' — {whose} turn, holding");
                return;
            }

            Log($"[Gramiel-{_preset}] stage1 SHATTER #{_sideCount} ({side}) msg='{msg}' tInf='{tInf}' — MY TURN ({whose}), taunting {target}");
            Taunt(target, "stage1");
        }

        #endregion

        #region Stage 2 — cascade on Gramiel

        private void HandleStage2(string tInf, long now)
        {
            if (!_wasStage2)
            {
                _wasStage2 = true;
                _gramielCount = 0;
                Log($"[Gramiel-{_preset}] STAGE 2 (orbs dead) — cascade reset, P1 first");
            }

            _gramielCount++;
            GramielPreset owner = CascadeOrder[(_gramielCount - 1) % CascadeOrder.Length];

            if (owner == _preset)
            {
                bool vendetta = HasVendetta();
                _lastVendetta = vendetta;
                if (vendetta)
                {
                    WriteBoard();
                    Log($"[Gramiel-{_preset}] stage2 SHATTER #{_gramielCount} tInf='{tInf}' — MY TURN but Vendetta active, skipping (slot open for takeover)");
                    return;
                }

                // Owner claims first so substitutes arriving ~900ms later
                // see the slot as taken.
                TryClaim("G", now, out _);
                WriteBoard(taunt: true);
                Log($"[Gramiel-{_preset}] orbs dead, taunting Gramiel STAGE 2 (shatter #{_gramielCount}) — MY TURN");
                Taunt(GramielTarget, "stage2");
            }
            else
            {
                Log($"[Gramiel-{_preset}] stage2 SHATTER #{_gramielCount} tInf='{tInf}' — {owner} turn, watching for takeover");
                WatchForTakeover(owner, now, _gramielCount);
            }
        }

        /// <summary>
        /// Non-owner path: after a short delay, take the slot if nobody
        /// claimed it and we are clean. Atomic file create guarantees a
        /// single winner when several accounts race.
        /// </summary>
        private void WatchForTakeover(GramielPreset owner, long shatterTicks, int count)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(SubstituteDelayMs);
                    if (_disposed)
                        return;
                    if (!Player.IsLoggedIn || !Player.IsAlive)
                        return;

                    bool vendetta = HasVendetta();
                    _lastVendetta = vendetta;
                    WriteBoard();
                    if (vendetta)
                    {
                        Log($"[Gramiel-{_preset}] stage2 SHATTER #{count} slot open but I have Vendetta too — holding");
                        return;
                    }

                    if (FindClaim("G", shatterTicks))
                        return; // owner (or another substitute) got it

                    var info = ReadOwnerStatus(owner);
                    string why = info.found && info.fresh && info.vendetta
                        ? $"{owner} has Vendetta"
                        : info.found && info.fresh
                            ? $"{owner} silent"
                            : $"{owner} unknown/offline";

                    if (!TryClaim("G", shatterTicks, out string winner))
                    {
                        Log($"[Gramiel-{_preset}] stage2 SHATTER #{count} takeover race lost to {winner} — holding");
                        return;
                    }

                    WriteBoard(taunt: true);
                    Log($"[Gramiel-{_preset}] orbs dead, TAKING OVER taunting Gramiel STAGE 2 (shatter #{count}, {why})");
                    Taunt(GramielTarget, "takeover");
                }
                catch { }
            });
        }

        private static bool HasVendetta()
        {
            try { return Player.GetAuras(true, "Vendetta") > 0; }
            catch { return false; }
        }

        private static string TargetName()
        {
            try { return Player.GetTargetName ?? "?"; }
            catch { return "?"; }
        }

        #endregion

        /// <summary>
        /// Extracts the monster map id from tInf regardless of separator:
        /// "id:2", "id.2", "id-2", "id'2", "2" all yield 2.
        /// Returns 0 when no trailing number is present.
        /// </summary>
        private static int ParseMonId(string tInf)
        {
            try
            {
                if (string.IsNullOrEmpty(tInf))
                    return 0;
                Match m = Regex.Match(tInf, @"(\d+)\s*$");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int id))
                    return id;
            }
            catch { }
            return 0;
        }

        private string OrbTarget()
        {
            switch (_preset)
            {
                case GramielPreset.P1:
                case GramielPreset.P2:
                    return "id:2";
                case GramielPreset.P3:
                case GramielPreset.P4:
                    return "id:3";
                default:
                    return null;
            }
        }

        private void Taunt(string target, string role)
        {
            // Skip overlapping taunts instead of stacking paused flags.
            if (Interlocked.Exchange(ref _taunting, 1) != 0)
            {
                Log($"[Gramiel-{_preset}] taunt already in progress, skipping {role} taunt");
                return;
            }

            var engine = UI.BotManager.Instance?.ActiveBotEngine;
            if (engine != null)
                engine.paused = true;

            Task.Run(async () =>
            {
                try
                {
                    if (!Player.IsLoggedIn || !Player.IsAlive)
                    {
                        Log($"[Gramiel-{_preset}] not taunting — logged in: {Player.IsLoggedIn}, alive: {Player.IsAlive}");
                        return;
                    }

                    if (!string.IsNullOrEmpty(target))
                    {
                        try { Player.AttackMonster(target); } catch { }
                        await Task.Delay(400);
                        // Confirm the target stuck; re-attack once if not.
                        bool hasTarget = true;
                        try { hasTarget = Player.HasTarget; } catch { }
                        if (!hasTarget)
                        {
                            Log($"[Gramiel-{_preset}] no target on {target}, re-attacking");
                            try { Player.AttackMonster(target); } catch { }
                            await Task.Delay(400);
                            try { hasTarget = Player.HasTarget; } catch { }
                        }
                        Log($"[Gramiel-{_preset}] targeting {target} (locked={hasTarget}, current='{TargetName()}')");
                    }

                    // Wait for skill 5 cooldown (capped so we never hold
                    // the bot hostage), then force it through.
                    int wait = 0;
                    try { wait = Player.SkillAvailable("5"); } catch { }
                    if (wait > 0)
                        await Task.Delay(Math.Min(wait, 5000));

                    try { Player.ForceUseSkill("5"); } catch { }
                    await Task.Delay(300);
                    try { Player.UseSkill("5"); } catch { }
                    await Task.Delay(300);

                    // Retry once if the skill never went on cooldown.
                    try
                    {
                        if (Player.SkillAvailable("5") <= 0)
                            Log($"[Gramiel-{_preset}] {role} taunt fired on {target}");
                        else
                        {
                            Log($"[Gramiel-{_preset}] skill 5 still ready, retrying once");
                            try { Player.ForceUseSkill("5"); } catch { }
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Log($"[Gramiel-{_preset}] taunt error: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _taunting, 0);
                    try
                    {
                        var eng = UI.BotManager.Instance?.ActiveBotEngine;
                        if (eng != null)
                            eng.paused = false;
                    }
                    catch { }
                }
            });
        }

        #region Cross-client board + claims (%TEMP%\gramiel_taunt)

        private static string BoardDir()
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "gramiel_taunt");
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch { return null; }
        }

        private static string SafeUser(string user)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (char c in user ?? "?")
                    sb.Append(char.IsLetterOrDigit(c) ? c : '_');
                return sb.Length == 0 ? "unknown" : sb.ToString();
            }
            catch { return "unknown"; }
        }

        private static string Me()
        {
            try { return Game.Player.Username; } catch { return "?"; }
        }

        // Board format: preset / vendetta01 / lastTauntUtcTicks / lastUpdateUtcTicks
        private void WriteBoard(bool taunt = false)
        {
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return;
                long now = DateTime.UtcNow.Ticks;
                if (taunt)
                    _lastTauntTicks = now;
                string content = _preset + "\n" + (_lastVendetta ? "1" : "0")
                    + "\n" + _lastTauntTicks + "\n" + now;
                lock (_fileLock)
                    File.WriteAllText(Path.Combine(dir, SafeUser(Me()) + ".txt"), content);
            }
            catch { }
        }
        private long _lastTauntTicks;

        private struct OwnerInfo
        {
            public bool found;
            public bool fresh;
            public bool vendetta;
            public string user;
        }

        private OwnerInfo ReadOwnerStatus(GramielPreset owner)
        {
            var info = new OwnerInfo();
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return info;
                long now = DateTime.UtcNow.Ticks;
                string[] files;
                lock (_fileLock)
                    files = Directory.GetFiles(dir, "*.txt");
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("claim_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string[] lines;
                    try { lines = File.ReadAllLines(file); }
                    catch { continue; }
                    if (lines.Length < 4)
                        continue; // legacy board file, ignore
                    if (!string.Equals(lines[0].Trim(), owner.ToString(), StringComparison.OrdinalIgnoreCase))
                        continue;
                    info.found = true;
                    info.user = Path.GetFileNameWithoutExtension(file);
                    if (long.TryParse(lines[3], out long updated))
                        info.fresh = now - updated < BoardFreshTicks;
                    info.vendetta = lines[1].Trim() == "1";
                    return info;
                }
            }
            catch { }
            return info;
        }

        private static string ClaimPath(string side, long bucket)
        {
            return Path.Combine(BoardDir(), $"claim_{side}_{bucket}.txt");
        }

        // Atomic create: exactly one process wins per shatter bucket.
        private bool TryClaim(string side, long shatterTicks, out string winner)
        {
            winner = null;
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return false;
                long bucket = shatterTicks / BucketTicks;
                string path = ClaimPath(side, bucket);
                lock (_fileLock)
                {
                    using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs))
                        sw.Write(_preset + "|" + Me() + "|" + shatterTicks);
                }
                return true;
            }
            catch (IOException)
            {
                try { winner = File.ReadAllText(ClaimPath(side, shatterTicks / BucketTicks)).Split('|')[0]; }
                catch { winner = "?"; }
                return false;
            }
            catch { return false; }
        }

        private bool FindClaim(string side, long shatterTicks)
        {
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return false;
                long bucket = shatterTicks / BucketTicks;
                lock (_fileLock)
                {
                    for (long b = bucket - 1; b <= bucket + 1; b++)
                        if (File.Exists(ClaimPath(side, b)))
                            return true;
                }
            }
            catch { }
            return false;
        }

        private async Task Heartbeat(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    WriteBoard();
                    long now = DateTime.UtcNow.Ticks;
                    if (now - _lastPurgeTicks > 30L * 10000000L)
                    {
                        _lastPurgeTicks = now;
                        PurgeOldClaims(now);
                    }
                    Stage2Watchdog(now);
                    await Task.Delay(HeartbeatMs, token);
                }
                catch (TaskCanceledException) { break; }
                catch { }
            }
        }

        /// <summary>
        /// Stage-2 fallback when no shatter packets arrive after the orbs
        /// die: 5s wall-clock slots (same on all PCs in sync), owner
        /// P1→P2→P3→P4. Suppressed while shatter-driven taunts are active
        /// (any fresh claim). Vendetta skip + takeover, same as shatters.
        /// </summary>
        private void Stage2Watchdog(long now)
        {
            try
            {
                if (_disposed)
                    return;
                var engine = UI.BotManager.Instance?.ActiveBotEngine;
                if (engine == null || !engine.IsRunning)
                    return;
                if (!Player.IsLoggedIn || !Player.IsAlive)
                    return;
                string map;
                try { map = Player.Map; } catch { return; }
                if (string.IsNullOrEmpty(map) ||
                    map.IndexOf("ultragramiel", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    _lastWatchSlot = -1;
                    return;
                }
                if (IsOrbAlive("id:2") || IsOrbAlive("id:3"))
                {
                    _lastWatchSlot = -1; // stage 1: shatters drive taunts
                    return;
                }

                long slot = now / SlotTicks;
                if (slot == _lastWatchSlot)
                    return; // already acted this slot
                long slotAge = now - slot * SlotTicks;

                if (RecentActivity(now))
                    return; // shatter-driven taunts are active

                GramielPreset owner = CascadeOrder[slot % CascadeOrder.Length];

                if (owner == _preset)
                {
                    _lastWatchSlot = slot;
                    bool vendetta = HasVendetta();
                    _lastVendetta = vendetta;
                    WriteBoard();
                    if (vendetta)
                    {
                        Log($"[Gramiel-{_preset}] orbs dead STAGE 2 timer slot — MY TURN but Vendetta active, skipping (slot open for takeover)");
                        return;
                    }
                    if (SlotClaimed(slot))
                        return;
                    if (!TrySlotClaim(slot))
                        return;
                    WriteBoard(taunt: true);
                    Log($"[Gramiel-{_preset}] orbs dead, taunting Gramiel STAGE 2 (timer slot, MY TURN)");
                    Taunt(GramielTarget, "stage2-timer");
                    return;
                }

                // Non-owner: give the owner a head start, then take over.
                if (slotAge < 1500L * 10000L)
                    return;
                if (SlotClaimed(slot))
                    return;
                _lastWatchSlot = slot;

                var info = ReadOwnerStatus(owner);
                string why = info.found && info.fresh && info.vendetta
                    ? $"{owner} has Vendetta"
                    : info.found && info.fresh
                        ? $"{owner} silent"
                        : $"{owner} unknown/offline";

                bool meVendetta = HasVendetta();
                _lastVendetta = meVendetta;
                WriteBoard();
                if (meVendetta)
                {
                    Log($"[Gramiel-{_preset}] orbs dead STAGE 2 timer slot open ({why}) but I have Vendetta too — holding");
                    return;
                }
                if (!TrySlotClaim(slot, out string winner))
                {
                    Log($"[Gramiel-{_preset}] orbs dead STAGE 2 timer takeover race lost to {winner} — holding");
                    return;
                }
                WriteBoard(taunt: true);
                Log($"[Gramiel-{_preset}] orbs dead, TAKING OVER taunting Gramiel STAGE 2 (timer slot, {why})");
                Taunt(GramielTarget, "takeover-timer");
            }
            catch { }
        }

        private static string SlotClaimPath(long slot)
        {
            return Path.Combine(BoardDir(), $"claim_G_slot_{slot}.txt");
        }

        private bool SlotClaimed(long slot)
        {
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return false;
                lock (_fileLock)
                    return File.Exists(SlotClaimPath(slot));
            }
            catch { return false; }
        }

        private bool TrySlotClaim(long slot)
        {
            return TrySlotClaim(slot, out _);
        }

        private bool TrySlotClaim(long slot, out string winner)
        {
            winner = null;
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return false;
                string path = SlotClaimPath(slot);
                lock (_fileLock)
                {
                    using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs))
                        sw.Write(_preset + "|" + Me() + "|" + DateTime.UtcNow.Ticks);
                }
                return true;
            }
            catch (IOException)
            {
                try { winner = File.ReadAllText(SlotClaimPath(slot)).Split('|')[0]; }
                catch { winner = "?"; }
                return false;
            }
            catch { return false; }
        }

        // Any shatter-driven taunt claim written in the last few seconds.
        // Timer slot claims are excluded on purpose: a takeover in one
        // slot must not suppress the next slot's rightful owner.
        private bool RecentActivity(long now)
        {
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return false;
                string[] files;
                lock (_fileLock)
                    files = Directory.GetFiles(dir, "claim_G_*.txt");
                foreach (string file in files)
                {
                    try
                    {
                        string stem = Path.GetFileNameWithoutExtension(file);
                        if (stem.IndexOf("_slot_", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue; // timer claim: never suppresses
                        if (now - File.GetLastWriteTimeUtc(file).Ticks < RecentTauntTicks)
                            return true;
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        private void PurgeOldClaims(long now)
        {
            try
            {
                string dir = BoardDir();
                if (dir == null)
                    return;
                string[] files;
                lock (_fileLock)
                    files = Directory.GetFiles(dir, "claim_*.txt");
                foreach (string file in files)
                {
                    try
                    {
                        string stem = Path.GetFileNameWithoutExtension(file);
                        if (stem.IndexOf("_slot_", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Timer slot claim: purge by file age.
                            if (now - File.GetLastWriteTimeUtc(file).Ticks > 90L * 10000000L)
                                File.Delete(file);
                            continue;
                        }
                        // Bucket is the trailing number in claim_<side>_<bucket>.txt
                        int cut = stem.LastIndexOf('_');
                        if (cut > 0 && long.TryParse(stem.Substring(cut + 1), out long bucket))
                        {
                            if (now - bucket * BucketTicks > 90L * 10000000L)
                                File.Delete(file);
                        }
                        else if (now - File.GetLastWriteTimeUtc(file).Ticks > 120L * 10000000L)
                            File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        #endregion

        private static void Log(string msg)
        {
            try
            {
                string user = "?";
                try { user = Game.Player.Username; } catch { }
                UI.LogForm.Instance?.AppendDebug($"[{user}] {msg}");
            }
            catch { }
        }

        public void Dispose()
        {
            _disposed = true;
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _cts = null;
        }
    }
}
