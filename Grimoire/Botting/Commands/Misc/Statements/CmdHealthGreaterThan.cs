using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdHealthGreaterThan : StatementCommand, IBotCommand
    {
        public CmdHealthGreaterThan()
        {
            Tag = "This player";
            Text = "Health is greater than";
        }

        public Task Execute(IBotEngine instance)
        {
            string HpCheck = (instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1);
            int Hp;

            if (HpCheck.Contains("%"))
            {
                Hp = World.GetPlayerHealthPercentage(Player.Username);
                HpCheck = HpCheck.Replace("%", "");
            }
            else
                Hp = Player.Health;

            if (Hp <= int.Parse(HpCheck))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Health is greater than: " + Value1;
        }
    }
}