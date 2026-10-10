using Grimoire.Botting;
using Grimoire.Tools;
using Grimoire.UI;
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
            // if (qty != "*")
            // {
			else if (int.TryParse(qty, out int iqty))
            {
                if (ClientConfig.AutoCorrect && iqty > tempItem.MaxStack)
                    iqty = tempItem.MaxStack;
                return tempItem.Quantity >= iqty;
            }
            return true;
        }

        public bool ContainsItem(InventoryItem item)
        {
            return Items.FirstOrDefault((TempItem target) => target.Id == item.Id)?.Quantity >= item.Quantity;
        }
    }
}