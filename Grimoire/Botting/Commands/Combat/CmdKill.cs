using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.Tools;
using Grimoire.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Grimoire.Botting.Commands.Combat
{
    public class CmdKill : IBotCommand
    {
        public string Monster { get; set; }
        public string KillPriority { get; set; } = "";
        public bool AntiCounter { get; set; } = false;
        public string SkillSet { get; set; } = "";

        private bool onPause = false;

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Combat;

            onPause = false;

            if (instance.Configuration.SkipAttack)
            {
                if (Player.HasTarget) Player.CancelTarget();
                return;
            }

            string Monster = instance.IsVar(this.Monster) ? Configuration.Tempvariable[instance.GetVar(this.Monster)] : this.Monster;

            //waiting monster to respawn for 3s
            await instance.WaitUntil(() => World.IsMonsterAvailable(Monster), null, 3);

            if (instance.Configuration.WaitForAllSkills)
            {
                await Task.Delay(Player.AllSkillsAvailable);
            }

            if (!instance.IsRunning || !Player.IsAlive || !Player.IsLoggedIn)
                return;

            bool disableAnims = OptionsManager.DisableAnimations;
            if (AntiCounter)
            {
                OptionsManager.DisableAnimations = false;
                Flash.FlashCall += AntiCounterHandler;
            }

            LogForm.Instance.AppendDebug($"[CmdKill] Attacking monster: {Monster}");
            Player.AttackMonster(Monster);

            // Load skillset if one is specified (same as BotManager does)
            List<Skill> skillsToUse = new List<Skill>();
            
            // Treat empty string as "Auto Attack" (default behavior)
            string skillSetToUse = string.IsNullOrEmpty(SkillSet) ? "Auto Attack" : SkillSet;
            
            if (skillSetToUse != "Auto Attack")
            {
                LogForm.Instance.AppendDebug($"[CmdKill] Loading skillset: {skillSetToUse}");
                var skillSetData = SkillSetManager.Instance.LoadSkillSet(skillSetToUse);
                if (skillSetData != null && skillSetData.Skills != null)
                {
                    foreach (var savedSkill in skillSetData.Skills)
                    {
                        var skill = new Skill
                        {
                            Index = savedSkill.Index,
                            Text = savedSkill.Text,
                            Type = (Skill.SkillType)savedSkill.Type,
                            SType = (Skill.SafeType)savedSkill.SafeType,
                            IsSafeMp = savedSkill.IsSafeMp,
                            SafeValue = savedSkill.SafeValue,
                            SType2 = (Skill.SafeType)savedSkill.SafeType2,
                            IsSafeMp2 = savedSkill.IsSafeMp2,
                            SafeValue2 = savedSkill.SafeValue2,
                            waitCd = savedSkill.WaitCooldown,
                            dodgeAttack = savedSkill.WaitDodge
                        };
                        skillsToUse.Add(skill);
                    }
                }
            }
            else
            {
                LogForm.Instance.AppendDebug($"[CmdKill] Using auto attack from main UI");
            }
            
            // Use BotManager's skill execution system (it already works perfectly)
            if (skillsToUse.Count > 0)
            {
                // Custom skillset - execute those skills
                LogForm.Instance.AppendDebug($"[CmdKill] Executing custom skillset with {skillsToUse.Count} skills");
                while (Player.IsAlive && World.IsMonsterAvailable(Monster) && instance.IsRunning)
                {
                    try
                    {
                        if (!Player.HasTarget)
                        {
                            Player.AttackMonster(Monster);
                            await Task.Delay(200);
                        }

                        foreach (var skill in skillsToUse)
                        {
                            if (!instance.IsRunning || !Player.IsAlive || !World.IsMonsterAvailable(Monster))
                                break;
                            
                            await skill.ExecuteSkill();
                            await Task.Delay(50);
                        }
                        
                        await Task.Delay(25);
                    }
                    catch (Exception ex)
                    {
                        LogForm.Instance.AppendDebug($"[CmdKill] Error: {ex.Message}");
                        await Task.Delay(100);
                    }
                }
            }
            else
            {
                // No custom skillset - execute skills 1, 2, 3, 4 (hardcoded auto attack)
                LogForm.Instance.AppendDebug($"[CmdKill] Using hardcoded auto attack skills 1, 2, 3, 4");
                int[] autoAttackSkills = { 1, 2, 3, 4 };
                
                while (Player.IsAlive && World.IsMonsterAvailable(Monster) && instance.IsRunning)
                {
                    try
                    {
                        // Ensure we have a target
                        if (!Player.HasTarget && World.AvailableMonsters.Count > 0)
                        {
                            Player.AttackMonster(Monster);
                            await Task.Delay(200);
                        }

                        // Execute skills 1, 2, 3, 4 in sequence
                        foreach (int skillIndex in autoAttackSkills)
                        {
                            if (!instance.IsRunning || !Player.IsAlive || !World.IsMonsterAvailable(Monster))
                                break;
                            
                            Player.UseSkill(skillIndex.ToString());
                            await Task.Delay(50);
                        }
                        
                        await Task.Delay(25);
                    }
                    catch (Exception ex)
                    {
                        LogForm.Instance.AppendDebug($"[CmdKill] Error: {ex.Message}");
                        await Task.Delay(100);
                    }
                }
            }
            
            LogForm.Instance.AppendDebug($"[CmdKill] Combat finished");

            Player.CancelTarget();
            await instance.WaitUntil(() => !Player.HasTarget && !onPause, timeout: 20);

            if (AntiCounter)
            {
                OptionsManager.DisableAnimations = disableAnims;
                Flash.FlashCall -= AntiCounterHandler;
            }

            _cts?.Cancel(false);
        }

        private CancellationTokenSource _cts;

        private void AntiCounterHandler(AxShockwaveFlashObjects.AxShockwaveFlash flash, string function, params object[] args)
        {
            string msg = args[0].ToString();
            if (!msg.StartsWith("{")) return;
            if (function == "pext")
            {
                dynamic packet = JsonConvert.DeserializeObject<dynamic>(msg);
                string type = packet["params"].type;
                dynamic data = packet["params"].dataObj;
                UI.LogForm.Instance.AppendDebug($"[CmdKill] pext packet received - type: {type}");
                if (type == "json")
                {
                    UI.LogForm.Instance.AppendDebug($"[CmdKill] JSON packet - cmd: {data.cmd}");
                    if (data.cmd == "ct")
                    {
                        UI.LogForm.Instance.AppendDebug($"[CmdKill] ct packet detected");
                        // Check for dodge actions in sara array
                        JArray sara = (JArray)data.sara;
                        if (sara != null)
                        {
                            UI.LogForm.Instance.AppendDebug($"[CmdKill] Sara array found with {sara.Count} actions");
                            foreach (JObject action in sara)
                            {
                                JObject actionResult = (JObject)action["actionResult"];
                                if (actionResult != null)
                                {
                                    string actionType = actionResult["type"]?.ToString();
                                    string targetInfo = actionResult["tInf"]?.ToString();
                                    
                                    UI.LogForm.Instance.AppendDebug($"[CmdKill] Combat action - Type: {actionType}, Target: {targetInfo}");
                                    
                                    // Check if player dodged (tInf starts with "p:")
                                    if (actionType == "dodge" && targetInfo != null && targetInfo.StartsWith("p:"))
                                    {
                                        UI.LogForm.Instance.AppendDebug($"[CmdKill] 🛡️ DODGE DETECTED! Notifying DodgeDetector...");
                                        Grimoire.Botting.Commands.Combat.DodgeDetector.NotifyDodge(targetInfo);
                                    }
                                }
                            }
                        }
                        else
                        {
                            UI.LogForm.Instance.AppendDebug($"[CmdKill] No sara array in ct packet");
                        }
                        
                        JArray anims = (JArray)data.anims;
                        if (anims != null)
                            if (anims[0]["msg"].ToString().ToLower().Contains("prepares a counter attack"))
                            {
                                Player.CancelAutoAttack();
                                Player.CancelTarget();
                                onPause = true;
                                Console.WriteLine("Counter Attack: active");
                            }
                        JArray a = (JArray)data.a;
                        if (a != null)
                            foreach (JObject aura in a)
                            {
                                JObject aura2 = (JObject)aura["aura"];
                                if (aura2.GetValue("nam")?.ToString() == "Counter Attack" && aura.GetValue("cmd")?.ToString() == "aura--")
                                {
                                    onPause = false;
                                    Console.WriteLine("Counter Attack: fades");
                                    break;
                                }
                            }
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"Kill {Monster}";
        }
    }
}
