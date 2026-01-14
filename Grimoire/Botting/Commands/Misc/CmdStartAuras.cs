using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.UI;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdStartAuras : IBotCommand
    {
        public async Task Execute(IBotEngine instance)
        {
            BotManager.Instance.StartAuraMonitoring();
            await Task.Delay(100);
        }

        public override string ToString() => "[Start Aura Monitoring]";
    }
}
