using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.Networking;

namespace Grimoire.Botting.Commands.Combat
{
    public class CmdAttack : IBotCommand
    {
        public string Monster { get; set; }

        public bool UseSkill { get; set; } = false;

        public async Task Execute(IBotEngine instance)
		{
			BotData.BotState = BotData.State.Combat;
			
			// Reset skill index at the start of each attack
			ResetSkillIndex();
			
			if (instance.Configuration.SkipAttack)
            {
                if (Player.HasTarget) Player.CancelTarget();
                return;
            }

            if (instance.IsRunning && Player.IsAlive && Player.IsLoggedIn)
            {
                if (Monster == "*")
				{
					if (!Player.HasTarget)
					{
						Player.AttackMonster("*");
					}
				} 
                else
                {
					Player.AttackMonster(instance.IsVar(Monster) ? Configuration.Tempvariable[instance.GetVar(Monster)] : Monster);
				}
            }

            if (UseSkill)
            {
                if (instance.Configuration.Skills.Count > 0)
                    UseSkillsSet(instance);
                    //Task.Run(() => UseSkillsSet(instance));
            }

        }

        public override string ToString()
        {
            return "Attack " + this.Monster;
        }

        private int _skillIndex;
        private int Index;
        
        /// <summary>Reset skill indices when bot stops</summary>
        public static void ResetSkillIndex()
        {
            _staticSkillIndex = 0;
        }
        private static int _staticSkillIndex = 0;
        
        private async Task UseSkillsSet(IBotEngine instance)
        {
            int ClassIndex = -1;
            bool flag = BotData.SkillSet != null && BotData.SkillSet.ContainsKey("[" + BotData.BotSkill + "]");
            if (flag)
            {
                ClassIndex = BotData.SkillSet["[" + BotData.BotSkill + "]"] + 1;
            }
            int Count = instance.Configuration.Skills.Count - 1;
            this.Index = ClassIndex;
            bool flag2 = !Player.IsLoggedIn || !Player.IsAlive;
			/*if (flag2)
            {
                while (Player.HasTarget)
                {
                    Player.CancelTarget();
                    await Task.Delay(500);
                }
                return;
            }*/
			while (instance.IsRunning && Player.IsAlive && Player.IsLoggedIn && Player.HasTarget)
			{
				if (ClassIndex != -1)
				{
					//with label - use Index
					Skill s = instance.Configuration.Skills[this.Index];
					if (s.Type == Skill.SkillType.Label)
					{
						this.Index = ClassIndex;
					}

					await s.ExecuteSkill();

					int index;
					if (this.Index < Count)
					{
						int num3 = this.Index + 1;
						this.Index = num3;
						index = num3;
					}
					else
					{
						index = ClassIndex;
					}
					this.Index = index;
					s = null;
				}
				else
				{
					//non label - use _skillIndex
					Skill s = instance.Configuration.Skills[_skillIndex];

					await s.ExecuteSkill();

					int count = instance.Configuration.Skills.Count - 1;
					_skillIndex = _skillIndex >= count ? 0 : ++_skillIndex;
				}
				await Task.Delay(instance.Configuration.SkillDelay);
			}
        }

	}
}
