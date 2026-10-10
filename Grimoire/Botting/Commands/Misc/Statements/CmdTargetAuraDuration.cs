using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
	public class CmdTargetAuraDuration : StatementCommand, IBotCommand
	{
		public CmdTargetAuraDuration()
		{
			Tag = "Aura";
			Text = "Monster (ID) Aura duration is less than";
		}
		public int targetId
		{
			get;
			set;
		} = 0;

		//public bool modeP 
		//{ 
		//	get;
		//	set;
		//} = false;

        public Task Execute(IBotEngine instance)
		{
			string AuraName = instance.ResolveVars(Value1); 
			string TargetDuration = instance.ResolveVars(Value2);
			//instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1;
			//instance.IsVar(Value2) ? Configuration.Tempvariable[instance.GetVar(Value2)] : Value2;
			
            int CurrentDuration = AuraManager.EnemyAura(targetId).GetRemainingTime(AuraName);
            int.TryParse(TargetDuration, out int x);
			//x = 0 means aura not active
            if (CurrentDuration > x || CurrentDuration == 0)
				instance.Index++;
			return Task.FromResult<object>(null);
		}

		public override string ToString()
		{
            if (targetId == 0)
            {
                return $"Monster (ID) Aura duration is less than ... ms";
            }
            return $"Aura '{Value1}' on [id.{targetId}]: < {Value2}ms";
			//return $"Monster[id.{targetId}] Aura duration is less than: {Value1}, {Value2}ms";
		}
	}
}
