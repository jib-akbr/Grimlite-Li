using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.UI;
using Grimoire.Game.Data;
namespace Grimoire.Botting.Commands.Misc
{
    public class CmdStop : IBotCommand
    {
        public bool KeepLagkiller { get; set; } = false;
        public Task Execute(IBotEngine instance)
        {
            if (Configuration.Instance.BankOnStop)
            {
                foreach (InventoryItem item in Player.Inventory.Items)
                {
                    if (!item.IsEquipped && item.IsAcItem && item.Category != "Class" && item.Name.ToLower() != "treasure potion" && Configuration.Instance.Items.Contains(item.Name))
                    {
                        Player.Bank.TransferToBank(item.Name);
                        Task.Delay(70);
                        LogForm.Instance.AppendDebug("Transferred to Bank: " + item.Name);
                    }
                }
                LogForm.Instance.AppendDebug("Banked all AC Items in Items list");
            }
            Configuration.Instance.keepLagKiller = KeepLagkiller;
            //LogForm.Instance.AppendDebug($"Keep lag killer : {KeepLagkiller}");
            Task.Delay(2000);
            instance.Stop();
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Stop bot";
        }
    }
}