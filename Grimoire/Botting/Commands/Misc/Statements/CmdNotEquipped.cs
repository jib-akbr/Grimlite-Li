using Grimoire.Game;
using Grimoire.Game.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class CmdNotEquipped : StatementCommand, IBotCommand
    {
        public CmdNotEquipped()
        {
            Tag = "Item";
            Text = "Is not equipped";
        }

        public Task Execute(IBotEngine instance)
        {
            string itemName = string.IsNullOrEmpty(Value1) 
                ? Value1 
                : (instance.IsVar(Value1) 
                    ? Configuration.Tempvariable[instance.GetVar(Value1)] 
                    : Value1);
            
            InventoryItem item = Player.Inventory.Items.Find(x => x.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)) 
                ?? new InventoryItem();

            bool isEquipped = false;

            // For classes, check against EquippedClass
            if (item.Category == "Class")
            {
                isEquipped = Player.EquippedClass.Equals(itemName, StringComparison.OrdinalIgnoreCase);
            }
            // For weapons, try Flash property first, fallback to inventory
            else if (InventoryItem.Weapons.Contains(item.Category))
            {
                string equipped = Player.EquippedWeapon ?? "";
                isEquipped = !string.IsNullOrEmpty(equipped) && equipped.Equals(itemName, StringComparison.OrdinalIgnoreCase);
                
                // Fallback to inventory check if Flash property is empty
                if (!isEquipped && item.Id > 0)
                {
                    var invItem = Player.Inventory.Items.FirstOrDefault(it => it.Id == item.Id && it.IsEquipped);
                    isEquipped = invItem != null;
                }
            }
            // For armor, try Flash property first, fallback to inventory
            else if (item.Category == "Armor")
            {
                string equipped = Player.EquippedArmor ?? "";
                isEquipped = !string.IsNullOrEmpty(equipped) && equipped.Equals(itemName, StringComparison.OrdinalIgnoreCase);
                
                if (!isEquipped && item.Id > 0)
                {
                    var invItem = Player.Inventory.Items.FirstOrDefault(it => it.Id == item.Id && it.IsEquipped);
                    isEquipped = invItem != null;
                }
            }
            // For helm, try Flash property first, fallback to inventory
            else if (item.Category == "Helm")
            {
                string equipped = Player.EquippedHelm ?? "";
                isEquipped = !string.IsNullOrEmpty(equipped) && equipped.Equals(itemName, StringComparison.OrdinalIgnoreCase);
                
                if (!isEquipped && item.Id > 0)
                {
                    var invItem = Player.Inventory.Items.FirstOrDefault(it => it.Id == item.Id && it.IsEquipped);
                    isEquipped = invItem != null;
                }
            }
            // For cape, try Flash property first, fallback to inventory
            else if (item.Category == "Cape")
            {
                string equipped = Player.EquippedCape ?? "";
                isEquipped = !string.IsNullOrEmpty(equipped) && equipped.Equals(itemName, StringComparison.OrdinalIgnoreCase);
                
                if (!isEquipped && item.Id > 0)
                {
                    var invItem = Player.Inventory.Items.FirstOrDefault(it => it.Id == item.Id && it.IsEquipped);
                    isEquipped = invItem != null;
                }
            }
            else
            {
                // For other items (potions, etc.), check IsEquipped flag
                isEquipped = item.IsEquipped;
            }

            if (isEquipped)
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Item is not equipped: " + Value1;
        }
    }
}