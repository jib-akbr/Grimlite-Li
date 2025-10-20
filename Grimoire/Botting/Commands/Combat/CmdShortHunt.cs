﻿using Grimoire.Botting.Commands.Map;
using Grimoire.Game;
using Grimoire.Tools;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Combat
{
    class CmdShortHunt : IBotCommand
    {
        public string Map { get; set; }
        public string Cell { get; set; }
        public string Pad { get; set; }
        public string Monster { get; set; }
        public string ItemName { get; set; }
        public ItemType ItemType { get; set; }
        public string Quantity { get; set; }
        public string KillPriority { get; set; } = "";
        public bool AntiCounter { get; set; } = false;
        public string QuestId { get; set; }
        public int DelayAfterKill { get; set; } = 50;
        public bool BlankFirst { get; set; }

        public async Task Execute(IBotEngine instance)
        {
            string _Items = instance.ResolveVars(ItemName);
            string _Qty = instance.ResolveVars(Quantity);
            string _Map = instance.ResolveVars(Map.ToLower());

            if (ItemType == ItemType.Items)
                if (Player.Inventory.ContainsItem(_Items, _Qty)) return;
            else
                if (Player.TempInventory.ContainsItem(_Items, _Qty)) return;

            CmdJoin join = new CmdJoin
            {
                Map = _Map,
                Cell = Cell,
                Pad = Pad
            };
            while (!Player.Map.Equals(_Map.Split('-')[0]) && instance.IsRunning)
            {
                if (BlankFirst)
                {
                    string[] safeCell = ClientConfig.GetValue(ClientConfig.C_SAFE_CELL).Split(',');
                    Player.MoveToCell(safeCell[0], safeCell[1]);
                    await instance.WaitUntil(() => Player.CurrentState != Player.State.InCombat, timeout: 3);
                    await Task.Delay(1000);
                }
                await join.Execute(instance);
            }
            CmdKillFor killFor = new CmdKillFor
            {
                Monster = Monster,
                ItemName = _Items,
                ItemType = ItemType,
                Quantity = _Qty,
                QuestId = QuestId,
                DelayAfterKill = DelayAfterKill,
                KillPriority = KillPriority,
                AntiCounter = AntiCounter
            };


            bool running = true;
            var monitorTask = Task.Run(async () =>
            {
                while (running && instance.IsRunning)
                {
                    if (!Player.Cell.Equals(Cell))
                        Player.MoveToCell(Cell, Pad);
                    await Task.Delay(1000);
                }
            });

            await killFor.Execute(instance);

            running = false;
            await monitorTask;
        }

        public override string ToString()
        {
            string itemType = ItemType == ItemType.Items ? "Items" : "Temps";
            return $"Hunt {itemType} {Quantity}x {ItemName}";
        }

    }
}
