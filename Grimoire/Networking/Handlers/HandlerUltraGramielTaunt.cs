using System;
using System.Threading;
using System.Threading.Tasks;
using Grimoire.Game;
using Newtonsoft.Json.Linq;

namespace Grimoire.Networking.Handlers
{
    /// <summary>
    /// Maid-style taunt handler for Ultra Gramiel.
    /// When enabled via Special handlers (P1–P4), this will periodically
    /// force attacks on Gramiel to maintain aggro, with per-preset offsets
    /// so multiple players can share the taunt cycle.
    ///
    /// The user is still responsible for configuring their normal skill
    /// rotation in the bot; this handler only handles the taunt timing.
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

        private readonly GramielPreset _preset;
        private CancellationTokenSource _cts;
        private Task _tauntTask;

        // Per-instance defense shattering counter, mirroring Maid's ultraBossHandler
        // logic on each client independently for Gramiel L1/L2/R1/R2.
        private int _defenseShatteringCount;

        // Listen to "ct" (combat/animation) packets so we can inspect the boss
        // text like Maid's AnimsMsgHandler (e.g., "shattering").
        public string[] HandledCommands { get; } = new[] { "ct" };

        public HandlerUltraGramielTaunt(GramielPreset preset)
        {
            _preset = preset;
            StartTauntLoop();
        }

        public void Handle(JsonMessage message)
        {
            // Mirror Maid's behaviour by reading animation text (e.g. "shattering")
            // and using a shared crystal counter to decide when each preset
            // should act on its side. This is similar to ultraBossHandler's
            // Gramiel L1/L2/R1/R2 logic.
            try
            {
                if (message?.DataObject?["anims"] == null)
                    return;

                JArray anims = (JArray)message.DataObject["anims"];
                if (anims == null)
                    return;

                foreach (JObject anim in anims)
                {
                    string msg = anim?["msg"]?.ToString()?.ToLower();
                    if (string.IsNullOrEmpty(msg))
                        continue;

                    // Only care about Gramiel's defense shattering message, like Maid.
                    if (!msg.Contains("defense shattering"))
                        continue;

                    // Extract monId from tInf (e.g. "id:2" / "id:3") just like Maid.
                    int monId = 0;
                    string tInf = anim?["tInf"]?.ToString();
                    if (!string.IsNullOrEmpty(tInf))
                    {
                        string[] parts = tInf.Split(':');
                        if (parts.Length > 1)
                            int.TryParse(parts[1], out monId);
                    }

                    // Prefer to use monId when available, but do not *require* it.
                    // If monId is 0 (missing), still count the shatter so left-side
                    // (P1/P2) and right-side (P3/P4) clients can alternate locally.
                    if (monId != 0)
                    {
                        switch (_preset)
                        {
                            // Left-side presets expect crystal id 2.
                            case GramielPreset.P1:
                            case GramielPreset.P2:
                                if (monId != 2)
                                    continue;
                                break;

                            // Right-side presets expect crystal id 3.
                            case GramielPreset.P3:
                            case GramielPreset.P4:
                                if (monId != 3)
                                    continue;
                                break;
                        }
                    }

                    _defenseShatteringCount++;

                    // Decide if this preset should act on this shatter, using the same
                    // odd/even rule as Maid's ultraBossHandler.
                    bool act;
                    switch (_preset)
                    {
                        // Gramiel L1 / Gramiel R1: odd counts.
                        case GramielPreset.P1:
                        case GramielPreset.P3:
                            act = (_defenseShatteringCount % 2 != 0);
                            break;

                        // Gramiel L2 / Gramiel R2: even counts (and > 0).
                        case GramielPreset.P2:
                        case GramielPreset.P4:
                            act = (_defenseShatteringCount % 2 == 0 && _defenseShatteringCount > 0);
                            break;

                        default:
                            act = false;
                            break;
                    }

                    if (!act)
                        continue;

                    // Decide our orb side based on preset.
                    string orbTarget = null;
                    switch (_preset)
                    {
                        case GramielPreset.P1:
                        case GramielPreset.P2:
                            orbTarget = "id:2";
                            break;
                        case GramielPreset.P3:
                        case GramielPreset.P4:
                            orbTarget = "id:3";
                            break;
                    }

                    bool hasOrb = !string.IsNullOrEmpty(orbTarget) && World.IsMonsterAvailable(orbTarget);
                    bool hasGramiel = World.IsMonsterAvailable("Gramiel");
                    bool hasGrace = World.IsMonsterAvailable("Grace Crystal");

                    // When it's this preset's defense shattering turn, taunt our side's
                    // crystal if it exists; otherwise fall back to Gramiel when safe.
                    // Use a strong cast sequence similar to Maid's Special Anims:
                    // wait for cooldown, then cast the taunt skill twice, and retry
                    // until the Focus target aura is applied or attempts are exhausted.
                    if (hasOrb)
                    {
                        Player.AttackMonster(orbTarget);
                        Task.Run(async () =>
                        {
                            try
                            {
                                for (int attempt = 0; attempt < 3; attempt++)
                                {
                                    // If target already has Focus, stop retrying.
                                    if (Player.GetAuras(false, "Focus") > 0)
                                        break;

                                    int wait = Player.SkillAvailable("5");
                                    if (wait > 0)
                                        await Task.Delay(wait);

                                    Player.ForceUseSkill("5");
                                    await Task.Delay(500);
                                    Player.UseSkill("5");

                                    // Short delay before re-checking Focus.
                                    await Task.Delay(300);
                                }
                            }
                            catch { }
                        });
                    }
                    else if (hasGramiel && !hasGrace)
                    {
                        if (Player.GetAuras(true, "Vendetta") == 0)
                        {
                            Player.AttackMonster("Gramiel");
                            Task.Run(async () =>
                            {
                                try
                                {
                                    for (int attempt = 0; attempt < 3; attempt++)
                                    {
                                        if (Player.GetAuras(false, "Focus") > 0)
                                            break;

                                        int wait = Player.SkillAvailable("5");
                                        if (wait > 0)
                                            await Task.Delay(wait);

                                        Player.ForceUseSkill("5");
                                        await Task.Delay(500);
                                        Player.UseSkill("5");

                                        await Task.Delay(300);
                                    }
                                }
                                catch { }
                            });
                        }
                    }
                }
            }
            catch
            {
                // Swallow errors to avoid breaking networking pipeline.
            }
        }

