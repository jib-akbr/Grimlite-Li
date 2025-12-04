using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grimoire.Game;
using Newtonsoft.Json.Linq;

namespace Grimoire.Networking.Handlers
{
    public class HandlerUltraSpeakerMechanics : IJsonMessageHandler, IDisposable
    {
        public enum SpeakerRole
        {
            DPS,
            LR,
            LOO,
            AP
        }

        private readonly SpeakerRole _role;

        public string[] HandledCommands { get; } = { "ct" };

        private int _truthCount = 0;
        private int _listenCount = 0;
        private int _equalCount = 0;

        private int _speakerCounter = 0;

        // Track death so we can reposition on respawn.
        private bool _wasDead = false;

        private bool _forceSkill = false;
        private string _skillToForce = "";
        private int _forceDelay = 0;

        // IN / OUT points for UltraSpeaker fight
        // IN should be the circle at (100, 321), OUT the other spot at (203, 301)
        private const string IN_X = "669";
        private const string IN_Y = "259";

        private const string OUT_X = "100";
        private const string OUT_Y = "321";

        public HandlerUltraSpeakerMechanics(SpeakerRole role)
        {
            _role = role;

            // On handler start, move to the safe edge position so we don't
            // interfere with existing zones.
            if (Player.IsLoggedIn && Player.IsAlive && Player.Map.Equals("ultraspeaker", StringComparison.OrdinalIgnoreCase) && Player.Cell.Equals("Boss", StringComparison.OrdinalIgnoreCase))
            {
                Player.WalkToPoint("893", "257");
            }
            else if (Player.IsLoggedIn && !Player.IsAlive)
            {
                _wasDead = true;
            }
        }

        public async void Handle(JsonMessage message)
        {
            try
            {
                // Handle death/respawn repositioning first.
                if (!Player.IsLoggedIn)
                    return;

                if (!Player.IsAlive)
                {
                    _wasDead = true;
                    return;
                }

                if (_wasDead && Player.IsAlive)
                {
                    _wasDead = false;
                    // After respawn, go back to the safe edge position.
                    Player.WalkToPoint("893", "257");
                }

                var animsToken = message?.DataObject?["anims"];
                if (!(animsToken is JArray anims) || anims.Count == 0)
                    return;

                string customMessage = anims[0]["msg"]?.ToString();
                if (string.IsNullOrEmpty(customMessage))
                    return;

                string msgLower = customMessage.ToLowerInvariant();
                bool isTruth = msgLower.Contains("truth");
                bool isListen = msgLower.Contains("listen");
                bool isEqual = msgLower.Contains("equal");

                // ================================
                // BASE ULTRASPEAKER SPEECH MOVES
                // ================================
                switch (customMessage)
                {
                    case "I will make you see the truth.":
                        Player.WalkToPoint("893", "257");
                        _truthCount++;
                        break;

                    case "You shall listen.":
                        Player.WalkToPoint("893", "257");
                        _listenCount++;
                        break;

                    case "All stand equal beneath the eyes of the Eternal.":
                        _equalCount++;
                        break;

                    default:
                        Player.WalkToPoint("893", "257");
                        break;
                }

                // ==================================================
                // TAUNT + RED-ZONE CYCLE (full 20-step chart)
                // ==================================================
                if (isTruth || isListen || isEqual)
                {
                    _speakerCounter++;
                    if (_speakerCounter > 20)
                        _speakerCounter = 3; // after first full cycle, repeat from step 3

                    var step = WhatAction(_speakerCounter);

                    // =========================
                    // RED ZONE (circle) on Equal steps only
                    // =========================
                    if (isEqual && step.ZoneRole.HasValue && step.ZoneRole.Value == _role)
                    {
                        // Move into the red-zone circle.
                        Player.WalkToPoint(IN_X, IN_Y);

                        // When LR is assigned the red zone (its Equal), spam skill 1.
                        if (_role == SpeakerRole.LR)
                        {
                            for (int i = 0; i < 25; i++)
                            {
                                Player.UseSkill("1");
                                await Task.Delay(100);
                            }
                        }
                    }

                    // =========================
                    // TAUNT (skill 5) on Truth/Listen steps ONLY
                    // =========================
                    if ((isTruth || isListen) && step.SkillRole.HasValue && step.SkillRole.Value == _role)
                    {
                        SetForceSkill("5", step.DelayMs);
                    }
                }
            }
            catch
            {
            }
        }

