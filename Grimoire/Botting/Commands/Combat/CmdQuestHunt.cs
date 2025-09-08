using Grimoire.Botting.Commands.Map;
using Grimoire.Game.Data;
using Grimoire.Game;
using Grimoire.Tools;
using System.Threading.Tasks;
using System.Collections.Generic;
using Grimoire.UI;

namespace Grimoire.Botting.Commands.Combat
{
    class CmdQuestHunt : IBotCommand
    {
        public string Map { get; set; }
        public string Cell { get; set; }
        public string Pad { get; set; }
        public string Monster { get; set; }
        public string ItemName { get; set; }
        public ItemType ItemType { get; set; }
        public string Quantity { get; set; }
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
            string[] items = ItemName.Split(',');
            if (ItemType == ItemType.Items)
            {
                return;
            }

            if (!Player.Map.Equals(Map.Split('-')[0]))
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
                    await instance.WaitUntil(() => Player.CurrentState != Player.State.InCombat, timeout:3);
                    await Task.Delay(1000);
                }

                await join.Execute(instance);
            }
            if (!Player.Cell.Equals(Cell)) Player.MoveToCell(Cell, Pad);
            
            await Task.Delay(1000);


            for (int i = 0; i < items.Length; i++)
            {

            }

            CmdKillFor killFor = new CmdKillFor
            {
                Monster = this.Monster,
                ItemName = this.ItemName,
                ItemType = this.ItemType,
                Quantity = this.Quantity,
                DelayAfterKill = this.DelayAfterKill,
                KillPriority = this.KillPriority,
            };

            await killFor.Execute(instance);
        }

        public override string ToString()
        {
            string itemType = ItemType == ItemType.Items ? "Items" : "Temps";
            return $"Hunt {itemType} {Quantity}x {ItemName}";
        }

    }
}
