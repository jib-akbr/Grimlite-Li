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
            BotData.BotState = BotData.State.Combat;
            if (instance.Configuration.SkipAttack)
            {
                if (Player.HasTarget) 
                    Player.CancelTarget();
                if (Skill.isBuff(Index))
                    Player.ForceUseSkill(Index);
                return;
            }
            try
            {
                FindTarget(target);
				if (!Force && !Player.HasTarget)
					return;
				if (Wait)
				{
					await Task.Delay(Player.SkillAvailable(Index));
					FindTarget(target);
				}
				if (isCSH()) Player.ForceUseSkill(Index);
				else Player.UseSkill(Index);
            }
            finally { }
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
