using Grimoire.Game;
using Grimoire.Game.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Item
{
    // Compat shims for bots saved by Grimlite Rev (id-based shop commands).
    public class CmdBuyByID : IBotCommand
    {
        public string ItemID
        {
            get;
            set;
        }

        public string ShopID
        {
            get;
            set;
        }

        public string ShopItemID
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Transaction;
            int shopId = int.Parse(instance.ResolveVars(ShopID));
            int itemId = int.Parse(instance.ResolveVars(ItemID));
            int shopItemId = int.Parse(instance.ResolveVars(ShopItemID));
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.BuyItem));
            Shop.ResetShopInfo();
            Shop.Load(shopId);
            await instance.WaitUntil(() => Shop.IsShopLoaded);
            Shop.BuyItemQty(itemId: itemId, shopItemId: shopItemId, qty: 1);
        }

        public override string ToString()
        {
            return $"Buy item by ID: {ItemID}, {ShopID}, {ShopItemID}";
        }
    }

    public class CmdBuyFastByID : IBotCommand
    {
        public string ItemID
        {
            get;
            set;
        }

        public string ShopID
        {
            get;
            set;
        }

        public string ShopItemID
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Transaction;
            int itemId = int.Parse(instance.ResolveVars(ItemID));
            int shopItemId = int.Parse(instance.ResolveVars(ShopItemID));
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.BuyItem), timeout: 3);
            using (new pauseProvoke(instance.Configuration))
            {
                await Player.ExitCombat(2000);
                Shop.BuyItemQty(itemId: itemId, shopItemId: shopItemId, qty: 1);
            }
        }

        public override string ToString()
        {
            return $"Buy item fast by ID: {ItemID}, {ShopID}, {ShopItemID}";
        }
    }

    public class CmdLoad2 : IBotCommand
    {
        public string ShopId
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Transaction;
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.LoadShop));
            Shop.ResetShopInfo();
            Shop.Load(int.Parse(instance.ResolveVars(ShopId)));
            await instance.WaitUntil(() => Shop.IsShopLoaded);
        }

        public override string ToString()
        {
            return "Load Shop: " + ShopId;
        }
    }

    public class CmdMapItem2 : IBotCommand
    {
        public string ItemId
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Others;
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.GetMapItem));
            Player.GetMapItem(int.Parse(instance.ResolveVars(ItemId)));
            await Task.Delay(500);
        }

        public override string ToString()
        {
            return $"Get map item: {ItemId}";
        }
    }
}
