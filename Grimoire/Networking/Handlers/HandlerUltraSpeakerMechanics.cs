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

        private int _speakerCounter = -1;

        // Track death so we can reposition on respawn.
        private bool _wasDead = false;

        // Track zone position for continuous positioning (like decompiled plugin)
        private bool _inZone = false;

        private bool _forceSkill = false;
        private string _skillToForce = "";
        private int _forceDelay = 0;

        // Coordinates for UltraSpeaker mechanics
        private const string DEFAULT_X = "100";   // Starting position (_inZone = false)
        private const string DEFAULT_Y = "321";

        private const string ZONE_OUT_X = "203";  // Zone position when active (_inZone = true)
        private const string ZONE_OUT_Y = "301";

        private bool _isRunning = true;

        public HandlerUltraSpeakerMechanics(SpeakerRole role)
        {
            _role = role;

            // On handler start, move to the starting position
            if (Player.IsLoggedIn && Player.IsAlive && Player.Map.Equals("ultraspeaker", StringComparison.OrdinalIgnoreCase) && Player.Cell.Equals("Boss", StringComparison.OrdinalIgnoreCase))
            {
                Player.WalkToPoint("100", "321");
            }
            else if (Player.IsLoggedIn && !Player.IsAlive)
            {
                _wasDead = true;
            }

            // Start continuous positioning and healing loop
            Task.Run(async () => await ContinuousLoop());
        }

        private async Task ContinuousLoop()
        {
            while (_isRunning)
            {
                try
                {
                    if (!Player.IsLoggedIn)
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    // Handle death/respawn repositioning
                    if (!Player.IsAlive)
                    {
                        if (!_wasDead)
                        {
                            Grimoire.UI.LogForm.Instance.AppendDebug($"💀💀💀 {_role} DIED 💀💀💀");
                        }
                        _wasDead = true;
                        await Task.Delay(100);
                        continue;
                    }

                    if (_wasDead && Player.IsAlive)
                    {
                        Grimoire.UI.LogForm.Instance.AppendDebug($"♻️ {_role} RESPAWNED - Repositioning to start");
                        _wasDead = false;
                        Player.WalkToPoint("100", "321");
                    }

                    // Only process if in Boss cell
                    if (Player.Cell.Equals("Boss", StringComparison.OrdinalIgnoreCase))
                    {
                        // ================================
                        // AUTO-ATTACK MONSTER
                        // ================================
                        if (World.IsMonsterAvailable("*") && !Player.HasTarget)
                        {
                            Player.AttackMonster("*");
                        }

                        // ================================
                        // HEAL LOW HEALTH PLAYERS
                        // ================================
                        if (_role == SpeakerRole.AP || _role == SpeakerRole.LOO)
                        {
                            foreach (string player in World.PlayersInMap)
                            {
                                if (World.GetPlayerHealthPercentage(player) < 70)
                                {
                                    Player.UseSkill("2");
                                    break; // Only heal once per cycle
                                }
                            }
                        }
                        else if (_role == SpeakerRole.LR)
                        {
                            foreach (string player in World.PlayersInMap)
                            {
                                if (World.GetPlayerHealthPercentage(player) < 70)
                                {
                                    Player.UseSkill("3");
                                    break; // Only heal once per cycle
                                }
                            }
                        }

                        // ================================
                        // CONTINUOUS ZONE POSITIONING
                        // ================================
                        if (_inZone)
                        {
                            if (Player.Position[0] != ZONE_OUT_X)
                                Player.WalkToPoint(ZONE_OUT_X, ZONE_OUT_Y);
                        }
                        else
                        {
                            if (Player.Position[0] != DEFAULT_X)
                                Player.WalkToPoint(DEFAULT_X, DEFAULT_Y);
                        }
                    }
                }
                catch { }

                await Task.Delay(50); // Run every 50ms
            }
        }

        public async void Handle(JsonMessage message)
        {
            try
            {
                if (!Player.IsLoggedIn)
                    return;

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

                // ==================================================
                // ROTATION LOGIC (on truth/listen messages)
                // ==================================================
                if (isTruth || isListen)
                {
                    _speakerCounter++;

                    var step = WhatAction(_speakerCounter);
                    var zoneRole = StringToRole(step.zoneClass);
                    var skillRole = StringToRole(step.tauntClass);

                    // Debug logging (only when this role is involved)
                    if ((skillRole.HasValue && skillRole.Value == _role) || 
                        (zoneRole.HasValue && zoneRole.Value == _role))
                    {
                        Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] Counter={_speakerCounter} | Taunt={step.tauntClass} | Zone={step.zoneClass} | Type={step.zoneType} | Delay={step.delay}");
                    }

                    // =========================
                    // SET ZONE FLAG 
                    // =========================
                    if (zoneRole.HasValue && zoneRole.Value == _role)
                    {
                        if (step.zoneType == "IN")
                        {
                            _inZone = true;   // Will move to (262, 255)
                            Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] Moving to zone position");
                        }
                        else if (step.zoneType == "OUT")
                        {
                            _inZone = false;  // Will return to (103, 272)
                            Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] Returning to start position");
                        }
                    }
                    
            // AP uses skill 2 after every zone (whenever OUT appears) - multiple attempts for reliability
            if (step.zoneType == "OUT" && _role == SpeakerRole.AP)
            {
                Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker AP] Using skill 2 after zone completion");
                await Task.Delay(300);
                Player.UseSkill("2");
                await Task.Delay(100);
                Player.UseSkill("2");
                await Task.Delay(100);
                Player.UseSkill("2");
            }
            
            // TAUNT (skill 5) based on rotation
            // =========================
            if (skillRole.HasValue && skillRole.Value == _role)
            {
                SetForceSkill("5", step.delay);
            }

            // LR spams skill 1 on truth/listen
            if (_role == SpeakerRole.LR)
            {
                Player.UseSkill("1");
                Player.UseSkill("1");
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
        private (string tauntClass, string zoneClass, string zoneType, int delay) WhatAction(int count)
        {
            switch (count)
            {
                case 0:  return ("LR",  null,  null,  0);
                case 1:  return ("LOO", "DPS", "IN",  0);
                case 2:  return ("AP",  "DPS", "OUT", 0);
                case 3:  return ("LOO", null,  null,  500);
                case 4:  return (null,  "LR",  "IN",  0);
                case 5:  return ("LR",  "LR",  "OUT", 500);
                case 6:  return (null,  null,  null,  0);
                case 7:  return ("LOO", null,  null,  500);
                case 8:  return (null,  "AP",  "IN",  0);
                case 9:  return ("AP",  "AP",  "OUT", 0);
                case 10: return ("LR",  null,  null,  500);
                case 11: return (null,  "LOO", "IN",  0);
                case 12: return ("LOO", "LOO", "OUT", 500);
                case 13: return (null,  null,  null,  0);
                case 14: return ("LR",  null,  null,  0);
                case 15: 
                    _speakerCounter = 1;
                    return ("LOO", "DPS", "IN",  500);
                default: return (null,  null,  null,  0);
            }
        }

        // ====================================
        // STRING TO ROLE CONVERTER
        // ====================================
        private SpeakerRole? StringToRole(string roleStr)
        {
            if (roleStr == null) return null;
            switch (roleStr)
            {
                case "DPS": return SpeakerRole.DPS;
                case "LR":  return SpeakerRole.LR;
                case "LOO": return SpeakerRole.LOO;
                case "AP":  return SpeakerRole.AP;
                default: return null;
            }
        }

        // ====================================
        // FORCE SKILL (TAUNTER AGGRESSIVE SPAM UNTIL SUCCESS)
        // ====================================
        private void SetForceSkill(string skill, int delay = 0)
        {
            // Prevent overlapping taunt attempts
            if (_forceSkill)
            {
                Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] ⚠️ TAUNT ALREADY IN PROGRESS - IGNORING NEW REQUEST");
                return;
            }

            // Pause bot script to prevent skill command interference
            if (Grimoire.UI.BotManager.Instance?.ActiveBotEngine != null)
            {
                Grimoire.UI.BotManager.Instance.ActiveBotEngine.paused = true;
            }

            _forceSkill = true;
            _forceDelay = delay;
            _skillToForce = skill;

            Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] 🎯 TAUNT REQUESTED - Delay: {delay}ms | Player Alive: {Player.IsAlive} | In Boss Cell: {Player.Cell}");

            Task.Run(async () =>
            {
                try
                {
                    // APPLY THE DELAY FIRST (critical for boss timing sync!)
                    if (_forceDelay > 0)
                    {
                        Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] ⏳ Waiting {_forceDelay}ms before taunt...");
                        await Task.Delay(_forceDelay);
                    }
                    
                    // Wait for skill to be available BEFORE starting spam
                    int skillCooldown = Player.SkillAvailable("5");
                    if (skillCooldown > 0)
                    {
                        Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] ⏱️ Skill 5 on cooldown - waiting {skillCooldown}ms");
                        await Task.Delay(skillCooldown);
                    }
                    
                    Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] 💥 Starting aggressive taunt spam");
                    
                    int attempts = 0;
                    int maxAttempts = 50;
                    
                    while (attempts < maxAttempts)
                    {
                        // Check if player is still alive and logged in
                        if (!Player.IsLoggedIn || !Player.IsAlive)
                        {
                            Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] ❌ TAUNT ABORTED - Player dead or logged out");
                            break;
                        }

                        // Spam skill 5 rapidly
                        Player.UseSkill("5");
                        Player.UseSkill("5");
                        Player.UseSkill("5");
                        
                        await Task.Delay(30);
                        
                        // Check if taunt succeeded
                        int focusStacks = Player.GetAuras(false, "Focus");
                        if (focusStacks > 0)
                        {
                            Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] ✅ TAUNT SUCCESS after {attempts} attempts - Focus stacks: {focusStacks}");
                            break;
                        }
                        
                        attempts++;
                        
                        // Log every 10 attempts to track progress
                        if (attempts % 10 == 0)
                        {
                            Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] 🔄 Still trying... Attempt {attempts}/{maxAttempts}");
                        }
                    }
                    
                    if (attempts >= maxAttempts)
                    {
                        Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] ⛔ TAUNT FAILED after {maxAttempts} attempts!");
                    }
                }
                catch (Exception ex)
                {
                    Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] 💀 TAUNT EXCEPTION: {ex.Message}");
                }
                finally
                {
                    _forceSkill = false;
                    
                    // Resume bot script
                    if (Grimoire.UI.BotManager.Instance?.ActiveBotEngine != null)
                    {
                        Grimoire.UI.BotManager.Instance.ActiveBotEngine.paused = false;
                    }
                    
                    Grimoire.UI.LogForm.Instance.AppendDebug($"[UltraSpeaker {_role}] 🔓 Taunt flag cleared - Script resumed");
                }
            });
        }

        public void Dispose()
        {
            _forceSkill = false;
            _isRunning = false;
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
