using Grimoire.Game;
using Grimoire.UI;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
	public class CmdTargetAuraLessThan : StatementCommand, IBotCommand
	{
		public CmdTargetAuraLessThan()
		{
			Tag = "Aura";
			Text = "Target aura value less than";
		}

		public Task Execute(IBotEngine instance)
		{
			string Aura = instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1;
		string AuraValue = instance.IsVar(Value2) ? Configuration.Tempvariable[instance.GetVar(Value2)] : Value2;
		string Skill = instance.IsVar(Value3) ? Configuration.Tempvariable[instance.GetVar(Value3)] : Value3;

		int auraValue = Player.GetAuras(false, Aura);
		LogForm.Instance.AppendDebug($"[TargetAuraLessThan] Aura '{Aura}': {auraValue}");
			int x = 0;
			int.TryParse(AuraValue, out x);
			if (auraValue < x)
		{
			LogForm.Instance.AppendDebug($"[TargetAuraLessThan] Condition met ({auraValue} < {x})");
			if (!string.IsNullOrEmpty(Skill))
			{
				var availableMonsters = World.AvailableMonsters;
				if (availableMonsters.Count > 0)
				{
					Player.AttackMonster(availableMonsters[0].Name);
					LogForm.Instance.AppendDebug($"[TargetAuraLessThan] Targeted {availableMonsters[0].Name}, casting skill: {Skill}");
					Player.UseSkill(Skill);
				}
				else
				{
					LogForm.Instance.AppendDebug($"[TargetAuraLessThan] No monsters available to target for skill cast");
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
			return $"Target aura less than: {Value1}, {Value2}, {Value3}";
		}
	}
}
