using System.Threading.Tasks;
using Grimoire.Game;
using Grimoire.UI;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdStopAuras : IBotCommand
    {
        public async Task Execute(IBotEngine instance)
        {
            BotManager.Instance.StopAuraMonitoring();
            await Task.Delay(100);
        }

        public override string ToString() => "[Stop Aura Monitoring]";
    }
}
