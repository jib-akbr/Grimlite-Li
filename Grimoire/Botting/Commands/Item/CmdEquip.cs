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

			if (item == null) return;

			while (instance.IsRunning && !IsEquipped(item.Id))
			{
				using (new pauseProvoke(instance.Configuration))
				{
				if (Safe)
				{
					BotData.BotState = BotData.State.Transaction;
					while (instance.IsRunning && Player.CurrentState == Player.State.InCombat)
					{
						Player.MoveToCell(Player.Cell, Player.Pad);
						await Task.Delay(1000);
					}
					await instance.WaitUntil(() => World.IsActionAvailable(LockActions.EquipItem));
				}

				if (item.Category == "Item")
					Player.EquipPotion(item.Id, item.Description, item.File, item.Name);
				else
					Player.Equip(item.Id);
				}
			}
		}

		public bool IsEquipped(int ItemID)
		{
			return Player.Inventory.Items.FirstOrDefault((InventoryItem it) => it.IsEquipped && it.Id == ItemID) != null;
		}

		public override string ToString()
		{
			return (Safe ? "Safe" : "Unsafe") + " Equip: " + ItemName;
		}
	}
}