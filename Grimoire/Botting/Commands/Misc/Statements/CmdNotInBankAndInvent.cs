using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
	public class CmdNotInBankAndInvent : StatementCommand, IBotCommand
	{
		public CmdNotInBankAndInvent()
		{
			Tag = "Item";
			Text = "Is not in bank and inventory";
		}

		public Task Execute(IBotEngine instance)
		{
			string _name = Bot.Instance.ResolveVars(Value1);
            string _qty  = Bot.Instance.ResolveVars(Value2);

			bool inBank;
			bool inInventory;
            
			if (int.TryParse(_name, out int id) && int.TryParse(_qty, out int qty))
            {
                inBank = Player.Bank.ContainsItem(id, qty);
                inInventory = Player.Inventory.ContainsItem(id, qty);
			}
			else
			{
                inBank = Player.Bank.ContainsItem(_name, _qty);
                inInventory = Player.Inventory.ContainsItem(_name, _qty);
            }

            if (!inBank && !inInventory)
			{
				//not inbank & inventory
			}
			else
            {
				instance.Index++;
			}
			return Task.FromResult<object>(null);
		}

		public override string ToString()
		{
			return "Is not in bank and invent: " + Value1 + ", " + Value2;
		}
	}
}
