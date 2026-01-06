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

        public CmdBalanceHP()
        {
            Tag = "Monster";
            Text = "Balance HP";
            Description1 = "Boss 1 name (primary)";
            Description2 = "Boss 2 name (secondary)";
            // Don't set unused fields - leave them null so they don't serialize
        }

        public override void OnBotStarted()
        {
            // Reset counters when bot starts
            _currentPhaseIndex = 0;
            _onSecondBoss = false;
            _thresholds = new int[0];
            _lastLoggedHP = -1;
        }

        public override void OnBotStopped()
        {
            // Reset counters when bot stops
            _currentPhaseIndex = 0;
            _onSecondBoss = false;
            _thresholds = new int[0];
            _lastLoggedHP = -1;
        }

        public async Task Execute(IBotEngine instance)
        {
            // Value1 = Boss 1 name/monmapid (primary boss, can be comma-separated priorities)
            // Value2 = Boss 2 name/monmapid (secondary boss, can be comma-separated priorities)
            // Label = Comma-separated HP thresholds in descending order (e.g., "80,50,20")

            string boss1Raw = instance.ResolveVars(Value1);
            string boss2Raw = instance.ResolveVars(Value2);
            string thresholdStr = instance.ResolveVars(Label);

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

            // If no valid boss names, log error and return
            if (boss1Priorities.Count == 0 || boss2Priorities.Count == 0)
            {
                LogForm.Instance.AppendDebug($"[BalanceHP] Boss names not provided. Boss1: '{boss1Raw}', Boss2: '{boss2Raw}'");
                return;
            }

            // If no thresholds, just attack Boss1
            if (_thresholds.Length == 0)
            {
                LogForm.Instance.AppendDebug($"[BalanceHP] No valid thresholds provided, attacking Boss1");
                try
                {
                    AttackFirstAvailable(boss1Priorities);
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
                    AttackFirstAvailable(boss1Priorities);
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
                        AttackFirstAvailable(boss2Priorities);
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
                            AttackFirstAvailable(boss1Priorities);
                        }
                        else
                        {
                            // Continue with next threshold
                            LogForm.Instance.AppendDebug($"[BalanceHP] Boss2 reached {currentThreshold}%, advancing to phase {_currentPhaseIndex} (threshold {_thresholds[_currentPhaseIndex]}%), back to Boss1");
                            AttackFirstAvailable(boss1Priorities);
                        }
                    }
                }
                else
                {
                    // Continue attacking current target
                    Player.AttackMonster(targetBoss);
                }
            }
            catch (Exception ex)
            {
                LogForm.Instance.AppendDebug($"[BalanceHP] Error during execution: {ex.Message}");
            }
        }

        private void AttackFirstAvailable(List<string> priorities)
        {
            foreach (string p in priorities)
            {
                if (World.IsMonsterAvailable(p))
                {
                    Player.AttackMonster(p);
                    return;
                }
            }
        }

        public override string ToString()
        {
            string result = $"Balance HP:";
            if (!string.IsNullOrEmpty(Value1))
                result += $" {Value1}";
            if (!string.IsNullOrEmpty(Value2))
                result += $", {Value2}";
            if (!string.IsNullOrEmpty(Label))
                result += $" | Thresholds: {Label}";
            return result;
        }
    }
}
