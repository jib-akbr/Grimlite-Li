using Grimoire.Botting.Commands.Item;
using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.Tools;
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
			
			// Create a background task to check inventory every 1 second while killing
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
							Player.CancelTarget();
							break;
						}
					}
					
					await Task.Delay(1000); // Check every 1 second
				}
			});
			
			// Inline attack loop - check inventory between each skill for instant exit
			int[] autoAttackSkills = { 1, 2, 3, 4 };
			string skillSetName = string.IsNullOrEmpty(SkillSet) ? "Auto Attack" : SkillSet;
			List<Skill> skillsToUse = new List<Skill>();
			
			// Load custom skillset if specified
			if (skillSetName != "Auto Attack")
			{
				var skillSetData = SkillSetManager.Instance.LoadSkillSet(skillSetName);
				if (skillSetData != null && skillSetData.Skills != null)
				{
					foreach (var savedSkill in skillSetData.Skills)
					{
						var skill = new Skill
						{
							Index = savedSkill.Index,
							Text = savedSkill.Text,
							Type = (Skill.SkillType)savedSkill.Type,
							SType = (Skill.SafeType)savedSkill.SafeType,
							IsSafeMp = savedSkill.IsSafeMp,
							SafeValue = savedSkill.SafeValue,
							SType2 = (Skill.SafeType)savedSkill.SafeType2,
							IsSafeMp2 = savedSkill.IsSafeMp2,
							SafeValue2 = savedSkill.SafeValue2,
							waitCd = savedSkill.WaitCooldown,
							dodgeAttack = savedSkill.WaitDodge
						};
						skillsToUse.Add(skill);
					}
				}
			}
			
			// Kill loop with per-attack inventory checks
			while (instance.IsRunning && Player.IsLoggedIn && Player.IsAlive && !foundRequired)
			{
				// Check if target is still available
				if (!World.IsMonsterAvailable(Monster))
				{
					LogForm.Instance.AppendDebug($"[CmdKillFor] Monster {Monster} unavailable, retrying...");
					await instance.WaitUntil(() => World.IsMonsterAvailable(Monster), null, 3);
					
					if (!World.IsMonsterAvailable(Monster))
						break;
				}
				
				// Attack and execute skills
				if (!Player.HasTarget)
				{
					Player.AttackMonster(Monster);
					await Task.Delay(200);
				}
				
				killCount++;
				LogForm.Instance.AppendDebug($"[CmdKillFor] Kill #{killCount} started");
				
				// Execute skills and check inventory after each one
				if (skillsToUse.Count > 0)
				{
					// Custom skillset
					foreach (var skill in skillsToUse)
					{
						if (!instance.IsRunning || !Player.IsAlive || !World.IsMonsterAvailable(Monster) || foundRequired)
							break;
						
						await skill.ExecuteSkill();
						await Task.Delay(50);
						
						// Inline inventory check - instant exit without waiting for full combat
						if (!foundRequired)
						{
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
										break;
									}
								}
							}
							if (allObtained)
							{
								foundRequired = true;
								Player.CancelTarget();
								break;
							}
						}
					}
				}
				else
				{
					// Auto attack skills 1, 2, 3, 4
					foreach (int skillIndex in autoAttackSkills)
					{
						if (!instance.IsRunning || !Player.IsAlive || !World.IsMonsterAvailable(Monster) || foundRequired)
							break;
						
						Player.UseSkill(skillIndex.ToString());
						await Task.Delay(50);
						
						// Inline inventory check - instant exit without waiting for full combat
						if (!foundRequired)
						{
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
										break;
									}
								}
							}
							if (allObtained)
							{
								foundRequired = true;
								Player.CancelTarget();
								break;
							}
						}
					}
				}
				
				await Task.Delay(25);
				
				if (foundRequired)
					break;
				
				await Task.Delay(DelayAfterKill);
			}
			
			// Exit immediately
			if (foundRequired)
			{
				LogForm.Instance.AppendDebug($"[CmdKillFor] Items obtained! Exiting hunt loop immediately. [{DateTime.Now:HH:mm:ss.fff}]");
				Player.CancelTarget();
				
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
				int[] autoAttackSkills = { 1, 2, 3, 4 };
				
				while (instance.IsRunning && 
					Player.IsLoggedIn && 
					Player.IsAlive &&
					!Enumerable.Range(0, itemsName.Length).All(i => Player.Inventory.ContainsItem(itemsName[i], quantities[i])))
				{
					await instance.WaitUntil(() => World.IsMonsterAvailable(Monster), null, 3);
					if (!World.IsMonsterAvailable(Monster))
						continue;

					if (!Player.HasTarget)
					{
						Player.AttackMonster(Monster);
						await Task.Delay(200);
					}

					while (Player.IsAlive && World.IsMonsterAvailable(Monster) && instance.IsRunning &&
						!Enumerable.Range(0, itemsName.Length).All(i => Player.Inventory.ContainsItem(itemsName[i], quantities[i])))
					{
						foreach (int skillIndex in autoAttackSkills)
						{
							if (!instance.IsRunning || !Player.IsAlive || !World.IsMonsterAvailable(Monster))
								break;
							
							Player.UseSkill(skillIndex.ToString());
							await Task.Delay(50);
						}
						await Task.Delay(25);
					}

					Player.CancelTarget();
					await Task.Delay(DelayAfterKill);
					
					// Check if item obtained
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
				int[] autoAttackSkills = { 1, 2, 3, 4 };
				
				while (instance.IsRunning && 
					Player.IsLoggedIn && 
					Player.IsAlive &&
					!Player.TempInventory.ContainsItem(ItemName, trimmedQty))
				{
					await instance.WaitUntil(() => World.IsMonsterAvailable(Monster), null, 3);
					if (!World.IsMonsterAvailable(Monster))
						continue;

					if (!Player.HasTarget)
					{
						Player.AttackMonster(Monster);
						await Task.Delay(200);
					}

					while (Player.IsAlive && World.IsMonsterAvailable(Monster) && instance.IsRunning &&
						!Player.TempInventory.ContainsItem(ItemName, trimmedQty))
					{
						foreach (int skillIndex in autoAttackSkills)
						{
							if (!instance.IsRunning || !Player.IsAlive || !World.IsMonsterAvailable(Monster))
								break;
							
							Player.UseSkill(skillIndex.ToString());
							await Task.Delay(50);
						}
						await Task.Delay(25);
					}

					Player.CancelTarget();
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
