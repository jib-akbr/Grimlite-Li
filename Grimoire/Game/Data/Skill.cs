using System;
using System.Collections.Generic;
using Grimoire.Tools;
using Newtonsoft.Json.Linq;

namespace Grimoire.Game.Data
{
    public class Skill
    {
        public string Text
        {
            get;
            set;
        }
        public string Index { get; set; }

        public Skill.SkillType Type { get; set; }

        public Skill.SafeType SType { get; set; }

        public bool IsSafeMp { get; set; }

        public int SafeValue { get; set; }
        
        public Skill.SafeType SType2 { get; set; }

        public bool IsSafeMp2 { get; set; }

        public int SafeValue2 { get; set; }
        
        public bool waitCd { get; set; } = false;

        public static string GetSkillName(string index)
        {
            return Flash.Call<string>("GetSkillName", new string[]{index});
        }

        public static bool isBuff(string index)
        {
            try
            {
                var prop = Flash.Instance.GetGameObject<List<JObject>>("world.actions.active");

                string selfskill = prop[int.Parse(index)]?["tgt"]?.ToString();
                //debug($"{selfskill}");
                return selfskill != "h"; //H or F/S [H - monster/hostile, F/S - Friendly/self]
            }
            catch 
            { 
                return false; 
            }
        }

        public enum SkillType
        {
            Normal,
            Safe,
            Label
        }

        public enum SafeType
        {
            LowerThan,
            GreaterThan,
            Equals
        }

