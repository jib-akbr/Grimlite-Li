using Grimoire.Game;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Item
{
    public class CmdBankTransfer : IBotCommand
    {
        public bool TransferFromBank
        {
            get;
            set;
        }

        public string ItemName
        {
            get;
            set;
        }

        public string EnhancementName
        {
            get;
            set;
        }

        public int EnhancementID
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Others;
            
            // Ensure bank is loaded
            Player.Bank.LoadItems();
            await Task.Delay(1000);
            
            string ItemName = (instance.IsVar(this.ItemName) ? Configuration.Tempvariable[instance.GetVar(this.ItemName)] : this.ItemName);
            
            // Parse enhancement from item name if using | delimiter (e.g., "ItemName|Wizard" or "ItemName|6")
            string enhName = null;
            int enhId = 0;
            bool useEnhancement = false;
            
            if (ItemName.Contains("|"))
            {
                var parts = ItemName.Split('|');
                ItemName = parts[0].Trim();
                if (parts.Length > 1)
                {
                    string enhPart = parts[1].Trim();
                    useEnhancement = true;
                    // Try to parse as number first (enhancement ID)
                    if (int.TryParse(enhPart, out int parsedId))
                    {
                        enhId = parsedId;
                    }
                    else
                    {
                        // Otherwise treat as enhancement name
                        enhName = enhPart;
                    }
                }
            }
            
            // Enhancement-based transfer (by CharItemID or EnhancementID)
            if (useEnhancement)
            {
                if (TransferFromBank)
                {
                    // Find item in bank with matching enhancement
                    var bankItem = Player.Bank.Items.FirstOrDefault(i => 
                        i.Name.Equals(ItemName, System.StringComparison.OrdinalIgnoreCase) &&
                        (enhId > 0 ? i.Enhancement == enhId : 
                         (!string.IsNullOrEmpty(enhName) && GetEnhancementName(i.Enhancement).Equals(enhName, System.StringComparison.OrdinalIgnoreCase))));
                    
                    if (bankItem != null)
                    {
                        Player.Bank.TransferFromBankByID(bankItem.CharItemId);
                        await Task.Delay(500);
                    }
                }
                else
                {
                    // Find item in inventory with matching enhancement
                    var invItem = Player.Inventory.Items.FirstOrDefault(i => 
                        i.Name.Equals(ItemName, System.StringComparison.OrdinalIgnoreCase) &&
                        (enhId > 0 ? i.Enhancement == enhId : 
                         (!string.IsNullOrEmpty(enhName) && GetEnhancementName(i.Enhancement).Equals(enhName, System.StringComparison.OrdinalIgnoreCase))));
                    
                    if (invItem != null)
                    {
                        Player.Bank.TransferToBankByID(invItem.CharItemId);
                        await Task.Delay(500);
                    }
                }
            }
            // Standard name-based transfer
            else if (TransferFromBank)
            {
                if (Player.Bank.GetItemByName(ItemName) != null)
                {
                    Player.Bank.TransferFromBank(ItemName);
                    //await instance.WaitUntil(() => Player.Inventory.ContainsItem(ItemName, "*"));
                    await Task.Delay(500);
                }
            }
            else if (Player.Inventory.GetItemByName(ItemName) != null)
            {
                Player.Bank.TransferToBank(ItemName);
                //await instance.WaitUntil(() => !Player.Inventory.ContainsItem(ItemName, "*"));
                await Task.Delay(500);
            }
        }
        
        private string GetEnhancementName(int enhId)
        {
            // Check special Luck enhancements
            if (Game.Data.InventoryItem.EnhancementNames.ContainsKey(enhId))
                return Game.Data.InventoryItem.EnhancementNames[enhId];
            
            // Check forge enhancements
            if (System.Enum.IsDefined(typeof(Game.Data.InventoryItem.forgeID), enhId))
                return ((Game.Data.InventoryItem.forgeID)enhId).ToString();
            
            return "Unenhanced";
        }

        public override string ToString()
        {
            return (TransferFromBank ? "Bank -> Inv: " : "Inv -> Bank: ") + ItemName;
        }
    }
}