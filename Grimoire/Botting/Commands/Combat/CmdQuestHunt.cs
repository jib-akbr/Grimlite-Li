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
using Grimoire.Networking;

namespace Grimoire.Botting.Commands.Combat
{
    class CmdQuestHunt : IBotCommand
    {
        public string Map { get; set; }
        public string Cell { get; set; }
        public string Pad { get; set; }
        public string Monster { get; set; }
        public string ItemName { get; set; }
        public string Quantity { get; set; }
        public int QID { get; set; } = 0;
        public int maxcell { get; set; } = 10;
        public bool BlankFirst { get; set; } = false;

        public async Task Execute(IBotEngine instance)
        {
            string[] items = ItemName.Split(',');
            string[] quantities = Quantity.Split(',');
            string[] monsters = Monster.Split(',');
            bool doQuest = QID != 0;
            if (doQuest)
            {
                if (!Player.Quests.QuestTree.Exists(q => q.Id == QID))
                {
                    Player.Quests.Load(QID);
                    await instance.WaitUntil(() => Player.Quests.QuestTree.Any((Game.Data.Quest q) => q.Id == QID));
                }

                Game.Data.Quest quest = Player.Quests.QuestTree.Find(q => q.Id == QID);
                int progress = int.Parse(Flash.CallGameFunction2("world.getQuestValue", quest.ISlot));
                if (progress >= quest.IValue)
                    return;

                if (!Player.Quests.IsInProgress(QID))
                {
                    Player.Quests.Accept(QID);
                    await instance.WaitUntil(() => !Player.Quests.IsInProgress(QID), timeout: 1);
                }
                var reqs = quest.RequiredItems;
                for (int i = 0; i < reqs.Count; i++)
                {
                    string name = reqs[i].Name;
                    string qty = reqs[i].Quantity.ToString();
                    LogForm.Instance.AppendDebug($"Name = {name} | Qty = {qty}");
                    if (!Player.Map.Equals(Map.Split('-')[0].ToLower()))
                        await joinmap(Map, instance);
                    if (int.TryParse(items[i], out _))
                        await getMap(items[i], qty);
                    else
                        await hunt(name, qty, monsters[i], instance);
                }
                Player.Quests.Complete(QID);
                await Task.Delay(600);
                return;
            }
            for (int i = 0; i < items.Length; i++)
            {
                if (Player.Quests.CanComplete(QID))
                    break;

                if (itemCollected(items[i], quantities[i]))
                    continue;

                if (!Player.Map.Equals(Map.Split('-')[0].ToLower()))
                    await joinmap(Map, instance);

                if (int.TryParse(items[i], out _))
                    await getMap(items[i], quantities[i]);
                else
                    await hunt(items[i], quantities[i], monsters[i], instance);

                await Task.Delay(500);
            }
            if (doQuest && Player.Quests.CanComplete(QID))
            {
                Player.Quests.Complete(QID);
            }
        }
        async Task hunt(string item, string qty, string monster, IBotEngine instance)
        {
            List<string> targets = GetMonsterCells(monster);
            int _maxcell;
            if (targets.Count >= maxcell)
                _maxcell = maxcell;
            else
                _maxcell = targets.Count;

            CmdKill kill = new CmdKill
            {
                Monster = monster,
            };
            int i = 0;
            if (targets.Count > 0)
            {
                while (!itemCollected(item, qty) && instance.IsRunning)
                {
                    if (World.IsMonsterAvailable(monster))
                    {
                        Player.AttackMonster(monster);
                        await kill.Execute(instance);
                        continue;
                    }
                    else if (targets.Count > 1)
                        Player.MoveToCell(targets[i], "Left");
                    else if (Player.Cell != targets[i])
                    {
                        Player.MoveToCell(targets[i], "Left");
                        await Task.Delay(200);
                    }
                    i++;
                    if (i >= _maxcell)
                        i = 0;
                }
            }
        }

