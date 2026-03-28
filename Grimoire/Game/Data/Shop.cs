using Grimoire.Botting;
using Grimoire.Networking;
using Grimoire.Tools;
using Grimoire.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimoire.Game.Data
{
    public class Shop
    {
        public static Shop Instance = new Shop();

        [JsonProperty("sName")]
        public string Name
        {
            get;
            set;
        }

        [JsonProperty("ShopID")]
        public int Id
        {
            get;
            set;
        }

        [JsonProperty("items")]
        public List<InventoryItem> Items
        {
            get;
            set;
        }

        public string Location
        {
            get;
            set;
        }

        public static bool IsShopLoaded => Flash.Call<bool>("IsShopLoaded", new string[0]);

        public static void BuyItem(string name)
        {
            Flash.Call("BuyItem", name);
        }

        public static void BuyItemQty(string name, int qty)
        {

            if (qty < 0)
            {
                dynamic sitem = GetShopItemData(name);
                if (sitem != null)
                    qty = MaximumShopBuys(sitem);
                if (qty == 0)
                {
                    LogForm.Instance.devDebug($"[MaxBuy] Buy skipped due to max/requirement not meet");
                    return;
                }
            }
            Flash.Call("BuyItemQty", new string[] { name, qty.ToString() });
        }
        public static void BuyItemQty(int itemId, int shopItemId, int qty)
        {
            if (qty < 0)
            {
                dynamic sitem = GetShopItemData(itemId, shopItemId);
                if (sitem != null)
                    qty = MaximumShopBuys(sitem);
                
                if (qty == 0)
                {
                    LogForm.Instance.devDebug($"[MaxBuy] Buy skipped due to max/requirement not meet");
                    return;
                }
            }
            Flash.Call("BuyItemQtyById", new string[] { qty.ToString(), itemId.ToString(), shopItemId.ToString() });
        }
		
        #region Maximum_buy_stuff
        //code extracted from SKuA Corebots
        private static dynamic GetShopItemData(int itemID, int shopItemID = 0)
        {
            return GetShopItemData(item =>
            item?.ItemID == itemID,
            shopItemID);
        }
        private static dynamic GetShopItemData(string itemName, int shopItemID = 0)
        {
            return GetShopItemData(item =>
            itemName.EqualsIgnoreCase((string)item?.sName),
            shopItemID);
        }
        public static int MaximumShopBuys(dynamic shopItem)
        {
            if (shopItem == null)
                return 0;

            var owned = Player.Inventory.Items.FirstOrDefault(x => x.Id == (int)shopItem.ItemID);

            if ((string)shopItem.sES == "ar")
                return 1;

            int stackRemaining = owned != null
                ? (int)shopItem.iStk - owned.Quantity
                : (int)shopItem.iStk;

            int perBuy = (int)shopItem.iQty;

            // berapa kali bisa beli berdasarkan stack
            int stackLimit = stackRemaining / perBuy;

            if (stackLimit < 1)
                return 0;

            int maxBuys = stackLimit;

            // ===== Currency Limit =====
            if ((int)shopItem.iCost > 0)
            {
                int currency = (int)shopItem.bCoins == 1 ? Player.Coins : Player.Gold;
                int currencyLimit = currency / (int)shopItem.iCost;

                maxBuys = Math.Min(maxBuys, currencyLimit);
            }

            // ===== Merged Limit =====
            if (shopItem.turnin != null)
            {
                foreach (var merge_req in shopItem.turnin)
                {
                    var material = Player.Inventory.Items
                        .FirstOrDefault(x => x.Id == (int)merge_req.ItemID);

                    if (material == null)
                        return 0;

                    int materialLimit = material.Quantity / (int)merge_req.iQty;

                    if (materialLimit == 0)
                        return 0;

                    maxBuys = Math.Min(maxBuys, materialLimit);
                }
            }
            maxBuys *= perBuy;

            return Math.Min(maxBuys, 100000);
        }
        private static dynamic GetShopItemData(Func<dynamic, bool> match, int shopItemID = 0)
        {
            dynamic[] shopItems = Flash.Instance
                .GetGameObject<dynamic[]>("world.shopinfo.items");

            if (shopItems == null)
                return null;

            foreach (dynamic item in shopItems)
            {
                if (match(item) && (shopItemID == 0 || item?.ShopItemID == shopItemID))
                {
                    return item;
                }
            }

            return null;
        }
        #endregion


        public static void ResetShopInfo()
        {
            Flash.Call("ResetShopInfo", new string[0]);
        }
        private static int previousShop = 0;
        public static void Load(int id)
        {
            if (previousShop != id)
            {
                ResetShopInfo();
                previousShop = id;
            }
            Flash.Call("LoadShop", id.ToString());
        }

        public static void SellItem(string name)
        {
            Flash.Call("SellItem", name);
        }
        public static void SellItem(string name, int qty = 1)
        {
            var item = Player.Inventory.Items.Find(i => i.Name.EqualsIgnoreCase(name));
            if (item == null)
                return;

            if (qty <= 0)
                qty += item.Quantity; // 0 = sell whole stack, -1 = Leave 1 stack
            if (qty > 0)
            {
                _ = Proxy.Instance.SendToServer($"%xt%zm%sellItem%{World.RoomId}%{item.Id}%{qty}%");
            }
        }

        public static void LoadHairShop(string id)
        {
            Flash.Call("LoadHairShop", id);
        }

        public static void LoadHairShop(int id)
        {
            Flash.Call("LoadHairShop", id.ToString());
        }

        public static void LoadArmorCustomizer()
        {
            Flash.Call("LoadArmorCustomizer", new string[0]);
        }
    }
}