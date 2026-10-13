using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.Tools;
using Grimoire.UI;

namespace Grimoire.Botting.Commands.Map
{
	public class CmdJoin : IBotCommand
	{
		public string Map { get; set; }

		public string Cell { get; set; }

		public string Pad { get; set; }

		public int Try { get; set; } = 1;

		public string MapSwf { get; set; } = "";
		private string _Cell;
		private string _Pad;
		private bool _skipcell = false;
		public async Task Execute(IBotEngine instance)
		{
			BotData.BotState = BotData.State.Move;
			//await instance.WaitUntil(() => World.IsActionAvailable(LockActions.Transfer), null, 15);

			_Cell = instance.ResolveVars(Cell);
			_Pad  = instance.ResolveVars(Pad);
			string _Map = instance.ResolveVars(Map);
			//[MAP]-[NUMBER]

			string MapName = _Map.Contains("-") ? _Map.Split('-')[0] : _Map;
			//string MapName = (instance.IsVar(MapName) ? Configuration.Tempvariable[instance.GetVar(MapName)] : MapName);

			string RoomNumber = _Map.Contains("-") ? _Map.Split('-')[1] : "";
			//string RoomNumber = (instance.IsVar(RoomNumber) ? Configuration.Tempvariable[instance.GetVar(RoomNumber)] : RoomNumber);

			int _try = Try;

			if (!MapName.EqualsIgnoreCase(Player.Map))
			{
				if (!int.TryParse(RoomNumber, out int n) && RoomNumber != "")
				{
					Random random = new Random();
					int num = random.Next(1000, 99999);
					RoomNumber = num.ToString();
				}
                // using (new pauseProvoke(instance.Configuration))
                // {
                // }
                // check for MapName, instead of Map 
                holdProvoke(MapName);
                while (_try > 0 && !MapName.EqualsIgnoreCase(Player.Map)) 
                {
					await this.TryJoin(instance, MapName, RoomNumber);
					_try--;
                }
			}

			if (_skipcell)
				return;

            if (MapName.EqualsIgnoreCase(Player.Map))
			{
				if (!Player.Cell.EqualsIgnoreCase(_Cell))
				{
					Player.MoveToCell(_Cell, _Pad);
					await Task.Delay(500);
				}
				World.SetSpawnPoint();
				BotData.BotMap = MapName;
				BotData.BotCell = _Cell;
				BotData.BotPad = _Pad;
			}

			if (MapSwf.Contains(".swf") && !MapSwf.Contains("(.swf)"))
			{
				World.LoadMap(MapSwf);
				await instance.WaitUntil(() => !World.IsMapLoading, null, 15);
			}
		}

		public async Task TryJoin(IBotEngine instance, string MapName, string RoomNumber)
		{
			//await instance.WaitUntil(() => World.IsActionAvailable(LockActions.Transfer), null, 15);
			if (Player.CurrentState == Player.State.InCombat)
			{
				string[] safeCell = ClientConfig.GetValue(ClientConfig.C_SAFE_CELL).Split(',');
				Player.MoveToCell(safeCell[0], safeCell[1]);
				await instance.WaitUntil(() => Player.CurrentState != Player.State.InCombat, timeout: 10);
				await Task.Delay(1500);
			}
			String join = RoomNumber.Length > 0 ? $"{MapName}-{RoomNumber}" : MapName;
			Player.JoinMap(join, _Cell, _Pad);
			await instance.WaitUntil(() => Player.Map.EqualsIgnoreCase(MapName), () => Player.IsLoggedIn, 5);
			await instance.WaitUntil(() => !World.IsMapLoading, () => Player.IsLoggedIn, 40);
		}

		public override string ToString()
		{
			string withSwf = MapSwf != "" ? "[SWF] " : "";
			return string.Concat(new string[]
			{
				$"Join: [{Try}x] {withSwf}",
				this.Map,
				", ",
				this.Cell,
				", ",
				this.Pad
			});
		}

		public async Task ExecuteSkipCell(IBotEngine instance)
		{
			_skipcell = true;
			await Execute(instance);
		}

        private async void holdProvoke(string map)
        {
			bool temp = BotManager.Instance.chkProvoke.Checked;
            if (temp)
                BotManager.Instance.chkProvoke.Checked = false;
			
			await BotUtilities.WaitUntil(() => Player.Map.Equals(map) && !World.IsMapLoading,30);
            await Task.Delay(1500);
			BotManager.Instance.chkProvoke.Checked = temp;
        }
    }

	// Compat shim for bots saved by Grimlite Rev (CmdJoin2: Map + Room split).
	public class CmdJoin2 : IBotCommand
	{
		public string Map { get; set; }

		public string Room { get; set; }

		public string Cell { get; set; }

		public string Pad { get; set; }

		public async Task Execute(IBotEngine instance)
		{
			BotData.BotState = BotData.State.Move;
			string map = instance.ResolveVars(Map);
			string room = instance.ResolveVars(Room);
			string cell = instance.ResolveVars(Cell);
			string pad = instance.ResolveVars(Pad);
			if (cell == "Blank")
				cell = "Wait";
			if (string.IsNullOrEmpty(room))
				room = "1e99";
			if (!map.EqualsIgnoreCase(Player.Map))
			{
				if (Player.CurrentState == Player.State.InCombat)
				{
					Player.MoveToCell(Player.Cell, Player.Pad);
					await Task.Delay(2000);
				}
				room = new Regex("(1e)[0-9]{1,}", RegexOptions.IgnoreCase).Replace(room, m => new Random().Next(1001, 99999).ToString());
				Player.JoinMap($"{map}-{room}", cell, pad);
				await instance.WaitUntil(() => Player.Map.EqualsIgnoreCase(map), () => Player.IsLoggedIn, 5);
				await instance.WaitUntil(() => !World.IsMapLoading, () => Player.IsLoggedIn, 10);
			}
			if (map.EqualsIgnoreCase(Player.Map))
			{
				if (!Player.Cell.EqualsIgnoreCase(cell))
				{
					Player.MoveToCell(cell, pad);
					await Task.Delay(500);
				}
				World.SetSpawnPoint();
				BotData.BotMap = map;
				BotData.BotCell = cell;
				BotData.BotPad = pad;
			}
		}

		public override string ToString()
		{
			return $"Join: {Map}, {Room}, {Cell}, {Pad}";
		}
	}
}
