using System;
using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.Game.Data;

namespace Grimoire.Botting.Commands.Combat
{
    public class CmdUseSkill : IBotCommand
    {
        public string Monster { get; set; } = "*";

        public string Index { get; set; }

        public bool Wait { get; set; }

        public bool Force { get; set; }

        public async Task Execute(IBotEngine instance)
        {
            string target = instance.IsVar(Monster) ? Configuration.Tempvariable[instance.GetVar(Monster)] : Monster;
            if (instance.Configuration.SkipAttack)
            {
                if (Player.HasTarget) 
                    Player.CancelTarget();
                if (Skill.isBuff(Index))
                    Player.ForceUseSkill(Index);
                return;
            }
            
            // Auto-fix for stuck skill 5 bug
            if (Index == "5")
            {
                await Player.ResetSkill5IfStuck();
            }
            
            FindTarget(target);
            if (!Force)
            {
                if (!Player.HasTarget) return;
            }
            if (Wait)
            {
                await Task.Delay(Player.SkillAvailable(Index));
                FindTarget(target);
            }
            
            // Use the async skill execution with dodge logic
            if (Force || isCSH())
                Player.ForceUseSkill(Index);
            else if (int.TryParse(Index, out int skillIndex) && skillIndex < instance.Configuration.Skills.Count)
            {
                Skill skill = instance.Configuration.Skills[skillIndex];
                await skill.useSkill(Index);
            }
            else
            {
                // Fallback to ForceUseSkill if skill not found in config
                Player.ForceUseSkill(Index);
            }
        }

        private bool isCSH()
        {
            return Player.EquippedClass.IndexOf("Chrono Shadow", StringComparison.OrdinalIgnoreCase) >= 0;
        }      


        private void FindTarget(string target)
        {
            if (Monster == "*")
            {
                if (!Player.HasTarget)
                {
                    Player.AttackMonster("*");
                }
                return;
            }
            else
            {
                Player.AttackMonster(target);
            }
        }

        public override string ToString()
        {
            return "Skill " + $"[{Monster}] " + (Wait ? "[Wait] " : " ") + (Force ? "[Force] " : " ") + Index + ": " + Skill.GetSkillName(Index);
        }
    }
}
