using System;
using System.Collections.Generic;
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

            //Console.WriteLine("Mon:" + Monster);
            Player.AttackMonster(Monster);

            if (instance.Configuration.Skills.Count > 0)
                await UseSkillsSet(instance);

            Player.CancelTarget(); //timeout increased to 20 for Autoattack/empty skills users
            await instance.WaitUntil(() => !Player.HasTarget && !onPause, timeout: 20);

            if (AntiCounter)
            {
                OptionsManager.DisableAnimations = disableAnims;
                Flash.FlashCall -= AntiCounterHandler;
            }

            _cts?.Cancel(false);
            _cts?.Dispose();
        }

        private static CancellationTokenSource _cts;

        private static int Index = 0;

        private static string _lastBotSkill;
        private async Task UseSkillsSet(IBotEngine instance)
        {
            _cts = new CancellationTokenSource();

            int ClassIndex = 0;
            bool flag = BotData.SkillSet != null && BotData.SkillSet.ContainsKey("[" + BotData.BotSkill + "]");
            if (flag)
                ClassIndex = BotData.SkillSet["[" + BotData.BotSkill + "]"] + 1;

            int Count = instance.Configuration.Skills.Count - 1;
            if (Index > Count)
            {
                Index = ClassIndex;
                //LogForm.Instance.AppendDebug($"Index is higher than CurrentSkillcount, Sets index as {Index}");
            }
            if (_lastBotSkill != BotData.BotSkill)
            {// Reset index only when skillset changed
                if (ClassIndex != 0)
                {
                    Index = ClassIndex;
                }
                _lastBotSkill = BotData.BotSkill;
                //LogForm.Instance.AppendDebug($"Skillset Used  : {BotData.BotSkill}");
            }


            while (!_cts.IsCancellationRequested && !onPause && Player.HasTarget && Player.GetTargetHealth() > 0)
            {
                switch (this.Monster.ToLower())
                {
                    case "escherion":
                        if (World.IsMonsterAvailable("Staff of Inversion"))
                            Player.AttackMonster("Staff of Inversion");
                        break;
                    case "commander gallaeon":
                        if (World.IsMonsterAvailable("hydra crew"))
                            Player.AttackMonster("hydra crew");
                        break;
                    case "vath":
                        if (World.IsMonsterAvailable("Stalagbite"))
                            Player.AttackMonster("Stalagbite");
                        break;
                    case "ultra avatar tyndarius":
                        if (World.IsMonsterAvailable("Ultra Fire Orb"))
                            Player.AttackMonster("Ultra Fire Orb");
                        break;
                }

                if (KillPriority.Length > 0)
                {
                    List<string> priorities = new List<string>();
                    if (KillPriority.Contains(","))
                    {
                        foreach (string p in KillPriority.Split(','))
                        {
                            priorities.Add(p);
                        }
                    }
                    else
                    {
                        priorities.Add(KillPriority);
                    }

                    foreach (string p in priorities)
                    {
                        if (World.IsMonsterAvailable(p))
                        {
                            Player.AttackMonster(p);
                            break;
                        }
                    }
                }

                Skill s = instance.Configuration.Skills[Index];
                if (s.Type == Skill.SkillType.Label)
                {
                    //Reset back when meet with another SkillLabel
                    Index = ClassIndex;
                    continue;
                }

                if (!Player.IsAlive)
                    return;

                if (instance.Configuration.WaitForSkill || s.waitCd)
                {
                    BotManager.Instance.OnSkillIndexChanged(Index);
                    await Task.Delay(Player.SkillAvailable(s.Index));
                }

                s.ExecuteSkill();

                //Reset back when reaching end of skill list index
                Index = (Index < Count) ? Index + 1 : ClassIndex;

                await Task.Delay(instance.Configuration.SkillDelay);

            }

        }

        private void AntiCounterHandler(AxShockwaveFlashObjects.AxShockwaveFlash flash, string function, params object[] args)
        {
            string msg = args[0].ToString();
            if (!msg.StartsWith("{")) return;
            if (function == "pext")
            {
                dynamic packet = JsonConvert.DeserializeObject<dynamic>(msg);
                string type = packet["params"].type;
                dynamic data = packet["params"].dataObj;
                if (type != "json")
                    return;
                if (data.cmd == "ct")
                {
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

        public override string ToString()
        {
            return $"Kill {Monster}";
        }
    }
}
