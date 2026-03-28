using Grimoire.Botting;
using Grimoire.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Game.Data
{
    public class Inventory
    {
        public List<InventoryItem> Items => Flash.Call<List<InventoryItem>>("GetInventoryItems", new string[0]);

        public InventoryItem GetItemByName(string name)
        {
            return Flash.Call<InventoryItem>("GetInventoryItemByName", name);
        }

        public int MaxSlots => Flash.Call<int>("InventorySlots", new string[0]);

        public int UsedSlots => Flash.Call<int>("UsedInventorySlots", new string[0]);

        public int AvailableSlots => MaxSlots - UsedSlots;

        public bool ContainsItemX(string name, string quantity)
        {
            InventoryItem inventoryItem = Items.FirstOrDefault((InventoryItem i) => i.Name.EqualsIgnoreCase(name));
            if (inventoryItem != null)
            {
                if (!(quantity == "*"))
                {
                    return inventoryItem.Quantity >= int.Parse(quantity);
                }
                return true;
            }
            return false;
        }
        #region waitItemTaken
       /* public async Task<bool> WaitForItem(string itemName, int attempts = 3, int delayMS = 1000)
        {
            for (int i = 0; i < attempts; i++)
            {
                bool found = Player.Inventory.Items.Any(it => it.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));

                if (found)
                    return true;

                await Task.Delay(delayMS);
            }
            return false;
        }
        public async Task<bool> WaitForItemId(int itemId, int attempts = 3, int delayMS = 1000)
        {
            for (int i = 0; i < attempts; i++)
            {
                bool found = Player.Inventory.Items.Any(it => it.Id == itemId);

                if (found)
                    return true;

                await Task.Delay(delayMS);
            }
            return false;
        }*/
        #endregion
        
		public bool ContainsItem(string itemName, string quantity = "*")
        {
            InventoryItem item = Player.Inventory.GetItemByName(itemName);
            if (item == null)
            {
                return false;
            }
            else if (int.TryParse(quantity, out int qty))
            {
                if (item.Category != "Class" && qty > item.MaxStack)
                    qty = item.MaxStack;
                return item.Quantity >= qty;
            }
            return true;
        }
		
        public bool ContainsItem(int id, int qty)
        {
            InventoryItem item = Items.FirstOrDefault((InventoryItem i) => i.Id == id);
            if (item == null)
            {
                return false;
            }
            else if (item.Category != "Class" && qty > item.MaxStack)
            {
                qty = item.MaxStack;
            }
            return item.Quantity >= qty;
            //return Items.FirstOrDefault((InventoryItem it) => it.Id == id)?.Quantity >= qty;
        }
		
        public int GetItemQty(string format)
        {//not yet used...
            format = format.Replace(".qty", "");
            return Items.FirstOrDefault(
                (InventoryItem i) => i.Name.EqualsIgnoreCase(format)
                )?.Quantity ?? 0;
        }
		
        public bool ContainsItem(InventoryItem item)
        {
            return Items.FirstOrDefault((InventoryItem target) => target.Id == item.Id)?.Quantity >= item.Quantity;
        }

        public bool ContainsMaxItem(string name)
        {
            InventoryItem inventoryItem = Items.FirstOrDefault((InventoryItem i) => i.Name.EqualsIgnoreCase(name));
            if (inventoryItem == null)
                return false;
            return inventoryItem.Quantity >= inventoryItem.MaxStack;
        }
    }
}
