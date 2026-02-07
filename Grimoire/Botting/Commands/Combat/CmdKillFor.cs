using Grimoire.Botting.Commands.Item;
using Grimoire.Game;
using Grimoire.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Combat
{
	public class CmdKillFor : IBotCommand
	{
		public string Monster
		{
			get;
			set;
		}

		public string ItemName
		{
			get;
			set;
		}

		public ItemType ItemType
		{
			get;
			set;
		}

		public string Quantity
		{
			get;
			set;
		}
		public string KillPriority { get; set; } = "";
		public bool AntiCounter { get; set; } = false;
		public string QuestId { get; set; }
		public string SkillSet { get; set; } = "Auto Attack";
		public int DelayAfterKill { get; set; } = 500;

		private Configuration config;
		public async Task Execute(IBotEngine instance)
		{
			string Monster = (instance.IsVar(this.Monster) ? Configuration.Tempvariable[instance.GetVar(this.Monster)] : this.Monster);
			string ItemName = ((instance.IsVar(this.ItemName) ? Configuration.Tempvariable[instance.GetVar(this.ItemName)] : this.ItemName)).Trim();

			BotData.BotState = BotData.State.Combat;
			CmdKill kill = new CmdKill {
				Monster = Monster,
				KillPriority = KillPriority,
				SkillSet = SkillSet,
				AntiCounter = AntiCounter
			};

			int id;
			if (int.TryParse(QuestId, out id))
			{
				// Check if quest is available first (not already completed one-time quest)
				if (!Player.Quests.IsAvailable(id))
				{
					LogForm.Instance.AppendDebug($"[CmdKillFor] Quest {id} unavailable (one-time quest already completed)");
					return;
				}

				// Try to accept the quest if not already in progress
				if (!Player.Quests.IsInProgress(id))
				{
					LogForm.Instance.AppendDebug($"[CmdKillFor] Accepting quest {id}...");
					Player.Quests.Accept(id);
					await instance.WaitUntil(() => Player.Quests.IsInProgress(id), timeout: 5);
					LogForm.Instance.AppendDebug($"[CmdKillFor] Quest {id} accepted");
				}

				// Kill until quest can be completed
			int killCount = 0;
			while (instance.IsRunning && Player.IsLoggedIn && Player.IsAlive)
			{
				// Check if quest is now completable
				if (Player.Quests.CanComplete(id))
				{
					LogForm.Instance.AppendDebug($"[CmdKillFor] Quest {id} is now completable after {killCount} kills");
					break;
				}
				
				killCount++;
				await kill.Execute(instance);
				await Task.Delay(DelayAfterKill + 500); // Extra delay to let server update
			}
			
			// Complete the quest if it can be completed
			if (Player.Quests.CanComplete(id))
			{
				LogForm.Instance.AppendDebug($"[CmdKillFor] Completing quest {id}...");
				Player.Quests.Complete(id);
				await Task.Delay(1000);
				LogForm.Instance.AppendDebug($"[CmdKillFor] Quest {id} completed!");
			}
		}
			else
			{
				List<string> removedList = new List<string>();
				config = instance.Configuration;

				string[] itemsName = ItemName.Split(new char[] { ',' });

				string[] quantities = Quantity.Split(new char[] { ',' });

				if (ItemType == ItemType.Items)
				{
					LogForm.Instance.AppendDebug($"[CmdKillFor] Item mode - Hunting for {ItemName} ({Quantity}x)");
					while (instance.IsRunning && 
						Player.IsLoggedIn && 
						Player.IsAlive &&
						!Enumerable.Range(0, itemsName.Length).All(i => Player.Inventory.ContainsItem(itemsName[i], quantities[i]))
						)
					{
						await kill.Execute(instance);
						await Task.Delay(DelayAfterKill);
					}
					LogForm.Instance.AppendDebug($"[CmdKillFor] Item hunting complete!");
				}
				else
				{
					while (instance.IsRunning && 
						Player.IsLoggedIn && 
						Player.IsAlive &&
						!Player.TempInventory.ContainsItem(ItemName, Quantity))
					{
						await kill.Execute(instance);
						await Task.Delay(DelayAfterKill);
					}
				}

				Player.CancelTarget();
				await Task.Delay(500);
			}
		}

		public override string ToString()
		{
			string text;
			if (int.TryParse(QuestId, out _))
			{
				text = $"KFQuest: [{QuestId}] [{Monster}]";
			}
			else if (ItemType == ItemType.Items)
			{
				text = $"KFItems: [{ItemName} {Quantity}x] [{Monster}]";
			}
			else
			{
				text = $"KFTemps: [{ItemName} {Quantity}x] [{Monster}]";
			}

			return text;
		}
	}
}