        public void ExecuteSkill()
        {
            Skill s = this;
            if (s.Type == Skill.SkillType.Label)
            {
                // Handle statement commands (aura checks)
                ExecuteStatementCommand();
            }
            else if (s.Type == Skill.SkillType.Safe)
            {
                double healthPercent = (double)Player.Health / Player.HealthMax * 100;
                double manaPercent = (double)Player.Mana / Player.ManaMax * 100;
                
                if (s.IsSafeMp)
                {
                    UI.LogForm.Instance.AppendDebug($"[SafeSkill] Skill {s.Index} ({s.Text}) - MP: {manaPercent:F1}% (Requirement: {s.SType} {s.SafeValue}%)");
                    
                    switch (s.SType)
                    {
                        case Skill.SafeType.LowerThan:
                            if (manaPercent <= s.SafeValue)
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✓ Using skill {s.Index} - MP condition met");
                                useSkill(s.Index);
                            }
                            else
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✗ Skipping skill {s.Index} - MP too high");
                            }
                            break;
                        case Skill.SafeType.GreaterThan:
                            if (manaPercent >= s.SafeValue)
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✓ Using skill {s.Index} - MP condition met");
                                useSkill(s.Index);
                            }
                            else
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✗ Skipping skill {s.Index} - MP too low");
                            }
                            break;
                        case Skill.SafeType.Equals:
                            if (manaPercent == s.SafeValue)
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✓ Using skill {s.Index} - MP condition met");
                                useSkill(s.Index);
                            }
                            else
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✗ Skipping skill {s.Index} - MP doesn't match");
                            }
                            break;
                    }
                }
                else
                {
                    UI.LogForm.Instance.AppendDebug($"[SafeSkill] Skill {s.Index} ({s.Text}) - HP: {healthPercent:F1}% (Requirement: {s.SType} {s.SafeValue}%)");
                    
                    switch (s.SType)
                    {
                        case Skill.SafeType.LowerThan:
                            if (healthPercent <= s.SafeValue)
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✓ Using skill {s.Index} - HP condition met");
                                useSkill(s.Index);
                            }
                            else
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✗ Skipping skill {s.Index} - HP too high");
                            }
                            break;
                        case Skill.SafeType.GreaterThan:
                            if (healthPercent >= s.SafeValue)
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✓ Using skill {s.Index} - HP condition met");
                                useSkill(s.Index);
                            }
                            else
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✗ Skipping skill {s.Index} - HP too low");
                            }
                            break;
                        case Skill.SafeType.Equals:
                            if (healthPercent == s.SafeValue)
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✓ Using skill {s.Index} - HP condition met");
                                useSkill(s.Index);
                            }
                            else
                            {
                                UI.LogForm.Instance.AppendDebug($"[SafeSkill] ✗ Skipping skill {s.Index} - HP doesn't match");
                            }
                            break;
                    }
                }
            }
            else
            {
                UI.LogForm.Instance.AppendDebug($"[Skill] Using skill {s.Index} ({s.Text})");
                useSkill(s.Index);
                //Player.UseSkill(s.Index);
            }
        }

        private void ExecuteStatementCommand()
        {
            UI.LogForm.Instance.AppendDebug($"[AuraStmt] Executing: {Index} - Text: {Text}");
            
            // Parse parameters from Text field: "[Condition] auraname|value|skillindex|aura2name|aura2value|operator"
            string[] parts = Text.Split('|');
            if (parts.Length < 3)
            {
                UI.LogForm.Instance.AppendDebug($"[AuraStmt] ERROR: Invalid format, expected 3 parts but got {parts.Length}");
                return;
            }

            string auraName = parts[0];
            // Remove the condition prefix like "[Player Aura <] "
            int pipePos = auraName.IndexOf(']');
            if (pipePos >= 0)
                auraName = auraName.Substring(pipePos + 1).Trim();

            if (!int.TryParse(parts[1], out int auraValue))
            {
                UI.LogForm.Instance.AppendDebug($"[AuraStmt] ERROR: Could not parse aura value from '{parts[1]}'");
                return;
            }
            string skillIndex = parts[2].Trim();
            
            // Check for multi-aura parameters
            string aura2Name = null;
            int aura2Value = 0;
            string auraOperator = null;
            bool hasMultiAura = false;
            
            if (parts.Length >= 6)
            {
                aura2Name = parts[3].Trim();
                if (int.TryParse(parts[4], out aura2Value))
                {
                    auraOperator = parts[5].Trim().ToUpper();
                    hasMultiAura = true;
                    UI.LogForm.Instance.AppendDebug($"[AuraStmt] Multi-aura detected - Aura2: '{aura2Name}', Value: {aura2Value}, Operator: {auraOperator}");
                }
            }
            
            UI.LogForm.Instance.AppendDebug($"[AuraStmt] Parsed - Aura: '{auraName}', TargetValue: {auraValue}, Skill: {skillIndex}");

            // Execute based on command type
            switch (Index)
            {
                case "CmdPlayerAuraEquals":
                    ExecutePlayerAura(auraName, auraValue, skillIndex, (current, target) => current == target, hasMultiAura, aura2Name, aura2Value, auraOperator);
                    break;
                case "CmdPlayerAuraGreaterThan":
                    ExecutePlayerAura(auraName, auraValue, skillIndex, (current, target) => current > target, hasMultiAura, aura2Name, aura2Value, auraOperator);
                    break;
                case "CmdPlayerAuraLessThan":
                    ExecutePlayerAura(auraName, auraValue, skillIndex, (current, target) => current < target, hasMultiAura, aura2Name, aura2Value, auraOperator);
                    break;
                case "CmdTargetAuraEquals":
                    ExecuteTargetAura(auraName, auraValue, skillIndex, (current, target) => current == target, hasMultiAura, aura2Name, aura2Value, auraOperator);
                    break;
                case "CmdTargetAuraGreaterThan":
                    ExecuteTargetAura(auraName, auraValue, skillIndex, (current, target) => current > target, hasMultiAura, aura2Name, aura2Value, auraOperator);
                    break;
                case "CmdTargetAuraLessThan":
                    ExecuteTargetAura(auraName, auraValue, skillIndex, (current, target) => current < target, hasMultiAura, aura2Name, aura2Value, auraOperator);
                    break;
            }
        }

        private void ExecutePlayerAura(string auraName, int targetValue, string skillIndex, Func<int, int, bool> condition, bool hasMultiAura = false, string aura2Name = null, int aura2Value = 0, string auraOperator = null)
        {
            int currentAuraValue = Player.GetAuras(true, auraName);
            UI.LogForm.Instance.AppendDebug($"[PlayerAura] Current '{auraName}' = {currentAuraValue}, Target = {targetValue}");
            
            bool firstConditionMet = condition(currentAuraValue, targetValue);
            bool shouldCast = firstConditionMet;
            
            // If multi-aura is enabled, check second aura
            if (hasMultiAura && !string.IsNullOrEmpty(aura2Name))
            {
                int currentAura2Value = Player.GetAuras(true, aura2Name);
                UI.LogForm.Instance.AppendDebug($"[PlayerAura] Current '{aura2Name}' = {currentAura2Value}, Target = {aura2Value}");
                
                bool secondConditionMet = condition(currentAura2Value, aura2Value);
                
                if (auraOperator == "AND")
                {
                    shouldCast = firstConditionMet && secondConditionMet;
                    UI.LogForm.Instance.AppendDebug($"[PlayerAura] AND condition: First={firstConditionMet}, Second={secondConditionMet}, Result={shouldCast}");
                }
                else if (auraOperator == "OR")
                {
                    shouldCast = firstConditionMet || secondConditionMet;
                    UI.LogForm.Instance.AppendDebug($"[PlayerAura] OR condition: First={firstConditionMet}, Second={secondConditionMet}, Result={shouldCast}");
                }
            }
            
            if (shouldCast)
            {
                UI.LogForm.Instance.AppendDebug($"[PlayerAura] Condition MET! Attempting to cast skill {skillIndex}");
                var availableMonsters = World.AvailableMonsters;
                UI.LogForm.Instance.AppendDebug($"[PlayerAura] Available monsters: {availableMonsters.Count}");
                
                if (availableMonsters.Count > 0)
                {
                    UI.LogForm.Instance.AppendDebug($"[PlayerAura] Attacking {availableMonsters[0].Name} and using skill {skillIndex}");
                    Player.AttackMonster(availableMonsters[0].Name);
                    Player.UseSkill(skillIndex);
                }
                else
                {
                    UI.LogForm.Instance.AppendDebug($"[PlayerAura] No monsters available to target");
                }
            }
            else
            {
                UI.LogForm.Instance.AppendDebug($"[PlayerAura] Condition NOT met (current={currentAuraValue}, target={targetValue})");
            }
        }

        private void ExecuteTargetAura(string auraName, int targetValue, string skillIndex, Func<int, int, bool> condition, bool hasMultiAura = false, string aura2Name = null, int aura2Value = 0, string auraOperator = null)
        {
            int currentAuraValue = Player.GetAuras(false, auraName);
            UI.LogForm.Instance.AppendDebug($"[TargetAura] Current '{auraName}' = {currentAuraValue}, Target = {targetValue}");
            
            bool firstConditionMet = condition(currentAuraValue, targetValue);
            bool shouldCast = firstConditionMet;
            
            // If multi-aura is enabled, check second aura
            if (hasMultiAura && !string.IsNullOrEmpty(aura2Name))
            {
                int currentAura2Value = Player.GetAuras(false, aura2Name);
                UI.LogForm.Instance.AppendDebug($"[TargetAura] Current '{aura2Name}' = {currentAura2Value}, Target = {aura2Value}");
                
                bool secondConditionMet = condition(currentAura2Value, aura2Value);
                
                if (auraOperator == "AND")
                {
                    shouldCast = firstConditionMet && secondConditionMet;
                    UI.LogForm.Instance.AppendDebug($"[TargetAura] AND condition: First={firstConditionMet}, Second={secondConditionMet}, Result={shouldCast}");
                }
                else if (auraOperator == "OR")
                {
                    shouldCast = firstConditionMet || secondConditionMet;
                    UI.LogForm.Instance.AppendDebug($"[TargetAura] OR condition: First={firstConditionMet}, Second={secondConditionMet}, Result={shouldCast}");
                }
            }
            
            if (shouldCast)
            {
                UI.LogForm.Instance.AppendDebug($"[TargetAura] Condition MET! Attempting to cast skill {skillIndex}");
                var availableMonsters = World.AvailableMonsters;
                UI.LogForm.Instance.AppendDebug($"[TargetAura] Available monsters: {availableMonsters.Count}");
                
                if (availableMonsters.Count > 0)
                {
                    UI.LogForm.Instance.AppendDebug($"[TargetAura] Attacking {availableMonsters[0].Name} and using skill {skillIndex}");
                    Player.AttackMonster(availableMonsters[0].Name);
                    Player.UseSkill(skillIndex);
                }
                else
                {
                    UI.LogForm.Instance.AppendDebug($"[TargetAura] No monsters available to target");
                }
            }
            else
            {
                UI.LogForm.Instance.AppendDebug($"[TargetAura] Condition NOT met (current={currentAuraValue}, target={targetValue})");
            }
        }

        private void useSkill(string Index)
        {
            if (Player.EquippedClass.IndexOf("Chrono Shadow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Player.ForceUseSkill(Index);
                return;
            }
            Player.UseSkill(Index);
        }

        public override string ToString()
        {
            string text = Text;
            if (text != null)
                if (text.StartsWith("1: ") || text.StartsWith("2: ") || text.StartsWith("3: ") || text.StartsWith("4: "))
                {
                    text = text.Remove(0, 3);
                }
            string skillName = text ?? Skill.GetSkillName(Index);
            string safeType = IsSafeMp ? "MP" : "HP";
            string safeTypeS = SType == SafeType.GreaterThan ? ">=" : "<=";

            string skillText;


            if (Type == SkillType.Safe)
                skillText = $"[{safeType} {safeTypeS} {SafeValue}%] {Index}: {skillName}";
            else if (Type == SkillType.Label)
                skillText = $"{Text}";
            else //normal
                skillText = $"{Index}: {skillName}";
            return waitCd ? $"[Wait] {skillText}" : skillText;
        }
    }
}
