using Grimoire.Game;
using Grimoire.Game.Data;
using Grimoire.UI;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Quest
{
    public class CmdAcceptQuest : IBotCommand
    {
        public Game.Data.Quest Quest
        {
            get;
            set;
        }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool ghostAccept
        {
            get;
            set;
        } = false;

        public async Task Execute(IBotEngine instance)
        {
            BotData.BotState = BotData.State.Quest;
            int id = this.Quest.Id;

            if (!Player.Quests.HasQuest(id))
            {
                Player.Quests.Load(id);
                await instance.WaitUntil(() => Player.Quests.HasQuest(id), timeout:3);
            }

            var Quest = Player.Quests.Quest(id);
            if (Quest == null)
            {
                LogForm.Instance.devDebug("[Quest] Failed to accept, Quest not found/loaded");
                return;
            }

            if (Quest.IValue <= Player.Quests.progress((int)Quest.ISlot) 
                && Quest.ISlot != 0 && Quest.IsNotRepeatable)
            {
                LogForm.Instance.devDebug($"[Quest] Skipping quest since requirement satisfied ({id} - {Quest.ISlot}) : {Player.Quests.progress((int)Quest.ISlot)}/{Quest.IValue}");
                return;
            }

            if (ghostAccept)
            {
                Quest.GhostAccept();
                await Task.Delay(600);
                return;
            }

            int i = 0;
            while (!Player.Quests.IsInProgress(Quest.Id) && Player.IsLoggedIn && instance.IsRunning && i < 2)
            {
                Quest.Accept();
                await Task.Delay(600);
                i++;
            }
            //await instance.WaitUntil(() => Player.Quests.IsInProgress(Quest.Id));
        }

        public override string ToString()
        {
            return (ghostAccept ? $"Ghost Accept: " : $"Accept Quest: ")+Quest.Id;
        }
    }
}