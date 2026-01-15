using Grimoire.Game;
using System;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdMapIsNot : StatementCommand, IBotCommand
    {
        public CmdMapIsNot()
        {
            Tag = "Map";
            Text = "Map is not";
        }

        public Task Execute(IBotEngine instance)
        {
            string mapValue = string.IsNullOrEmpty(Value1) ? Value1 : (instance.IsVar(Value1)  ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1);
            if (((mapValue).Contains("-") ? (mapValue).Split('-')[0] : (mapValue)).Equals(Player.Map, StringComparison.OrdinalIgnoreCase))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Map is not: " + Value1;
        }
    }
}