using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdPlayersHPLessThan : StatementCommand, IBotCommand
    {
        public CmdPlayersHPLessThan()
        {
            Tag = "Player";
            Text = "Health is less than";
        }

        public Task Execute(IBotEngine instance)
        {
            string PlayerName = string.IsNullOrEmpty(Value1) ? Value1 : (instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1);
            int CheckHP = int.Parse(string.IsNullOrEmpty(Value2) ? Value2 : (instance.IsVar(Value2) ? Configuration.Tempvariable[instance.GetVar(Value2)] : Value2));
            if (!(World.GetPlayerHealthPercentage(PlayerName) < CheckHP))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            string text = "Player HP less than: ";
            if (Value1 != null && Value2 != null)
            {
                text = $"{Value1}'s HP less than: {Value2}%";
            }
            return text;
        }
    }
}
