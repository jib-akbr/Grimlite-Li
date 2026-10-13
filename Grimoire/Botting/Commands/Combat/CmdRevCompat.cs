using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Combat
{
    // Compat shim for bots saved by Grimlite Rev.
    public class CmdCancelAutoAttack : IBotCommand
    {
        public async Task Execute(IBotEngine instance)
        {
            if (Player.HasTarget)
            {
                Player.CancelAutoAttack();
            }
            await Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Cancel auto attack";
        }
    }
}
