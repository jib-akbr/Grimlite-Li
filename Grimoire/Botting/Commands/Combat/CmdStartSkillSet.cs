using System.Threading.Tasks;
using Grimoire.UI;

namespace Grimoire.Botting.Commands.Combat
{
    public class CmdStartSkillSet : IBotCommand
    {
        public async Task Execute(IBotEngine instance)
        {
            BotManager.Instance.StartSkillSetMonitoring();
            await Task.Delay(100);
        }

        public override string ToString() => "[Start Skill Set]";
    }
}
