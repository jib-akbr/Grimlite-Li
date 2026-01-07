using Grimoire.Game;
using Grimoire.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdBalanceHP : StatementCommand, IBotCommand
    {
        // Track which threshold we're on (0, 1, 2...)
        private int _currentPhaseIndex = 0;
        
        // Track whether we're currently working on Boss2
        private bool _onSecondBoss = false;
        
        // Cached thresholds
        private int[] _thresholds = new int[0];
        
        // Track last logged HP to only log when crossing 10% boundaries
        private int _lastLoggedHP = -1;
        
        // Track if we've already logged the Boss2 not provided message
        private bool _boss2NotProvidedLogged = false;
        
        // Track if boss has died (for single boss mode)
        private bool _bossDied = false;

        public CmdBalanceHP()
        {
            Tag = "Monster";
            Text = "Balance HP";
            Description1 = "Boss 1 name (primary)";
            Description2 = "Boss 2 name (secondary, optional)";
            // Value3 = optional skill index to use when attacking
        }

        public override void OnBotStarted()
        {
            // Reset counters when bot starts
            _currentPhaseIndex = 0;
            _onSecondBoss = false;
            _thresholds = new int[0];
            _lastLoggedHP = -1;
            _boss2NotProvidedLogged = false;
            _bossDied = false;
        }

        public override void OnBotStopped()
        {
            // Reset counters when bot stops
            _currentPhaseIndex = 0;
            _onSecondBoss = false;
            _thresholds = new int[0];
            _lastLoggedHP = -1;
            _boss2NotProvidedLogged = false;
            _bossDied = false;
        }

        public async Task Execute(IBotEngine instance)
        {
            // Value1 = Boss 1 name/monmapid (primary boss, can be comma-separated priorities)
            // Value2 = Boss 2 name/monmapid (secondary boss, can be comma-separated priorities, optional)
            // Value3 = optional skill index to use when attacking
            // Label = Comma-separated HP thresholds in descending order (e.g., "80,50,20")

            string boss1Raw = instance.ResolveVars(Value1);
            string boss2Raw = instance.ResolveVars(Value2);
            string thresholdStr = instance.ResolveVars(Label);
            
            // Parse optional skill index
            string resolvedSkillIndex = null;
            if (!string.IsNullOrWhiteSpace(Value3))
            {
                resolvedSkillIndex = instance.ResolveVars(Value3);
                if (string.IsNullOrWhiteSpace(resolvedSkillIndex))
                    resolvedSkillIndex = null;
            }

            // Parse priorities like CmdSpecialAnims does
            List<string> boss1Priorities = new List<string>();
            if (!string.IsNullOrWhiteSpace(boss1Raw))
            {
                if (boss1Raw.Contains(","))
                {
                    foreach (string p in boss1Raw.Split(','))
                    {
                        boss1Priorities.Add(p.Trim());
                    }
                }
                else
                {
                    boss1Priorities.Add(boss1Raw.Trim());
                }
            }

            List<string> boss2Priorities = new List<string>();
            if (!string.IsNullOrWhiteSpace(boss2Raw))
            {
                if (boss2Raw.Contains(","))
                {
                    foreach (string p in boss2Raw.Split(','))
                    {
                        boss2Priorities.Add(p.Trim());
                    }
                }
                else
                {
                    boss2Priorities.Add(boss2Raw.Trim());
                }
            }

            // Parse thresholds if not already done
            if (_thresholds.Length == 0 && !string.IsNullOrWhiteSpace(thresholdStr))
            {
                try
                {
                    _thresholds = thresholdStr.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t) && int.TryParse(t, out _))
                        .Select(int.Parse)
                        .OrderByDescending(t => t)
                        .ToArray();

                    if (_thresholds.Length > 0)
                    {
                        LogForm.Instance.AppendDebug($"[BalanceHP] Parsed thresholds: {string.Join(", ", _thresholds)}");
                    }
                }
                catch (Exception ex)
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] Error parsing thresholds '{thresholdStr}': {ex.Message}");
                    return;
                }
            }

            // If no valid boss1, log error and return
            if (boss1Priorities.Count == 0)
            {
                LogForm.Instance.AppendDebug($"[BalanceHP] Boss1 name not provided: '{boss1Raw}'");
                return;
            }

            // If Boss2 is not provided, handle single-boss mode
            if (boss2Priorities.Count == 0)
            {
                if (!_boss2NotProvidedLogged)
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] Boss2 not provided, attacking Boss1 only");
                    _boss2NotProvidedLogged = true;
                }
                
                // If no thresholds, just attack Boss1
                if (_thresholds.Length == 0)
                {
                    try
                    {
                        AttackFirstAvailable(boss1Priorities, resolvedSkillIndex);
                    }
                    catch (Exception ex)
                    {
                        LogForm.Instance.AppendDebug($"[BalanceHP] Error attacking Boss1: {ex.Message}");
                    }
                    return;
                }
                
                // Single boss with thresholds - check HP and trigger skill at thresholds
                try
                {
                    string targetBoss = null;
                    foreach (string p in boss1Priorities)
                    {
                        if (World.IsMonsterAvailable(p))
                        {
                            targetBoss = p;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(targetBoss))
                    {
                        if (!_bossDied)
                        {
                            LogForm.Instance.AppendDebug($"[BalanceHP] {boss1Priorities[0]} killed");
                            _bossDied = true;
                        }
                        return;
                    }

                    var availableMonsters = World.AvailableMonsters;
                    var targetMonster = availableMonsters.FirstOrDefault(m => m.Name == targetBoss || m.MonMapID.ToString() == targetBoss);
                    
                    if (targetMonster == null && targetBoss.Contains("."))
                    {
                        string[] parts = targetBoss.Split('.');
                        if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int extractedId))
                        {
                            targetMonster = availableMonsters.FirstOrDefault(m => m.MonMapID == extractedId);
                        }
                    }
                    
                    if (targetMonster == null)
                    {
                        LogForm.Instance.AppendDebug($"[BalanceHP] Could not get monster data for '{targetBoss}'");
                        return;
                    }

                    int currentHP = targetMonster.Health;
                    int maxHP = targetMonster.MaxHealth;

                    double hpPercent = maxHP > 0 
                        ? (currentHP / (double)maxHP) * 100.0 
                        : 100.0;

                    int roundedHP = (int)(hpPercent / 10) * 10;
                    bool hpChanged = roundedHP != _lastLoggedHP;
                    
                    if (hpChanged)
                    {
                        LogForm.Instance.AppendDebug($"[BalanceHP] Boss1 ({targetBoss}) HP: {hpPercent:F0}%");
                        _lastLoggedHP = roundedHP;
                    }

                    // Check if we've reached a threshold and haven't advanced yet
                    if (_currentPhaseIndex < _thresholds.Length && hpPercent <= _thresholds[_currentPhaseIndex])
                    {
                        _currentPhaseIndex++;
                        if (resolvedSkillIndex != null)
                        {
                            LogForm.Instance.AppendDebug($"[BalanceHP] Boss1 reached {_thresholds[_currentPhaseIndex - 1]}%, triggering skill {resolvedSkillIndex}");
                            _ = CastSkillImmediate(resolvedSkillIndex);
                        }
                        else
                        {
                            LogForm.Instance.AppendDebug($"[BalanceHP] Boss1 reached {_thresholds[_currentPhaseIndex - 1]}%");
                        }
                    }

                    // Attack without passing skill - skill is only used at thresholds
                    AttackWithSkill(targetBoss, null);
                }
                catch (Exception ex)
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] Error in single boss mode: {ex.Message}");
                }
                return;
            }

            // If no thresholds, just attack Boss1
            if (_thresholds.Length == 0)
            {
                LogForm.Instance.AppendDebug($"[BalanceHP] No valid thresholds provided, attacking Boss1");
                try
                {
                    AttackFirstAvailable(boss1Priorities, resolvedSkillIndex);
                }
                catch (Exception ex)
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] Error attacking Boss1: {ex.Message}");
                }
                return;
            }

            // If all thresholds are complete, just attack Boss1 forever
            if (_currentPhaseIndex >= _thresholds.Length)
            {
                try
                {
                    AttackFirstAvailable(boss1Priorities, resolvedSkillIndex);
                }
                catch (Exception ex)
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] Error attacking Boss1 (final phase): {ex.Message}");
                }
                return;
            }

            int currentThreshold = _thresholds[_currentPhaseIndex];
            List<string> targetPriorities = _onSecondBoss ? boss2Priorities : boss1Priorities;
            string targetBossLabel = _onSecondBoss ? "Boss2" : "Boss1";

            try
            {
                // Find the first available target
                string targetBoss = null;
                foreach (string p in targetPriorities)
                {
                    if (World.IsMonsterAvailable(p))
                    {
                        targetBoss = p;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(targetBoss))
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] {targetBossLabel} not available");
                    return;
                }

                // Find the monster in the available list to get current and max HP
                var availableMonsters = World.AvailableMonsters;
                var targetMonster = availableMonsters.FirstOrDefault(m => m.Name == targetBoss || m.MonMapID.ToString() == targetBoss);
                
                // If not found, try extracting ID from "id.X" format
                if (targetMonster == null && targetBoss.Contains("."))
                {
                    string[] parts = targetBoss.Split('.');
                    if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int extractedId))
                    {
                        targetMonster = availableMonsters.FirstOrDefault(m => m.MonMapID == extractedId);
                    }
                }
                
                if (targetMonster == null)
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] Could not get monster data for '{targetBoss}'");
                    return;
                }

                int currentHP = targetMonster.Health;
                int maxHP = targetMonster.MaxHealth;

                // Calculate HP percentage
                double hpPercent = maxHP > 0 
                    ? (currentHP / (double)maxHP) * 100.0 
                    : 100.0;

                // Only log when HP crosses a 10% boundary (100%, 90%, 80%, etc.)
                int roundedHP = (int)(hpPercent / 10) * 10;
                bool hpChanged = roundedHP != _lastLoggedHP;
                
                if (hpChanged)
                {
                    LogForm.Instance.AppendDebug($"[BalanceHP] Phase {_currentPhaseIndex}, Threshold {currentThreshold}%, {targetBossLabel} ({targetBoss}) HP: {hpPercent:F0}%");
                    _lastLoggedHP = roundedHP;
                }

                // Check if we've reached the threshold
                if (hpPercent <= currentThreshold)
                {
                    // Time to switch or advance
                    if (!_onSecondBoss)
                    {
                        // Boss1 reached threshold, switch to Boss2
                        _onSecondBoss = true;
                        AttackFirstAvailable(boss2Priorities, resolvedSkillIndex);
                        LogForm.Instance.AppendDebug($"[BalanceHP] Boss1 reached {currentThreshold}%, switching to Boss2");
                    }
                    else
                    {
                        // Boss2 reached threshold, advance phase and switch back to Boss1
                        _currentPhaseIndex++;
                        _onSecondBoss = false;
                        
                        if (_currentPhaseIndex >= _thresholds.Length)
                        {
                            // All thresholds complete
                            LogForm.Instance.AppendDebug($"[BalanceHP] All thresholds complete, attacking Boss1 permanently");
                            AttackFirstAvailable(boss1Priorities, resolvedSkillIndex);
                        }
                        else
                        {
                            // Continue with next threshold
                            LogForm.Instance.AppendDebug($"[BalanceHP] Boss2 reached {currentThreshold}%, advancing to phase {_currentPhaseIndex} (threshold {_thresholds[_currentPhaseIndex]}%), back to Boss1");
                            AttackFirstAvailable(boss1Priorities, resolvedSkillIndex);
                        }
                    }
                }
                else
                {
                    // Continue attacking current target
                    AttackWithSkill(targetBoss, resolvedSkillIndex);
                }
            }
            catch (Exception ex)
            {
                LogForm.Instance.AppendDebug($"[BalanceHP] Error during execution: {ex.Message}");
            }
        }

        private void AttackFirstAvailable(List<string> priorities, string skillIndex = null)
        {
            foreach (string p in priorities)
            {
                if (World.IsMonsterAvailable(p))
                {
                    AttackWithSkill(p, skillIndex);
                    return;
                }
            }
        }

        private void AttackWithSkill(string target, string skillIndex = null)
        {
            if (!string.IsNullOrWhiteSpace(skillIndex))
            {
                // Attack with skill
                Player.AttackMonster(target);
                Player.UseSkill(skillIndex);
            }
            else
            {
                // Regular attack
                Player.AttackMonster(target);
            }
        }

        private async Task CastSkillImmediate(string skillIndex)
        {
            try
            {
                // Wait for skill to be off cooldown (if necessary), but don't stall forever
                int attempts = 3;
                while (attempts-- > 0)
                {
                    int cd = Player.SkillAvailable(skillIndex);
                    if (cd <= 0)
                        break;

                    await Task.Delay(Math.Min(cd, 1000));
                }

                // First attempt to force-cast
                Player.ForceUseSkill(skillIndex);

                // Short delay, then verify by checking if the skill went on cooldown
                await Task.Delay(150);
                if (Player.SkillAvailable(skillIndex) <= 0)
                {
                    // If it's still instantly available, try one more time
                    LogForm.Instance.AppendDebug($"[BalanceHP] Skill {skillIndex} appears not to have fired, retrying once.");
                    Player.ForceUseSkill(skillIndex);
                }
            }
            catch (Exception ex)
            {
                LogForm.Instance.AppendDebug($"[BalanceHP] Error during skill cast {skillIndex}: {ex.Message}");
            }
        }

        public override string ToString()
        {
            string result = $"Balance HP:";
            if (!string.IsNullOrEmpty(Value1))
                result += $" {Value1}";
            if (!string.IsNullOrEmpty(Value2))
                result += $", {Value2}";
            if (!string.IsNullOrEmpty(Value3))
                result += $" | Skill: {Value3}";
            if (!string.IsNullOrEmpty(Label))
                result += $" | Thresholds: {Label}";
            return result;
        }
    }
}
