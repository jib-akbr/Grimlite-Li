using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdMonsterHealthLessThan : StatementCommand, IBotCommand
    {
        public CmdMonsterHealthLessThan()
        {
            Tag = "Monster";
            Text = "Monster HP less than";
        }

        public Task Execute(IBotEngine instance)
        {
            string Monster = instance.ResolveVars(Value1);
            string Health = instance.ResolveVars(Value2)    ;

            int.TryParse(Health, out int i);
            int? monhp = World.GetMonsterHealth(Monster);
            if (monhp.HasValue && monhp > i)
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            string text = Value1 ?? "Monster";
            return $"{text}'s HP less than: {Value2}";
        }
    }
}