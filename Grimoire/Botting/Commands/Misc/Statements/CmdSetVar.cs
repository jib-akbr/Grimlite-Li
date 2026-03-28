using Grimoire.Game;
using Grimoire.Game.Data;
using System;
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
            //var list = Player.Inventory.Items;
            //foreach (var item in list)
            //{
            //    Console.WriteLine($"[{item.Id}] {item.Name} ({item.Category}) {item.IsEquipped}");
            //}
            switch (Value1)
            {
                case "{weapon}":
                    Value2 = Player.Inventory.Items.FirstOrDefault
                    (i => InventoryItem.Weapons.Contains(i.Category) && i.IsEquipped)?.Name ?? "Error";
                    break;
                case "{class}":
                    Value2 = Player.Inventory.Items.FirstOrDefault
                    (i => i.Category == "Class" && i.IsEquipped)?.Name;
                    break;
                case "{nr10}":
                    Value2 = Player.Inventory.Items.FirstOrDefault
                    (i => i.Category == "Class" && i.Quantity < 300_000)?.Name ?? "Error";
                    break;
                case "{helm}":
                    Value2 = Player.Inventory.Items.FirstOrDefault
                    (i => i.Category == "Helm" && i.IsEquipped)?.Name ?? "Error";
                    break;
                case "{cape}":
                    Value2 = Player.Inventory.Items.FirstOrDefault
                    (i => i.Category == "Cape" && i.IsEquipped)?.Name ?? "Error";
                    break;
                default:
                    break;
            }
            string RealString = Value2;
            if (RealString.Contains("["))
            {
                RealVar = instance.ResolveVars(Value2);
                RealString = RealVar;
            }
            /*else if (RealString.Contains(".qty"))
            {
                RealString = RealString.Replace(".qty", "");
                var tempitem = Player.TempInventory.Items.FirstOrDefault(i => i.Name.IndexOf(RealString, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (tempitem != null)
                    RealVar = tempitem.Quantity.ToString();
                var invitem = Player.Inventory.Items.FirstOrDefault(i => i.Name.IndexOf(RealString,System.StringComparison.OrdinalIgnoreCase) >=0 );
                if (invitem != null)
                    RealVar = tempitem.Quantity.ToString();
            }*/ //experimental changes for  getting certain item's qty according to its name

            if (!Configuration.Tempvariable.ContainsKey(Value1))
                Configuration.Tempvariable.Add(Value1, RealString);
            else
                Configuration.Tempvariable[Value1] = RealString;

            return Task.FromResult<object>(null);
        }
        private string RealVar;
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(RealVar))
            {
                return $"Variable {Value1}:{Value2} => {RealVar}";
            }
            return $"Variable {Value1}: {Value2}";
        }
    }
}