        // ==========================================
        // THE REAL MECHANIC TABLE (EXACT TAUNTER)
        // ==========================================
        private (SpeakerRole? SkillRole, SpeakerRole? ZoneRole, string Zone, int DelayMs) WhatAction(int count)
        {
            // Mapping based on the 20-step chart provided:
            // 1)  Truth  – Taunt: LR       – Red zone: none
            // 2)  Listen – Taunt: LOO      – Red zone: none
            // 3)  Equal  – Taunt: none     – Red zone: DPS
            // 4)  Truth  – Taunt: AP       – Red zone: none
            // 5)  Listen – Taunt: LOO      – Red zone: none
            // 6)  Truth  – Taunt: LOO      – Red zone: none
            // 7)  Equal  – Taunt: none     – Red zone: LR
            // 8)  Listen – Taunt: LR       – Red zone: none
            // 9)  Truth  – Taunt: LR       – Red zone: none
            // 10) Truth – Taunt: LOO      – Red zone: none
            // 11) Listen– Taunt: LOO      – Red zone: none
            // 12) Equal – Taunt: none     – Red zone: AP
            // 13) Truth – Taunt: AP       – Red zone: none
            // 14) Listen– Taunt: LR       – Red zone: none
            // 15) Truth – Taunt: LR       – Red zone: none
            // 16) Equal – Taunt: none     – Red zone: LOO
            // 17) Listen– Taunt: LOO      – Red zone: none
            // 18) Truth – Taunt: LOO      – Red zone: none
            // 19) Truth – Taunt: LR       – Red zone: none
            // 20) Listen– Taunt: LOO      – Red zone: none
            // → then repeat from step 3 (handled in Handle by resetting counter to 3).

            switch (count)
            {
                case 1:  return (SpeakerRole.LR,  null,           null, 0);
                case 2:  return (SpeakerRole.LOO, null,           null, 0);
                case 3:  return (null,            SpeakerRole.DPS,"IN", 0);
                case 4:  return (SpeakerRole.AP,  null,           null, 0);
                case 5:  return (SpeakerRole.LOO, null,           null, 0);
                case 6:  return (SpeakerRole.LOO, null,           null, 0);
                case 7:  return (null,            SpeakerRole.LR, "IN", 0);
                case 8:  return (SpeakerRole.LR,  null,           null, 0);
                case 9:  return (SpeakerRole.LR,  null,           null, 0);
                case 10: return (SpeakerRole.LOO, null,           null, 0);
                case 11: return (SpeakerRole.LOO, null,           null, 0);
                case 12: return (null,            SpeakerRole.AP, "IN", 0);
                case 13: return (SpeakerRole.AP,  null,           null, 0);
                case 14: return (SpeakerRole.LR,  null,           null, 0);
                case 15: return (SpeakerRole.LR,  null,           null, 0);
                case 16: return (null,            SpeakerRole.LOO,"IN", 0);
                case 17: return (SpeakerRole.LOO, null,           null, 0);
                case 18: return (SpeakerRole.LOO, null,           null, 0);
                case 19: return (SpeakerRole.LR,  null,           null, 0);
                case 20: return (SpeakerRole.LOO, null,           null, 0);
                default: return (null, null, null, 0);
            }
        }

        // ====================================
        // FORCE SKILL (TAUNTER DOUBLE CAST)
        // ====================================
        private void SetForceSkill(string skill, int delay = 0)
        {
            _forceSkill = true;
            _forceDelay = delay;
            _skillToForce = skill;

            Task.Run(async () =>
            {
                try
                {
                    if (_forceDelay > 0)
                        await Task.Delay(_forceDelay);

                    int wait = Player.SkillAvailable(_skillToForce);
                    if (wait > 0)
                        await Task.Delay(wait);

                    Player.UseSkill(_skillToForce);
                    Player.UseSkill(_skillToForce);
                }
                catch { }
                finally
                {
                    _forceSkill = false;
                }
            });
        }

        public void Dispose()
        {
            _forceSkill = false;
        }
    }

    // Role-specific wrappers
    public class HandlerUltraSpeakerDPS : HandlerUltraSpeakerMechanics
    {
        public HandlerUltraSpeakerDPS() : base(SpeakerRole.DPS) { }
    }
    public class HandlerUltraSpeakerLR : HandlerUltraSpeakerMechanics
    {
        public HandlerUltraSpeakerLR() : base(SpeakerRole.LR) { }
    }
    public class HandlerUltraSpeakerLOO : HandlerUltraSpeakerMechanics
    {
        public HandlerUltraSpeakerLOO() : base(SpeakerRole.LOO) { }
    }
    public class HandlerUltraSpeakerAP : HandlerUltraSpeakerMechanics
    {
        public HandlerUltraSpeakerAP() : base(SpeakerRole.AP) { }
    }
}
