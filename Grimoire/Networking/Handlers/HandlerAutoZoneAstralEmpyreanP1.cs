using System;
using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.UI;
using Newtonsoft.Json.Linq;

namespace Grimoire.Networking.Handlers
{
    /// <summary>
    /// Astral Empyrean auto-zone handler with alternating taunts triggered by
    /// boss text "Behold our starfire!". P1 taunts on odd counts (1,3,5...), P2 on even (2,4,6...).
    /// Shared static counter ensures proper alternation across all handler instances.
    /// Uses strong taunt sequence similar to Gramiel handler.
    /// </summary>
    public abstract class HandlerAutoZoneAstralEmpyreanBase : IJsonMessageHandler, IDisposable
    {
        // SHARED counter across all instances - incremented once per starfire
        private static int _sharedStarfireCount = 0;
        private static readonly object _lock = new object();

        private readonly int _offset; // 0 = P1 (odd), 1 = P2 (even)
        private readonly string _presetName; // "P1" or "P2" for debug

        protected HandlerAutoZoneAstralEmpyreanBase(int offset, string presetName)
        {
            _offset = offset;
            _presetName = presetName;
        }

        // Listen to both zone updates (event) and animation/chat text (ct).
        public string[] HandledCommands { get; } = { "event", "ct" };

        public void Handle(JsonMessage message)
        {
            try
            {
                switch (message.Command)
                {
                    case "event":
                        HandleZone(message);
                        break;
                    case "ct":
                        HandleStarfire(message);
                        break;
                }
            }
            catch
            {
                // Swallow to avoid breaking the proxy pipeline.
            }
        }

        private void HandleZone(JsonMessage message)
        {
            try
            {
                JObject args = (JObject)message.DataObject["args"];
                string zone = args?["zoneSet"]?.ToString();
                
                switch (zone)
                {
                    case "A":
                        Player.WalkToPoint("708", "447");
                        break;
                    case "B":
                        Player.WalkToPoint("287", "191");
                        break;
                    default:
                        Player.WalkToPoint("461", "329");
                        break;
                }
            }
            catch
            {
            }
        }

        private void HandleStarfire(JsonMessage message)
        {
            try
            {
                if (message?.DataObject?["anims"] == null)
                    return;

                JArray anims = (JArray)message.DataObject["anims"];
                if (anims == null)
                    return;

                bool foundStarfire = false;
                foreach (JObject anim in anims)
                {
                    string msg = anim?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(msg))
                        continue;
                    
                    // Check for starfire message (case-insensitive)
                    if (msg.IndexOf("behold our starfire", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundStarfire = true;
                        break;
                    }
                }

                if (!foundStarfire)
                    return;

                // Increment shared counter and check parity
                int currentCount;
                bool shouldAct;
                lock (_lock)
                {
                    _sharedStarfireCount++;
                    currentCount = _sharedStarfireCount;
                    // P1 (offset=0): acts on odd counts (1,3,5...)
                    // P2 (offset=1): acts on even counts (2,4,6...)
                    shouldAct = (_offset == 0) ? (currentCount % 2 == 1) : (currentCount % 2 == 0);
                }

                // Debug log
                LogForm.Instance.AppendDebug($"[Astral Empyrean {_presetName}] Starfire #{currentCount} - {(shouldAct ? "TAUNTING" : "skipping")}");

                if (!shouldAct)
                    return;

                // Check if Astral Empyrean is available before taunting
                if (!World.IsMonsterAvailable("Astral Empyrean"))
                    return;

                // Attack and trigger strong taunt sequence (Gramiel-style)
                Player.AttackMonster("Astral Empyrean");
                Task.Run(async () =>
                {
                    try
                    {
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            // If target already has Focus, stop retrying
                            if (Player.GetAuras(false, "Focus") > 0)
                                break;

                            if (!Player.IsLoggedIn)
                                break;

                            int wait = Player.SkillAvailable("5");
                            if (wait > 0)
                                await Task.Delay(wait);

                            Player.ForceUseSkill("5");
                            await Task.Delay(500);
                            Player.UseSkill("5");

                            // Short delay before re-checking Focus
                            await Task.Delay(300);
                        }
                    }
                    catch
                    {
                    }
                });
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            // No retained resources; taunt actions are fire-and-forget per message.
        }
    }

    public class HandlerAutoZoneAstralEmpyreanP1 : HandlerAutoZoneAstralEmpyreanBase
    {
        // P1 taunts on odd starfire counts (1,3,5...)
        public HandlerAutoZoneAstralEmpyreanP1() : base(0, "P1") { }
    }

    public class HandlerAutoZoneAstralEmpyreanP2 : HandlerAutoZoneAstralEmpyreanBase
    {
        // P2 taunts on even starfire counts (2,4,6...)
        public HandlerAutoZoneAstralEmpyreanP2() : base(1, "P2") { }
    }
}