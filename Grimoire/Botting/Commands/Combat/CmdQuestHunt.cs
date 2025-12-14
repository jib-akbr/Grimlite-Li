using Grimoire.Botting.Commands.Map;
using Grimoire.Game.Data;
using Grimoire.Game;
using System.Threading.Tasks;
using System.Collections.Generic;
using Grimoire.UI;
using System;
using System.Linq;
using Grimoire.Networking;
using System.Text.RegularExpressions;

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
            string[] items = instance.ResolveVars(ItemName).Split(',');
            string[] monsters = instance.ResolveVars(Monster).Split(',');
            string[] quantities = instance.ResolveVars(Quantity).Split(',');
            bool doQuest = QID != 0;
            #region quest-based hunt
            req = null;
            if (doQuest)
            {

                if (!Player.Quests.QuestTree.Exists(q => q.Id == QID))
                {
                    Player.Quests.Load(QID);
                    await instance.WaitUntil(() => Player.Quests.QuestTree.Any((Game.Data.Quest q) => q.Id == QID));
                }

                Game.Data.Quest quest = Player.Quests.Quest(QID);
                int progress = Player.Quests.progress(quest.Id);
                //int.Parse(Flash.CallGameFunction2("world.getQuestValue", quest.ISlot));

                //Checks if the quest require questchains
                if (progress >= quest.IValue && quest.ISlot > 0 && quest.IsNotRepeatable)
                    return;

                if (!quest.IsInProgress)
                {
                    quest.Accept();
                    await instance.WaitUntil(() => !Player.Quests.IsInProgress(QID), timeout: 3);
                }

                var reqs = quest.RequiredItems;
                if (items.Length < reqs.Count)
                    Array.Resize(ref items, reqs.Count);
                if (monsters.Length < reqs.Count)
                    Array.Resize(ref monsters, reqs.Count);

                await joinmap(Map, instance);

                for (int i = 0; i < reqs.Count && instance.IsRunning; i++)
                {
                    string name = reqs[i].Name;
                    string qty = reqs[i].Quantity.ToString();
                    req = reqs[i];

                    if (items[i] == null)
                        items[i] = "";

                    LogForm.Instance.devDebug($"Name = {name} | Qty = {qty}");
                    // if (!Player.Map.Equals(Map.Split('-')[0].ToLower()))


                    if (int.TryParse(items[i], out int mapitemid))
                        await getMap(mapitemid, qty);
                    else
                        await hunt(name, qty, monsters[i] ?? "*", instance);
                }
                if (quest.CanComplete)
                    quest.Complete();
                await Task.Delay(600);
                return;
            }
            #endregion
            #region non-quest
            await joinmap(Map, instance);
            for (int i = 0; i < items.Length && instance.IsRunning; i++)
            {
                string qty = i < quantities.Length ? quantities[i] : "1";
                string monster = i < monsters.Length ? monsters[i] : "*";

                if (itemCollected(items[i], quantities[i]))
                    continue;

                // if (!Player.Map.Equals(Map.Split('-')[0].ToLower()))
                if (int.TryParse(items[i], out int mapitemid))
                    await getMap(mapitemid, qty);
                else
                    await hunt(items[i], qty, monster, instance);

                await Task.Delay(600);
            }
            #endregion
        }
        private static InventoryItem req = null; //used only for doQuest 
        async Task hunt(string item, string qty, string monster, IBotEngine instance)
        {
            List<string> targetCell = World.GetMonsterCells(monster);
            int _maxcell;
            if (targetCell.Count >= maxcell)
                _maxcell = maxcell;
            else
                _maxcell = targetCell.Count;
            CmdKill kill = new CmdKill
            {
                Monster = monster,
            };
            if (targetCell.Count > 0)
            {
                int i = 0;
                while (!itemCollected(item, qty) && instance.IsRunning)
                {
                    //LogForm.Instance.devDebug("is it bugging??");
                    if (World.IsMonsterAvailable(monster))
                    {
                        Player.AttackMonster(monster);
                        await kill.Execute(instance);
                        continue;
                    }
                    if (Player.Cell != targetCell[i])
                    {
                        Player.MoveToCell(targetCell[i], "Left");
                        LogForm.Instance.devDebug($"Cell : {targetCell[i]} [{i + 1}/{_maxcell}]");
                    }
                    await instance.WaitUntil(() => World.IsMonsterAvailable(monster), interval: 50);
                    if (++i >= _maxcell)
                        i = 0;
                }
            }
        }

        async Task getMap(int mapitemid, string sqty)
        {
            Player.MoveToCell("Cut1", "Left");
            await Task.Delay(1000);
            if (!World.Cells.Contains("Cut1"))
                Player.MoveToCell("Enter", "Spawn");

            int qty = int.Parse(sqty);

            for (int i = -1; i < qty; i++) //extra 1 attempt for getting map anticipating failure
            {
                await Proxy.Instance.SendToServer($"%xt%zm%getMapItem%1%{mapitemid}%");
                await Task.Delay(600);

                if (!Player.recentMapItem.TryGetValue(mapitemid, out string itemName) || itemName?.Equals("blank") == true)
                    continue;
                if (itemCollected(itemName, sqty))
                    break;
            }
        }

        bool itemCollected(string id, string qty)
        {

            if (req != null) //Handles quest.reqs item
                return itemCollected(req);

            return Player.TempInventory.ContainsItem(id, qty)
                || Player.Inventory.ContainsItem(id, qty);
        }
        bool itemCollected(InventoryItem item)
        {
            //Finds containsItem but with item.id instead of Name
            //Cuz some quest may have same itemName, this is useful for Bulk GhostAccepted quests
            if (item.IsTemporary)
                return Player.TempInventory.ContainsItem(item);
            return Player.Inventory.ContainsItem(item);
        }

        /*List<string> GetMonsterCells(string monsterName)
        {
            List<Monster> monMap = World.GetAllMonsters();

            //1. Collect all monster, then ordered by most amount within a cell
            if (monsterName == "*")
            {
                return monMap
                    .Where(m => !string.IsNullOrEmpty(m.cell))
                    .GroupBy(m => m.cell, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .ToList();
            }

            // Logical OR filtering
            Func<Monster, bool> finalPredicate = m => false;

            // Filtering with MonId
            Match match = Regex.Match(monsterName, @"^id['.:-](?<Id>\d+)$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["Id"].Value, out int targetId))
            {
                //filter with ID when matched with "id." format
                finalPredicate = m => m.MonMapID == targetId;
            }
            else //otherwise filter with name contains 
            {
                finalPredicate = m => m.Name.IndexOf(monsterName, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            //2. Collect monsters that contains its name
            List<string> targetCells = monMap
                .Where(finalPredicate)
                .GroupBy(m => m.cell, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count()) // cell with most monster 
                .Select(g => g.Key)
                .ToList();
            return targetCells;
        }*/

        async Task joinmap(string map, IBotEngine instance)
        {
            CmdJoin join = new CmdJoin
            {
                Map = map,
                Cell = Cell,
                Pad = Pad,
                Try = 3
            };
            await join.Execute(instance);
        }

        public override string ToString()
        {
            string[] items = ItemName.Split(',');
            string[] quantities = Quantity.Split(',');
            string[] monsters = Monster.Split(',');
            List<string> _parts = new List<string>();
            if (QID != 0)
            {
                int _maxlen = Math.Max(items.Length, monsters.Length);
                for (int i = 0; i < _maxlen; i++)
                {
                    string item = (i < items.Length) ? items[i].Trim() : "";
                    string mob = (i < monsters.Length) ? monsters[i].Trim() : "";

                    if (string.IsNullOrEmpty(item) && string.IsNullOrEmpty(mob))
                    {
                        _parts.Add("Blank");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(item))
                        _parts.Add(item);
                    else if (!string.IsNullOrEmpty(mob))
                        _parts.Add($"{mob}*");
                }
                return $"Quest-{QID} items:{string.Join(" | ", _parts)}";
            }

            for (int i = 0; i < items.Length; i++)
            {
                //parts += $"{i + 1}-{items[i]} x{quantities[i]} [{monsters[i]}]";
                _parts.Add($"{i + 1}-{items[i]} x{quantities[i]} [{monsters[i]}]");
            }
            return $"Hunt : {string.Join(" | ", _parts)}";
        }

    }
}
