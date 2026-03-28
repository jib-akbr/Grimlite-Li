using Grimoire.Botting;
using Grimoire.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimoire.Game.Data
{
    public class TempInventory
    {
        public List<TempItem> Items => Flash.Call<List<TempItem>>("GetTempItems", new string[0]);

        public bool ContainsItem(string name, string qty)
        {
            TempItem tempItem = Items.FirstOrDefault((TempItem i) => i.Name.EqualsIgnoreCase(name));
            if (tempItem == null)
            {
                return false;
            }
            if (qty != "*")
            {
                return tempItem.Quantity >= int.Parse(qty);
            }
            return true;
        }

        public bool ContainsItem(InventoryItem item)
        {
            return Items.FirstOrDefault((TempItem target) => target.Id == item.Id)?.Quantity >= item.Quantity;
        }
    }
}