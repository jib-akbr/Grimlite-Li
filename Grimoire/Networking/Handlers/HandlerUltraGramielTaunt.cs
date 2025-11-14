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
        private int _crystalCount;
        // Set to true when a matching "shattering" event occurs and this preset
        // should act. Consumed once by the taunt loop to perform a single orb taunt.
        private bool _pendingOrbTaunt;

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

                    // Only care about the Gramiel crystal shattering message.
                    if (!msg.Contains("shattering"))
                        continue;

                    _crystalCount++;

                    bool act = ShouldActOnShattering();
                    if (!act)
                        continue;

                    // Mark that on the next loop tick we should perform a single orb
                    // taunt for this preset. The taunt loop will resolve the correct
                    // side and consume this flag.
                    _pendingOrbTaunt = true;
                }
            }
            catch
            {
                // Swallow errors to avoid breaking networking pipeline.
            }
        }

        private bool ShouldActOnShattering()
        {
            // Reproduce the Gramiel L1/L2/R1/R2 pattern from Maid's
            // ultraBossHandler:
            //
            // - L1/R1: act when crystalCount % 4 == 2
            // - L2/R2: act when crystalCount % 4 == 0

            int mod = _crystalCount % 4;
            switch (_preset)
            {
                case GramielPreset.P1: // Gramiel L1
                case GramielPreset.P3: // Gramiel R1
                    return mod == 2;

                case GramielPreset.P2: // Gramiel L2
                case GramielPreset.P4: // Gramiel R2
                    return mod == 0;

                default:
                    return false;
            }
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

            // Each preset gets a different initial offset in the cycle so that
            // P1–P4 are evenly spaced.
            int count = (int)_preset;

            // Side-specific orb priority, based on Maid's attack priority:
            // - Left side:  id.2
            // - Right side: id.3
            string orbTarget = null;
            switch (_preset)
            {
                case GramielPreset.P1:
                case GramielPreset.P2:
                    orbTarget = "id.2";
                    break;
                case GramielPreset.P3:
                case GramielPreset.P4:
                    orbTarget = "id.3";
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
                    bool hasOrb = !string.IsNullOrEmpty(orbTarget) && World.IsMonsterAvailable(orbTarget);

                    // Maid-style attack priority:
                    // 1) While our side's orb exists (id.2/id.3), stay on that orb.
                    // 2) Only fall back to Gramiel when no orb is available.
                    if (hasOrb)
                    {
                        Player.AttackMonster(orbTarget);

                        // If we have a pending orb taunt from a recent "shattering" event,
                        // consume it here with a single skill 5 cast.
                        if (_pendingOrbTaunt)
                        {
                            _pendingOrbTaunt = false;
                            Player.ForceUseSkill("5");
                        }

                        await Task.Delay(1000, token);
                        continue;
                    }

                    // No orb available. If Gramiel isn't present, nothing to do.
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

                    // Maid-style taunt cycle: when count hits 0, force a taunt
                    // on Gramiel, then reset the counter for the next cycle.
                    if (count <= 0)
                    {
                        count = cycle;

                        // Respect Vendetta aura: once we have it, stop taunting
                        // Gramiel so other accounts can obtain the aura.
                        if (Player.GetAuras(true, "Vendetta") == 0)
                        {
                            // Target Gramiel and cast taunt skill 5.
                            Player.AttackMonster("Gramiel");
                            Player.ForceUseSkill("5");
                        }
                    }
                    else
                    {
                        count--;
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
