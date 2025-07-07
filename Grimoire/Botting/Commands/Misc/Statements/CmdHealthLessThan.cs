using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdHealthLessThan : StatementCommand, IBotCommand
    {
        public CmdHealthLessThan()
        {
            Tag = "This player";
            Text = "Health is less than";
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

            if (Hp > int.Parse(HpCheck))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Health is less than: " + Value1;
        }
    }
}