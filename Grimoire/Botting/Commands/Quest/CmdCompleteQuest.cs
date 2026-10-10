using Grimoire.Game;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Quest
{
    public class CmdCompleteQuest : IBotCommand
    {
        public Game.Data.Quest Quest
        {
            get;
            set;
        }
        public int CompleteTry { get; set; } = 1;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool ReAccept { get; set; } = false;
        public bool InBlank { get; set; } = false;

        public async Task Execute(IBotEngine instance)
        {
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.TryQuestComplete));

            bool provokeMons = instance.Configuration.ProvokeMonsters;
            //if (!Player.Quests.AcceptedQuests.Contains(Quest)) Quest.Accept();
            //if (Player.Quests.CanComplete(Quest.Id) && instance.IsRunning && Player.IsLoggedIn)
            //{
            //}
            if (provokeMons) instance.Configuration.ProvokeMonsters = false;
            if (instance.Configuration.ExitCombatBeforeQuest)
            {
                Player.refreshCell();
                await instance.WaitUntil(() => Player.CurrentState != Player.State.InCombat);
                await Task.Delay(1000);
            }
            bool max = false;
            if (CompleteTry <= -1)
                max = true;

            Quest.Complete(CompleteTry,max);
			
            //await instance.WaitUntil(() => !Player.Quests.IsInProgress(Quest.Id));
            instance.Configuration.ProvokeMonsters = provokeMons;

            if (ReAccept)
            {
                // await Task.Delay(1500);
				await instance.WaitUntil(() => !Quest.IsInProgress && !Quest.CanComplete,interval:400,timeout:5);
                Quest.GhostAccept();
            }
			else
			{
				int delay = instance.Configuration.BotDelay <= 1000 ? 1100-instance.Configuration.BotDelay : 0;
				await Task.Delay(delay);
			}
        }
        
        public override string ToString()
        {
            string quant = this.CompleteTry <= -1 ? "Ma" : CompleteTry.ToString();
            return $"Complete quest [{quant}x]: {(Quest.ItemId != null && Quest.ItemId != "0" ? $"{Quest.Id}^{Quest.ItemId}" : Quest.Id.ToString())} {(InBlank ? "[InBlank]" : "")}";
        }
    }

    // Compat shim for bots saved by Grimlite Rev (quest/item ids as strings/vars).
    public class CmdCompleteQuest2 : IBotCommand
    {
        public string QuestID
        {
            get;
            set;
        }

        public string ItemID
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            int quest = int.Parse(instance.ResolveVars(QuestID));
            string item = ItemID != null ? instance.ResolveVars(ItemID) : null;
            if (!Player.Quests.CanComplete(quest))
            {
                return;
            }
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.TryQuestComplete));
            BotData.BotState = BotData.State.Quest;
            if (!Player.Quests.HasQuest(quest))
            {
                Player.Quests.Load(quest);
                await instance.WaitUntil(() => Player.Quests.HasQuest(quest), timeout: 3);
            }
            if (instance.Configuration.ExitCombatBeforeQuest || Player.CurrentState == Player.State.InCombat)
            {
                if (Player.CurrentState == Player.State.InCombat)
                {
                    Player.MoveToCell(Player.Cell, Player.Pad);
                    await Task.Delay(2000);
                }
            }
            if (!string.IsNullOrEmpty(item) && item != "0")
                Player.Quests.Complete(quest.ToString(), item);
            else
                Player.Quests.Complete(quest);
            await instance.WaitUntil(() => !Player.Quests.IsInProgress(quest));
        }

        public override string ToString()
        {
            return $"Complete quest: {((ItemID != null && ItemID != "0") ? $"{QuestID}, {ItemID}" : QuestID)}";
        }
    }
}