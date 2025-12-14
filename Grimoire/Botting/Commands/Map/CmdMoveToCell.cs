using Grimoire.Game;
using Grimoire.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Map
{
    public class CmdMoveToCell : IBotCommand
    {
        public string Cell
        {
            get;
            set;
        }

        public string Pad
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            string Cell = instance.IsVar(this.Cell) ? Configuration.Tempvariable[instance.GetVar(this.Cell)] : this.Cell;
            string Pad = instance.IsVar(this.Pad) ? Configuration.Tempvariable[instance.GetVar(this.Pad)] : this.Pad;

            // BotData.BotState = BotData.State.Others;
            while (!Player.Cell.Equals(Cell, StringComparison.OrdinalIgnoreCase))
            {
                Player.MoveToCell(Cell, Pad);
                await Task.Delay(500);
            }
            BotData.BotCell = Cell;
            BotData.BotPad = Pad;
        }

        public override string ToString()
        {
            return "Move to cell: " + Cell + ", " + Pad;
        }
    }

    public class CmdMoveToCell2 : IBotCommand
    {
        private static CancellationTokenSource cts;
        public string Cell
        {
            get;
            set;
        }

        public string Pad
        {
            get;
            set;
        }
        public string target { get; set; } = "*";
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool stop { get; set; } = false;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int maxcell { get; set; } = 5;
        
        public async Task Execute(IBotEngine instance)
        {
            List<string> Cell = instance.ResolveVars(this.Cell).Split(',').ToList();
            string[] Pad = instance.ResolveVars(this.Pad).Split(',');
			string target = instance.ResolveVars(this.target);
            if (stop)
                cts?.Cancel();
            else
            {
                cts?.Cancel();
                cts = new CancellationTokenSource();
                CancellationToken token = cts.Token;
				if (!string.IsNullOrEmpty(target) && target != "*" )
					Cell = World.GetMonsterCells(target);
                int _maxcell;
                if (Cell.Count >= maxcell)
                    _maxcell = maxcell;
                else
                    _maxcell = Cell.Count;
                _ = Task.Run(async () =>
                {
                    int i = 0;
                    int botdelay = instance.Configuration.BotDelay;
                    while (!token.IsCancellationRequested && instance.IsRunning)
                    {
                        if (World.IsMonsterAvailable(target))
                        {
                            // while monster is Alive within ur cell
                            // checks every 50ms up to 15 times then back to top loop
                            await instance.WaitUntil(() => !World.IsMonsterAvailable(target), interval: Math.Max(botdelay,50));
                            continue;
                        }

                        if (Player.Cell != Cell[i])
                        {
                            string pad = (i < Pad.Length) ? Pad[i] : "Left";
                            Player.MoveToCell(Cell[i], pad);
                            LogForm.Instance.devDebug($"Cell : {Cell[i]} [{i + 1}/{_maxcell}]");
                        }

                        // This loop is needed to wait init monster loaded from clientside
                        // Otherwise it will keep jumping nonstop
                        await instance.WaitUntil(() => World.IsMonsterAvailable(target), interval: 50);
                        if (++i >= _maxcell)
                            i = 0;
                    }
                });
            }
        }

        public override string ToString()
        {
            if (stop)
				return $"Stop Cell Jump";
            if (target != "*")
				return $"Start Jump and Find : {target}";
            return $"Start Jump between : {string.Join("|", Cell)}";
        }
    }
}