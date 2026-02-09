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
				// Wait for quests to load from server
			await instance.WaitUntil(() => Player.Quests != null, timeout: 10);

			// Try to accept the quest if not already in progress
			if (!Player.Quests.IsInProgress(id))
			{
				LogForm.Instance.AppendDebug($"[CmdKillFor] Attempting to accept quest {id}...");
				
				// Retry acceptance multiple times
				int retries = 0;
				while (!Player.Quests.IsInProgress(id) && retries < 5 && instance.IsRunning)
				{
					Player.Quests.Accept(id);
					await Task.Delay(800);
					retries++;
					LogForm.Instance.AppendDebug($"[CmdKillFor] Quest accept attempt {retries}/5");
				}
				
				if (Player.Quests.IsInProgress(id))
				{
					LogForm.Instance.AppendDebug($"[CmdKillFor] Quest {id} accepted successfully!");
				}
				else
				{
					LogForm.Instance.AppendDebug($"[CmdKillFor] WARNING: Quest {id} may not be accepted. Continuing anyway...");
				}
			}
			else
			{
				LogForm.Instance.AppendDebug($"[CmdKillFor] Quest {id} already in progress");
			}

			// Kill until quest can be completed
			int killCount = 0;
			bool foundRequired = false;
			
			// Parse comma-separated items and quantities
			string[] itemNames = ItemName.Split(new char[] { ',' });
			string[] quantities = Quantity.Split(new char[] { ',' });
			
			// Trim whitespace
			for (int i = 0; i < itemNames.Length; i++)
				itemNames[i] = itemNames[i].Trim();
			for (int i = 0; i < quantities.Length; i++)
				quantities[i] = quantities[i].Trim();
			
			// Create a background task to check inventory every 2 seconds while killing
			var inventoryCheckTask = Task.Run(async () =>
			{
				while (!foundRequired && instance.IsRunning)
				{
					if (!string.IsNullOrEmpty(ItemName))
					{
						// Check if all items have required quantities
						bool allObtained = true;
						
						for (int i = 0; i < itemNames.Length; i++)
						{
							if (int.TryParse(quantities[i], out int requiredQty))
							{
								var item = Player.TempInventory.Items.FirstOrDefault(it => 
									it.Name.Equals(itemNames[i], StringComparison.OrdinalIgnoreCase));
								int currentCount = item?.Quantity ?? 0;
								
								if (currentCount < requiredQty)
								{
									allObtained = false;
									LogForm.Instance.AppendDebug($"[CmdKillFor] {itemNames[i]}: {currentCount}/{requiredQty}");
								}
								else
								{
									LogForm.Instance.AppendDebug($"[CmdKillFor] {itemNames[i]}: {currentCount}/{requiredQty} ✓");
								}
							}
						}
						
						if (allObtained)
						{
							LogForm.Instance.AppendDebug($"[CmdKillFor] All items obtained! Moving to next command. [{DateTime.Now:HH:mm:ss.fff}]");
							foundRequired = true;
							
							// Aggressively cancel target repeatedly
							for (int cancelAttempt = 0; cancelAttempt < 20; cancelAttempt++)
							{
								Player.CancelTarget();
								await Task.Delay(50);
							}
							break;
						}
					}
					
					await Task.Delay(1000); // Check every 1 second
				}
			});
			
			// Kill loop runs normally while background task checks inventory
			while (instance.IsRunning && Player.IsLoggedIn && Player.IsAlive && !foundRequired)
			{
				killCount++;
				await kill.Execute(instance);
				
				// Check if items were obtained during combat
				if (foundRequired)
					break;
				
				await Task.Delay(DelayAfterKill); // Minimal delay - background task checks every 2 seconds
			}
			
			// Exit immediately and wait for background task to complete
			if (foundRequired)
			{
				LogForm.Instance.AppendDebug($"[CmdKillFor] Items obtained! Exiting hunt loop immediately. [{DateTime.Now:HH:mm:ss.fff}]");
				
				// Keep canceling target aggressively
				for (int i = 0; i < 30; i++)
				{
					Player.CancelTarget();
					await Task.Delay(50);
				}
				
				// Wait a brief moment for background task to finish cleanly
				try
				{
					await Task.WhenAny(inventoryCheckTask, Task.Delay(100));
				}
				catch { }
			}
			
			// Check if quest can be completed after hunting
			if (Player.Quests.CanComplete(id))
			{
				LogForm.Instance.AppendDebug($"[CmdKillFor] Quest {id} is completable. Completing...");
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
			// Trim whitespace from item names
			for (int i = 0; i < itemsName.Length; i++)
				itemsName[i] = itemsName[i].Trim();

			string[] quantities = Quantity.Split(new char[] { ',' });
			// Trim whitespace from quantities
			for (int i = 0; i < quantities.Length; i++)
				quantities[i] = quantities[i].Trim();

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
					
					// Check if item obtained and cancel target immediately to prevent further attacks
					bool allItemsObtained = Enumerable.Range(0, itemsName.Length).All(i => 
					{
						bool has = Player.Inventory.ContainsItem(itemsName[i], quantities[i]);
						if (!has)
							LogForm.Instance?.AppendDebug($"[CmdKillFor] Checking {itemsName[i]} x{quantities[i]} - Still needed");
						else
							LogForm.Instance?.AppendDebug($"[CmdKillFor] {itemsName[i]} x{quantities[i]} - OBTAINED!");
						return has;
					});
					
					if (allItemsObtained)
					{
						LogForm.Instance.AppendDebug($"[CmdKillFor] All items obtained! Stopping attack.");
						Player.CancelTarget();
						break;
					}
				}
				LogForm.Instance.AppendDebug($"[CmdKillFor] Item hunting complete!");
			}
			else
			{
				// Trim item name and quantity for temp inventory
				ItemName = ItemName.Trim();
				string trimmedQty = Quantity.Trim();
				
				LogForm.Instance.AppendDebug($"[CmdKillFor] Temp mode - Hunting for {ItemName} ({trimmedQty}x)");
				while (instance.IsRunning && 
					Player.IsLoggedIn && 
					Player.IsAlive &&
					!Player.TempInventory.ContainsItem(ItemName, trimmedQty))
				{
					await kill.Execute(instance);
					await Task.Delay(DelayAfterKill);
				
					// Check if item obtained
					if (Player.TempInventory.ContainsItem(ItemName, trimmedQty))
					{
						LogForm.Instance.AppendDebug($"[CmdKillFor] Temp item {ItemName} x{trimmedQty} OBTAINED!");
						Player.CancelTarget();
						break;
					}
					else
					{
						LogForm.Instance.AppendDebug($"[CmdKillFor] {ItemName} x{trimmedQty} - Still needed");
					}
				}

				LogForm.Instance.AppendDebug($"[CmdKillFor] Temp item hunting complete!");
				Player.CancelTarget();
				await Task.Delay(500);
			}
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
