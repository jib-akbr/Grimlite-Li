using Grimoire.Game;
using Grimoire.UI;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
	public class CmdTargetAuraEquals : StatementCommand, IBotCommand
	{
		public CmdTargetAuraEquals()
		{
			Tag = "Aura";
			Text = "Target aura value equals";
		}

		public Task Execute(IBotEngine instance)
		{
			string Aura = instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1;
		string AuraValue = instance.IsVar(Value2) ? Configuration.Tempvariable[instance.GetVar(Value2)] : Value2;
		string Skill = instance.IsVar(Value3) ? Configuration.Tempvariable[instance.GetVar(Value3)] : Value3;

		int auraValue = Player.GetAuras(false, Aura);
		LogForm.Instance.AppendDebug($"[TargetAuraEquals] Aura '{Aura}': {auraValue}");
			int x = 0;
			int.TryParse(AuraValue, out x);
			if (auraValue == x)
		{
			LogForm.Instance.AppendDebug($"[TargetAuraEquals] Condition met ({auraValue} == {x})");
			if (!string.IsNullOrEmpty(Skill))
			{
				var availableMonsters = World.AvailableMonsters;
				if (availableMonsters.Count > 0)
				{
					Player.AttackMonster(availableMonsters[0].Name);
					LogForm.Instance.AppendDebug($"[TargetAuraEquals] Targeted {availableMonsters[0].Name}, casting skill: {Skill}");
					Player.UseSkill(Skill);
				}
				else
				{
					LogForm.Instance.AppendDebug($"[TargetAuraEquals] No monsters available to target for skill cast");
				}
			}
		}
		else
		{
		}
			return Task.FromResult<object>(null);
		}

		public override string ToString()
		{
			return $"Target aura equals: {Value1}, {Value2}, {Value3}";
		}
	}
}