        // Legacy shatter helper is no longer used; the defense shattering logic
        // is implemented inline in Handle based on _defenseShatteringCount.
        private bool ShouldActOnShattering()
        {
            return true;
        }

        private void StartTauntLoop()
        {
            StopTauntLoop();
            _cts = new CancellationTokenSource();
            _tauntTask = Task.Run(() => TauntLoop(_cts.Token));
        }

        private async Task TauntLoop(CancellationToken token)
        {
            // These values mirror Maid's Gramiel taunt cycle behaviour:
            // - cycle: number of slots in the taunt cycle (party roles)
            // - second: total duration of one full cycle in seconds
            const int cycle = 4;
            const int second = 20;

            // NOTE: The original Maid-style per-preset taunt cycle is no longer used
            // for Gramiel. Instead, each account will spam taunt on Gramiel until it
            // obtains the Vendetta aura, then stop taunting.
            int count = (int)_preset;

            // Side-specific orb identifiers. The main taunt logic for orbs happens
            // reactively in Handle(...). Here we keep track of both "our" orb and
            // the opposite orb so that, once our side's crystal is dead, we can
            // automatically help kill the remaining crystal.
            string orbTarget = null;
            string otherOrbTarget = null;
            switch (_preset)
            {
                case GramielPreset.P1:
                case GramielPreset.P2:
                    orbTarget = "id:2";
                    otherOrbTarget = "id:3";
                    break;
                case GramielPreset.P3:
                case GramielPreset.P4:
                    orbTarget = "id:3";
                    otherOrbTarget = "id:2";
                    break;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Allow any instance variant of the map, e.g. "ultragramiel-1", "ultragramiel-1234".
                    // Only require that the current map name contains "ultragramiel" (case-insensitive).
                    if (!Player.IsLoggedIn ||
                        string.IsNullOrEmpty(Player.Map) ||
                        Player.Map.IndexOf("ultragramiel", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        await Task.Delay(2000, token);
                        continue;
                    }

                    bool hasGramiel = World.IsMonsterAvailable("Gramiel");
                    bool hasGrace = World.IsMonsterAvailable("Grace Crystal");

                    bool hasOurOrb = !string.IsNullOrEmpty(orbTarget) && World.IsMonsterAvailable(orbTarget);
                    bool hasOtherOrb = !string.IsNullOrEmpty(otherOrbTarget) && World.IsMonsterAvailable(otherOrbTarget);

                    // If our crystal is dead but the opposite crystal is still alive,
                    // help kill the remaining crystal before focusing Gramiel.
                    if (!hasOurOrb && hasOtherOrb)
                    {
                        Player.AttackMonster(otherOrbTarget);
                        await Task.Delay(1000, token);
                        continue;
                    }

                    // If Gramiel isn't present, nothing to do.
                    if (!hasGramiel)
                    {
                        await Task.Delay(2000, token);
                        continue;
                    }

                    // Do not taunt Gramiel while Grace Crystals are present.
                    if (hasGrace)
                    {
                        await Task.Delay(1000, token);
                        continue;
                    }

                    // New behaviour: spam taunt on Gramiel until this account has
                    // Vendetta, then stop taunting. All accounts can run this logic
                    // concurrently; each will naturally stop once it has the aura.
                    if (Player.GetAuras(true, "Vendetta") == 0)
                    {
                        Player.AttackMonster("Gramiel");

                        // Strong taunt sequence with cooldown wait, similar to the
                        // defence-shattering taunt logic.
                        Task.Run(async () =>
                        {
                            try
                            {
                                for (int attempt = 0; attempt < 3; attempt++)
                                {
                                    if (Player.GetAuras(true, "Vendetta") > 0)
                                        break;

                                    int wait = Player.SkillAvailable("5");
                                    if (wait > 0)
                                        await Task.Delay(wait);

                                    Player.ForceUseSkill("5");
                                    await Task.Delay(500);
                                    Player.UseSkill("5");

                                    await Task.Delay(300);
                                }
                            }
                            catch { }
                        });
                    }

                    int delayMs = (int)(second / (double)cycle * 1000);
                    await Task.Delay(delayMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch
                {
                    // Swallow any unexpected errors to avoid crashing the proxy
                    // thread; the loop will retry on the next tick.
                }
            }
        }

        private void StopTauntLoop()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch
                {
                }
                finally
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }

            _tauntTask = null;
        }

        public void Dispose()
        {
            StopTauntLoop();
        }
    }
}
