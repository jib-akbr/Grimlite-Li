using Grimoire.Game;
using System;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdNameEquals : StatementCommand, IBotCommand
    {
        public CmdNameEquals()
        {
            Tag = "This player";
            Text = "Name Equals";
        }

        public Task Execute(IBotEngine instance)
        {
            if (Player.Username.Equals(instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1, StringComparison.OrdinalIgnoreCase))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Player Name Equals: " + Value1;
        }
    }
}