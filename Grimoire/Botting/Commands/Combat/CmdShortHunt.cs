using Grimoire.Botting.Commands.Map;
using Grimoire.Game;
using Grimoire.Tools;
using Grimoire.UI;
using System;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Combat
{
    class CmdShortHunt : IBotCommand
    {
        public string Map { get; set; }
        public string Cell { get; set; }
        public string Pad { get; set; }
        public string Monster { get; set; }
        public string SkillSet { get; set; } = "";
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
            string[] _Cells = instance.ResolveVars(Cell).Split(',');
            string[] _pad = instance.ResolveVars(Pad).Split(',');

            LogForm.Instance.AppendDebug($"[CmdShortHunt] Starting hunt for {_Items}x{_Qty} (Monster: {Monster}) on map {_Map}");

            if (ItemType == ItemType.Items)
                if (Player.Inventory.ContainsItem(_Items, _Qty)) { LogForm.Instance.AppendDebug($"[CmdShortHunt] Already have {_Items}x{_Qty}"); return; }
                else
                if (Player.TempInventory.ContainsItem(_Items, _Qty)) { LogForm.Instance.AppendDebug($"[CmdShortHunt] Already have {_Items}x{_Qty} in temp"); return; }

            CmdJoin join = new CmdJoin
            {
                Map = _Map,
                Cell = _Cells[0],
                Pad = _pad[0]
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
                AntiCounter = AntiCounter,
                SkillSet = SkillSet
            };


            LogForm.Instance.AppendDebug($"[CmdShortHunt] Starting cell monitor with {_Cells.Length} cells");
            bool huntComplete = false;
            var monitorTask = Task.Run(async () =>
            {
                int i = 0;
                DateTime lastCellChange = DateTime.Now;
                while (instance.IsRunning && !huntComplete)
                {
                    if (!Player.Map.Equals(_Map.Split('-')[0]))
                    {
                        LogForm.Instance.AppendDebug($"[CmdShortHunt] Map change detected, stopping monitor");
                        return;
                    }

                    // Periodically cycle to next cell (every 10-15 seconds or when no monster available)
                    bool shouldChangeCell = false;
                    if (DateTime.Now - lastCellChange > TimeSpan.FromSeconds(12))
                    {
                        shouldChangeCell = true;
                        LogForm.Instance.AppendDebug($"[CmdShortHunt] Time to cycle cell (timeout)");
                    }
                    else if (!World.IsMonsterAvailable(Monster))
                    {
                        shouldChangeCell = true;
                        LogForm.Instance.AppendDebug($"[CmdShortHunt] No monster available, cycling to next cell");
                    }

                    if (shouldChangeCell)
                    {
                        if (++i >= _Cells.Length)
                            i = 0;
                        lastCellChange = DateTime.Now;
                    }

                    // Move to current target cell if not there
                    if (Player.Cell != _Cells[i])
                    {
                        string pad = (i < _pad.Length) ? _pad[i] : "Left";
                        LogForm.Instance.AppendDebug($"[CmdShortHunt] Moving to cell {_Cells[i]} pad {pad}");
                        Player.MoveToCell(_Cells[i], pad);
                    }
                    
                    await Task.Delay(500);
                }
            });

            LogForm.Instance.AppendDebug($"[CmdShortHunt] Starting kill loop");
            await killFor.Execute(instance);

            huntComplete = true;
            LogForm.Instance.AppendDebug($"[CmdShortHunt] Hunt complete, waiting for monitor to finish");
            await monitorTask;
            LogForm.Instance.AppendDebug($"[CmdShortHunt] Monitor finished, hunt is done");
        }

        public override string ToString()
        {
            string itemType = ItemType == ItemType.Items ? "Items" : "Temps";
            return $"Hunt {itemType} {Quantity}x {ItemName}";
        }

    }
}
