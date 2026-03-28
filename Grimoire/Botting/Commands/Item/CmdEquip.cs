using Grimoire.Game;
using Grimoire.Game.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Item
{
	public class CmdEquip : IBotCommand
	{
		public string ItemName
		{
			get;
			set;
		}

		public bool Safe
		{
			get;
			set;
		}

		public async Task Execute(IBotEngine instance)
		{
			// Resolve variables first (if ItemName is a variable key).
			var raw = instance.ResolveVars(ItemName);
            InventoryItem item = null;

            // 1) Forge enhancement by enum name (Valiance, Dauntless, Praxis, etc.)
            if (Enum.TryParse<InventoryItem.forgeID>(raw, ignoreCase: true, out var forge))
            {
                item = Player.Inventory.Items.FirstOrDefault(i => i.ForgeEnhancement == forge && i.IsEquippable);
            }

            // 2) Enhancement *display name* (Luck Awe Blast, Luck Health Vamp, etc.).
            //    If the string matches one of our known enhancement names, look up its ID
            //    and equip the first item that has that enhancement.
            if (item == null)
            {
                var kvp = InventoryItem.EnhancementNames
                    .FirstOrDefault(p => p.Value.Equals(raw, StringComparison.OrdinalIgnoreCase));
                if (!kvp.Equals(default(System.Collections.Generic.KeyValuePair<int, string>)))
                {
                    int enhIdByName = kvp.Key;
                    item = Player.Inventory.Items
                        .FirstOrDefault(i => i.Enhancement == enhIdByName && i.IsEquippable);
                }
            }

            // 3) Raw enhancement ID (for non-Forge enchants).
            //    If the string is a number, treat it as the enhancement ID and equip the first
            //    equippable item that has that enhancement.
            if (item == null && int.TryParse(raw, out int enhId))
            {
                item = Player.Inventory.Items.FirstOrDefault(i => i.Enhancement == enhId && i.IsEquippable);
            }

            // 4) Fallback: normal item name match.
            if (item == null)
            {
                item = Player.Inventory.Items.FirstOrDefault(i =>
                    i.IsEquippable && i.Name.Equals(raw, StringComparison.OrdinalIgnoreCase));
            }

			if (item == null)
				return;

			// If item is already equipped, skip
			if (IsEquipped(item))
				return;

			// Item is not equipped, so apply Safe logic if enabled
			if (Safe)
			{
				while (instance.IsRunning && Player.CurrentState == Player.State.InCombat)
				{
					Player.MoveToCell(Player.Cell, Player.Pad);
					await Task.Delay(1000);
				}

				await instance.WaitUntil(() => World.IsActionAvailable(LockActions.EquipItem));
			}

			int equipAttempts = 0;
			int maxAttempts = 100; // 50 seconds max (100 * 500ms)
			while (instance.IsRunning && !IsEquipped(item) && equipAttempts < maxAttempts)
			{
				equipAttempts++;
				
				bool shouldBreak = false;
				using (new pauseProvoke(instance.Configuration))
				{
					if (item.Category == "Item")
					{
						Player.EquipPotion(item.Id, item.Description, item.File, item.Name);
					}
					else
					{
						Player.Equip(item.Id);
						shouldBreak = true; // Exit loop immediately - assume equipment was successful
					}
					
					await Task.Delay(500);
				}
				
				if (shouldBreak)
					break;
			}


		}

		public bool IsEquipped(InventoryItem item)
		{
			// Classes are tracked separately - check against EquippedClass
			if (item.Category == "Class")
			{
				return Player.EquippedClass.Equals(item.Name, StringComparison.OrdinalIgnoreCase);
			}

			// For weapons, armor, helm, and cape: Try the Flash property first, fallback to inventory search
			if (InventoryItem.Weapons.Contains(item.Category) || item.Category == "Armor" || item.Category == "Helm" || item.Category == "Cape")
			{
				string equipped = null;
				
				// Try to get equipped item from Flash property (new approach)
				if (InventoryItem.Weapons.Contains(item.Category))
					equipped = Player.EquippedWeapon ?? "";
				else if (item.Category == "Armor")
					equipped = Player.EquippedArmor ?? "";
				else if (item.Category == "Helm")
					equipped = Player.EquippedHelm ?? "";
				else if (item.Category == "Cape")
					equipped = Player.EquippedCape ?? "";
				
				// If Flash property has a value, use it
				if (!string.IsNullOrEmpty(equipped))
				{
					return equipped.Equals(item.Name, StringComparison.OrdinalIgnoreCase);
				}
				
				// Fallback: Check if item was recently equipped by checking if it's now marked as equipped in inventory
				// This might work if the game updates the IsEquipped flag after equipping
				var inventoryItem = Player.Inventory.Items.FirstOrDefault(it => it.Id == item.Id);
				if (inventoryItem != null && inventoryItem.IsEquipped)
					return true;
				
				return false;
			}

			// For other items (potions, etc.), check the IsEquipped flag in inventory
			var equippedItem = Player.Inventory.Items.FirstOrDefault((InventoryItem it) => it.IsEquipped && it.Id == item.Id);
			return equippedItem != null;
		}

		public override string ToString()
		{
			return (Safe ? "Safe" : "Unsafe") + " Equip: " + ItemName;
		}
	}
}