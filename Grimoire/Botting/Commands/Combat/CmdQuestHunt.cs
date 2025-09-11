using Grimoire.Botting.Commands.Map;
using Grimoire.Game.Data;
using Grimoire.Game;
using Grimoire.Tools;
using System.Threading.Tasks;
using System.Collections.Generic;
using Grimoire.UI;
using System.Web.UI;
using System;
using System.Linq;

namespace Grimoire.Botting.Commands.Combat
{
    class CmdQuestHunt : IBotCommand
    {
        public string Map { get; set; }
        public string Cell { get; set; }
        public string Pad { get; set; }
        public string Monster { get; set; }
        public string _ItemName { get; set; }
        public ItemType _ItemType { get; set; }
        public string _Quantity { get; set; }
        public Game.Data.Quest Quest
        {
            get;
            set;
        }
        public string KillPriority { get; set; } = "";
        public int DelayAfterKill { get; set; } = 50;
        public bool BlankFirst { get; set; }

        public async Task Execute(IBotEngine instance)
        {
            string[] items = _ItemName.Split(',');
            string[] quantities = _Quantity.Split(',');
            if (_ItemType == ItemType.Items)
            {
                return;
            }

            while (!Player.Map.Equals(Map.Split('-')[0]))
            {
                CmdJoin join = new CmdJoin
                {
                    Map = this.Map,
                    Cell = this.Cell,
                    Pad = this.Pad
                };

                if (BlankFirst)
                {
                    string[] safeCell = ClientConfig.GetValue(ClientConfig.C_SAFE_CELL).Split(',');
                    Player.MoveToCell(safeCell[0], safeCell[1]);
                    await instance.WaitUntil(() => Player.CurrentState != Player.State.InCombat, timeout: 3);
                    await Task.Delay(1000);
                }

                await join.Execute(instance);
            }
            if (!Player.Cell.Equals(Cell)) Player.MoveToCell(Cell, Pad);

            await Task.Delay(1000);
            List<Monster> mon = World.GetAllMonsters();
            for (int i = 0; i < items.Length; i++)
            {
                int qty = int.Parse(quantities[i]);
                if (int.TryParse(items[i], out _))
                {
                    getMap(items[i], qty);
                } else
                {
                    
                }
            }
            CmdKillFor killFor = new CmdKillFor
            {
                Monster = this.Monster,
                ItemName = this._ItemName,
                ItemType = this._ItemType,
                Quantity = this._Quantity,
                DelayAfterKill = this.DelayAfterKill,
                KillPriority = this.KillPriority,
            };

            await killFor.Execute(instance);
        }

        void getMap(string id, int qty = 1)
        {
            //List<TempItem> initialItems = Player.TempInventory.Items;
            //TempItem newItem = null;

            for (int i = -2; i < qty; i++) //extra 2 attempt for getting map anticipating failure
            {
                Player.GetMapItem(id);
                Task.Delay(600);
                //if (newItem != null)
                //    continue;

                //List<TempItem> newItems = Player.TempInventory.Items?.Except(initialItems ?? Enumerable.Empty<TempItem>()).ToList();
                //newItem = newItems?.FirstOrDefault(x => x.Id == int.Parse(id)) ?? newItems?.FirstOrDefault();
            }

        }

        public override string ToString()
        {
            string itemType = _ItemType == ItemType.Items ? "Items" : "Temps";
            return $"Hunt {itemType} {_Quantity}x {_ItemName}";
        }

    }
}
