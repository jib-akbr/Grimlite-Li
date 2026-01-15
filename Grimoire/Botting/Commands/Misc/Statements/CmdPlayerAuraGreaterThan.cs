using Grimoire.Game;
using Grimoire.UI;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
	public class CmdPlayerAuraGreaterThan: StatementCommand, IBotCommand
	{
		public CmdPlayerAuraGreaterThan()
		{
			Tag = "Aura";
			Text = "Player aura value greater than";
		}

		public Task Execute(IBotEngine instance)
		{
			string Aura = string.IsNullOrEmpty(Value1) ? Value1 : (instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1);
			string AuraValue = string.IsNullOrEmpty(Value2) ? Value2 : (instance.IsVar(Value2) ? Configuration.Tempvariable[instance.GetVar(Value2)] : Value2);
			string Skill = string.IsNullOrEmpty(Value3) ? Value3 : (instance.IsVar(Value3) ? Configuration.Tempvariable[instance.GetVar(Value3)] : Value3);
			string Aura2 = string.IsNullOrEmpty(Value4) ? Value4 : (instance.IsVar(Value4) ? Configuration.Tempvariable[instance.GetVar(Value4)] : Value4);
			string AuraValue2 = string.IsNullOrEmpty(Value5) ? Value5 : (instance.IsVar(Value5) ? Configuration.Tempvariable[instance.GetVar(Value5)] : Value5);
			string Operator = string.IsNullOrEmpty(Value6) ? Value6 : (instance.IsVar(Value6) ? Configuration.Tempvariable[instance.GetVar(Value6)] : Value6);

			int auraValue = Player.GetAuras(true, Aura);
			int x = 0;
			int.TryParse(AuraValue, out x);
			bool condition1 = auraValue > x;
			bool finalCondition = condition1;

			// Handle multi-aura if second aura is specified
			if (!string.IsNullOrEmpty(Aura2))
			{
				int auraValue2 = Player.GetAuras(true, Aura2);
				int y = 0;
				int.TryParse(AuraValue2, out y);
				bool condition2 = auraValue2 > y;
				if (Operator.ToUpper() == "OR")
					finalCondition = condition1 || condition2;
				else // Default to AND
					finalCondition = condition1 && condition2;
			}

			if (!finalCondition)
			{
				instance.Index++;
			}
			else
			{
				LogForm.Instance.AppendDebug($"[PlayerAuraGreaterThan] Condition met");
				if (!string.IsNullOrEmpty(Skill))
				{
					var availableMonsters = World.AvailableMonsters;
					if (availableMonsters.Count > 0)
					{
						Player.AttackMonster(availableMonsters[0].Name);
						LogForm.Instance.AppendDebug($"[PlayerAuraGreaterThan] Targeted {availableMonsters[0].Name}, casting skill: {Skill}");
						Player.UseSkill(Skill);
					}
					else
					{
						LogForm.Instance.AppendDebug($"[PlayerAuraGreaterThan] No monsters available to target for skill cast");
					}
				}
			}

			return Task.FromResult<object>(null);
		}

		public override string ToString()
		{
			return $"Player aura greater than: {Value1}, {Value2}, {Value3}";
		}
	}
}
