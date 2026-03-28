using Grimoire.Game;
using System;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdReturn : IBotCommand
    {
        public async Task Execute(IBotEngine instance)
        {
            try
            {
                int i = --instance.CurrentConfiguration;
                Configuration oldConfig = Bot.Configurations[i];
                int oldIndex = Bot.OldIndex[i];
                if (oldConfig != null && oldConfig.Commands.Count > 0 && oldIndex > -1)
                {
                    instance.Configuration = oldConfig;
                    instance.Index = oldIndex;
                    instance.LoadBankItems();
                    instance.LoadAllQuests();
					if (oldConfig?.Quests.Count > 0)
						instance.StartQuestList();
					BotData.BotSkill = null;
                }
            }
            catch //(Exception e)
            {
                //MessageBox.Show(e.ToString());
            }
        }

        public override string ToString()
        {
            return "Return";
        }
    }
}