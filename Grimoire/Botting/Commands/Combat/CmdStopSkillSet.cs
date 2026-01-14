using System.Threading.Tasks;
using Grimoire.UI;

namespace Grimoire.Botting.Commands.Combat
{
    public class CmdStopSkillSet : IBotCommand
    {
        public async Task Execute(IBotEngine instance)
        {
            BotManager.Instance.StopSkillSetMonitoring();
            await Task.Delay(100);
        }

        public override string ToString() => "[Stop Skill Set]";
    }
}
