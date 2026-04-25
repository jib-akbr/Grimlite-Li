using Grimoire.Game;
using System.Threading.Tasks;
using Grimoire.Botting.Commands.Quest;
using System.Linq;
using System.Windows.Forms;
using Grimoire.UI;
using System;

namespace Grimoire.Botting.Commands.Quest
{
public class CmdAddQuestList : IBotCommand
{
public int Id
{
get;
set;
}
public string ItemId
{
get;
set;
}
public bool SafeRelogin
{
get;
set;
}

public async Task Execute(IBotEngine instance)
{
int questId = Id;

		// Ensure quest is loaded from server
		if (!Player.Quests.QuestTree.Any(q => q.Id == questId))
		{
			Player.Quests.Load(questId);
			await instance.WaitUntil(() => Player.Quests.QuestTree.Any(q => q.Id == questId), timeout: 5);
		}

		// Get the quest from QuestTree
		var loadedQuest = Player.Quests.QuestTree.FirstOrDefault(q => q.Id == questId);
		if (loadedQuest == null)
		{
			return;
		}

		// Try to accept the quest if not already in progress
		if (!loadedQuest.IsInProgress)
		{
			int attempts = 0;
			int maxAttempts = 3;

			while (!Player.Quests.IsInProgress(questId) && attempts < maxAttempts)
			{
				loadedQuest.Accept();
				await Task.Delay(600);
				attempts++;
			}
		}

		// Add to quest list
		Game.Data.Quest quest = new Game.Data.Quest
		{
			Id = Id,
			ItemId = ItemId,
			SafeRelogin = SafeRelogin,
		};

		if (instance.Configuration.Quests.FirstOrDefault(x => x.Id == quest.Id) == null)
		{
			BotManager.Instance.Invoke((MethodInvoker)delegate {
				BotManager.Instance.AddQuest(Id, ItemId, SafeRelogin);
			});
			instance.Configuration.Quests.Add(quest);

			if (instance.IsRunning)
			{
				instance.StartQuestList();
			}
		}
	}

	public override string ToString()
	{
		string safe = SafeRelogin ? " [SafeRelogin]" : "";
		string itemId = ItemId != null ? $" {ItemId}" : "";
		return $"Add Quest list : {Id}{itemId}{safe}";
	}
}
}