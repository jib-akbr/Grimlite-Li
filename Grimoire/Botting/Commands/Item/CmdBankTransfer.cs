using Grimoire.Game;
using Grimoire.UI;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Item
{
    public class CmdBankTransfer : IBotCommand
    {
        public bool TransferFromBank
        {
            get;
            set;
        }

        public string ItemName
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Transaction;
            string ItemName = instance.ResolveVars(this.ItemName);
            await Player.ExitCombat(2000);
			
            if (TransferFromBank && Player.Bank.GetItemByName(ItemName) != null)
            {
                Player.Bank.TransferFromBank(ItemName);
                await instance.WaitUntil(() => Player.Inventory.ContainsItem(ItemName, "*"),interval:200);
                // await Task.Delay(500);
            }
            else if (!TransferFromBank && Player.Inventory.GetItemByName(ItemName) != null)
            {
                Player.Bank.TransferToBank(ItemName);
                await instance.WaitUntil(() => !Player.Inventory.ContainsItem(ItemName, "*"),interval:200);
                // await Task.Delay(500);
            }else
				LogForm.Instance.devDebug($"[Bank] {ItemName} not found within your "+(TransferFromBank?"Bank":"Inventory"));
        }

        public override string ToString()
        {
            return (TransferFromBank ? "Bank -> Inv: " : "Inv -> Bank: ") + ItemName;
        }
    }
}