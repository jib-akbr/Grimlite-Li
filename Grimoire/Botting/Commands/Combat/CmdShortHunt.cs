using Grimoire.Botting.Commands.Map;
using Grimoire.Game;
using Grimoire.Tools;
using Grimoire.UI;
using System.Threading;
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
            string[] _Cells = instance.ResolveVars(Cell).Split(',');
            string[] _pad = instance.ResolveVars(Pad).Split(',');

            if (itemcollected(_Items, _Qty))
                return;

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
                AntiCounter = AntiCounter
            };


            bool running = true;
            var monitorTask = Task.Run(async () =>
            {
                int i = 0;
                Player.MoveToCell(_Cells[i], _pad[i]);
                while (running && instance.IsRunning)
                {
                    if (!Player.Map.Equals(_Map.Split('-')[0]))
                    {
                        LogForm.Instance.devDebug($"Map change detected, Lock cell stopped");
                        return;
                    }

                    if (World.IsMonsterAvailable(Monster))
                    {
                        // while monster is Alive within ur cell
                        // checks every 100ms up to 15 times then back to top loop
                        await instance.WaitUntil(() => !World.IsMonsterAvailable(Monster), interval: 50);
                        continue;
                    }

                    if (Player.Cell != _Cells[i])
                    {
                        string pad = (i < _pad.Length) ? _pad[i] : "Left";
                        Player.MoveToCell(_Cells[i], pad);
                        //LogForm.Instance.devDebug($"Cell : {_Cells[i]} [{i + 1}/{_Cells.Length}]");
                    }

                    // This loop is needed to wait init monster loaded from clientside
                    // Otherwise it will keep jumping nonstop
                    await instance.WaitUntil(() => World.IsMonsterAvailable(Monster), interval: 50);
                    if (++i >= _Cells.Length)
                        i = 0;
                }
            });
            CancellationTokenSource cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!itemcollected(_Items, _Qty) && instance.IsRunning && Player.IsLoggedIn)
                    await Task.Delay(500);
                cts?.Cancel();
                //LogForm.Instance.devDebug($"Cts Canceled");
            });
            //killFor.setCts(cts);
            await killFor.Execute(instance,cts.Token);
            running = false;
            await monitorTask;

            cts.Dispose();
        }

        private bool itemcollected(string item, string qty)
        {
            if (ItemType == ItemType.Items)
                return Player.Inventory.ContainsItem(item, qty);
            else
                return Player.TempInventory.ContainsItem(item, qty);
        }

        public override string ToString()
        {
			string shortmap = Shorten(Map,5);
            if (int.TryParse(QuestId, out _))
            {
                return $"Quest [{QuestId}]: {shortmap}, [{Monster}]";
            }
            string itemType = ItemType == ItemType.Items ? "Items" : "Temps";
            return $"Hunt {itemType}: {shortmap}, {Quantity}x {ItemName}";
        }
		
		private string Shorten(string text, int max)
		{
			if (string.IsNullOrEmpty(text))
				return text;
		
			return text.Length > max
				? text.Substring(0, max) + "..."
				: text;
		}
    }
}