        async Task getMap(string mapitemid, string sqty)
        {
            List<TempItem> previousItems = Player.TempInventory.Items?.ToList() ?? new List<TempItem>();
            TempItem targetItem = null;
            Player.MoveToCell("Cut1", "Left");
            int qty = int.Parse(sqty);
            for (int i = -2; i < qty; i++) //extra 2 attempt for getting map anticipating failure
            {
                await Proxy.Instance.SendToServer($"%xt%zm%getMapItem%1%{mapitemid}%");
                await Task.Delay(600);
                List<TempItem> currentItems = Player.TempInventory.Items?.ToList() ?? new List<TempItem>();

                // Cari item baru (belum ada di previousItems)
                targetItem = currentItems.FirstOrDefault(item =>
                    !previousItems.Any(old => old.Id == item.Id));

                // Kalau tidak ada item baru, mungkin stack item lama bertambah
                if (targetItem == null)
                {
                    targetItem = currentItems.FirstOrDefault(item =>
                    {
                        var oldItem = previousItems.FirstOrDefault(o => o.Id == item.Id);
                        return oldItem != null && item.Quantity > oldItem.Quantity;
                    });
                }
                else
                {
                    if (!Player.recentMapItem.Any(item => item.Id == targetItem.Id))
                        Player.recentMapItem.Add(targetItem);
                }

                // Kalau sudah ada item dan jumlah cukup, break
                if (targetItem != null && targetItem.Quantity >= qty)
                    break;

                // Update snapshot untuk perbandingan berikutnya
                previousItems = currentItems;
            }
        }

        bool itemCollected(string id, string qty)
        {
            if (int.TryParse(id, out int _))
            {
                id = Player.recentMapItem.FirstOrDefault(x => x.Quantity == int.Parse(qty)).ToString();
            }

            if (Player.TempInventory.ContainsItem(id, qty) || Player.Inventory.ContainsItem(id, qty))
            {
                return true;
            }
            return false;
        }

        List<string> GetMonsterCells(string monsterName)
        {
            List<Monster> monMap = World.GetAllMonsters();

            if (monsterName == "*")
            {
                // Semua monster, urutkan berdasarkan cell dengan jumlah terbanyak
                return monMap
                    .Where(m => !string.IsNullOrEmpty(m.cell))
                    .GroupBy(m => m.cell, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .ToList();
            }
            // Ambil semua cell unik tempat monsterName spawn
            List<string> targetCells = monMap
                .Where(m => m.Name != null &&
                            m.Name.IndexOf(monsterName, StringComparison.OrdinalIgnoreCase) >= 0) //&& m.IsAlive)
                .GroupBy(m => m.cell, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count()) // cell dengan monster hidup terbanyak
                .Select(g => g.Key)
                .ToList();
            return targetCells;
        }

        async Task joinmap(string map, IBotEngine instance)
        {
            CmdJoin join = new CmdJoin
            {
                Map = map,
                Cell = Cell,
                Pad = Pad
            };
            while (!Player.Map.Equals(map.Split('-')[0]) && instance.IsRunning)
            {
                if (BlankFirst)
                {
                    string[] safeCell = ClientConfig.GetValue(ClientConfig.C_SAFE_CELL).Split(',');
                    Player.MoveToCell(safeCell[0], safeCell[1]);
                    await instance.WaitUntil(() => Player.CurrentState != Player.State.InCombat, timeout: 3);
                    await Task.Delay(1000);
                }
                await join.Execute(instance);
                await Task.Delay(1500);
            }
            if (!Player.Cell.Equals(Cell)) Player.MoveToCell(Cell, Pad);
            //await Task.Delay(1000);
        }

        public override string ToString()
        {
            string[] items = ItemName.Split(',');
            string[] quantities = Quantity.Split(',');
            string[] monsters = Monster.Split(',');
            string wellShit = "";
            for (int i = 0; i < items.Length; i++)
            {
                wellShit += $"{i + 1}-{items[i]} x{quantities[i]} [{monsters[i]}] ";
            }
			if (QID != 0)
				return $"Quest-{QID} Map items:{string.Join(" | ", items)}";
            return $"Hunt {wellShit}";
        }

    }
}
