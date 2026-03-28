using Grimoire.Game;
using Grimoire.Game.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Item
{
    public class CmdBuy : IBotCommand
    {
        public int ShopId
        {
            get;
            set;
        }

        public string ItemName
        {
            get;
            set;
        }

        public int ItemId
        {
            get;
            set;
        }

        public int ShopItemId
        {
            get;
            set;
        }

        public int Qty
        {
            get;
            set;
        } = 1;

        public bool ByID
        {
            get;
            set;
        } = false;

        public bool SafeRelogin
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Transaction;
            string ItemName = instance.ResolveVars(this.ItemName);
            
                await instance.WaitUntil(() => 
                World.IsActionAvailable(LockActions.BuyItem) && 
                World.IsActionAvailable(LockActions.LoadShop),timeout:3);
                //Shop.ResetShopInfo(); //No Longer needed since Load will reset if shop was never loaded
                await Player.ExitCombat(2000); //To ensure out of combat before load shop
                Shop.Load(ShopId);
                await instance.WaitUntil(() => Shop.IsShopLoaded,interval:500);
                InventoryItem i = Player.Inventory.Items.FirstOrDefault((InventoryItem item) => item.Name.EqualsIgnoreCase(ItemName));
                if (ByID)
                    Shop.BuyItemQty(itemId: ItemId, shopItemId: ShopItemId, qty: Qty);
                else
                    Shop.BuyItemQty(name: ItemName, qty: Qty);
                if (i != null)
                    await instance.WaitUntil(() => Player.Inventory.Items.FirstOrDefault((InventoryItem it) => it.Name.EqualsIgnoreCase(ItemName)).Quantity != i.Quantity, timeout: 2);
                else
                    await instance.WaitUntil(() => Player.Inventory.Items.FirstOrDefault((InventoryItem it) => it.Name.EqualsIgnoreCase(ItemName)) != null, timeout: 2);
            
        }

        public override string ToString()
        {
            string Qty = this.Qty > 0 ? this.Qty.ToString() : "Ma";
            string text = $"Buy item [{Qty}x] {ItemName}";
            if (ByID)
            {
                text = $"Buy item [{Qty}x] {ItemId} : {ShopItemId}";
            }
            return text;
        }
    }
}