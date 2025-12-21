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

            // Ensure quest is loaded
            if (!Player.Quests.QuestTree.Any(q => q.Id == id))
            {
                Player.Quests.Load(id);
                
                // Wait for quest to load with timeout
                await instance.WaitUntil(() => Player.Quests.QuestTree.Any(q => q.Id == id), timeout: 3);
                
                if (!Player.Quests.QuestTree.Any(q => q.Id == id))
                {
                    LogForm.Instance.devDebug($"[Quest] Timeout: Quest {id} failed to load within 3 seconds");
                    return;
                }
            }

            // Get quest reference with null safety
            var Quest = Player.Quests.Quest(id);
            if (Quest == null)
            {
                LogForm.Instance.devDebug($"[Quest] Failed to accept: Quest {id} not found after loading");
                return;
            }

            // Skip if quest is already completed (non-repeatable quests only)
            if (Quest.IValue <= Player.Quests.progress(Quest.Id) && Quest.ISlot != 0 && Quest.IsNotRepeatable)
            {
                LogForm.Instance.devDebug($"[Quest] Skipping quest {id} - already completed ({Quest.ISlot}): {Player.Quests.progress(id)}/{Quest.IValue}");
                return;
            }

            // Skip if quest is already in progress
            if (Player.Quests.IsInProgress(Quest.Id))
            {
                LogForm.Instance.devDebug($"[Quest] Quest {id} already in progress");
                return;
            }

            // Wait for action to be available
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.AcceptQuest), timeout: 5);
            
            if (!World.IsActionAvailable(LockActions.AcceptQuest))
            {
                LogForm.Instance.devDebug($"[Quest] Warning: AcceptQuest action not available after 5 seconds, attempting anyway...");
            }

            // Handle ghost accept
            if (ghostAccept)
            {
                Quest.GhostAccept();
                await Task.Delay(600);
                LogForm.Instance.devDebug($"[Quest] Ghost accepted: {id}");
                return;
            }

            // Try to accept quest with retry logic
            int attempts = 0;
            int maxAttempts = 3;

            while (!Player.Quests.IsInProgress(Quest.Id) && Player.IsLoggedIn && instance.IsRunning && attempts < maxAttempts)
            {
                Quest.Accept();
                await Task.Delay(600);
                attempts++;

                if (attempts == maxAttempts && !Player.Quests.IsInProgress(Quest.Id))
                {
                    LogForm.Instance.devDebug($"[Quest] Failed to accept quest {id} after {maxAttempts} attempts");
                }
            }

            if (Player.Quests.IsInProgress(Quest.Id))
            {
                LogForm.Instance.devDebug($"[Quest] Successfully accepted: {id}");
            }
        }

        public override string ToString()
        {
            return (ghostAccept ? "Ghost Accept: " : "Accept Quest: ") + Quest.Id;
        }
    }
}