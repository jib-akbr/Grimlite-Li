using Grimoire.Game;
using Grimoire.Game.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdSetVar : StatementCommand, IBotCommand
    {
        public CmdSetVar()
        {
            Tag = "Misc";
            Text = "Set Temporary Variable";
        }

        public Task Execute(IBotEngine instance)
        {
            switch (Value1)
            {
                case "{weapon}":
                    Value2 = Player.Inventory.Items.FirstOrDefault(i => InventoryItem.Weapons.Contains(i.Category) && i.IsEquipped)?.Name;
                    break;
                case "{class}":
                    Value2 = Player.Inventory.Items.FirstOrDefault(i => i.Category == "Class" && i.IsEquipped)?.Name;
                    break;
                case "{helm}":
                    Value2 = Player.Inventory.Items.FirstOrDefault(i => i.Category == "Helm" && i.IsEquipped)?.Name;
                    break;
                case "{cape}":
                    Value2 = Player.Inventory.Items.FirstOrDefault(i => i.Category == "Cape" && i.IsEquipped)?.Name;
                    break;
                default:
                    break;
            }

            if (!Configuration.Tempvariable.ContainsKey(Value1))
                Configuration.Tempvariable.Add(Value1, Value2);
            else
                Configuration.Tempvariable[Value1] = Value2;

            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Variable {Value1}: {Value2}";
        }
    }
}