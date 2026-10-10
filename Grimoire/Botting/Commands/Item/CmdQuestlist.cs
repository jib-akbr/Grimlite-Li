using Grimoire.Game;
using Grimoire.UI;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Grimoire.Botting.Commands.Item
{
    // Compat shim for bots saved by Grimlite Rev (Rev declares this in Commands.Item).
    // Mapped onto Li's quest list (Configuration.Quests).
    public class CmdQuestlist : IBotCommand
    {
        public enum State
        {
            Add,
            Remove,
            Clear
        }

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

        public State state
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            switch (state)
            {
                case State.Add:
                    int quest = int.Parse(instance.ResolveVars(QuestID));
                    string item = ItemID != null ? instance.ResolveVars(ItemID) : null;
                    if (instance.Configuration.Quests.FirstOrDefault(q => q.Id == quest) == null)
                    {
                        Game.Data.Quest q = new Game.Data.Quest
                        {
                            Id = quest,
                            ItemId = item
                        };
                        q.GhostAccept();
                        BotManager.Instance.Invoke((MethodInvoker)delegate {
                            BotManager.Instance.AddQuest(quest, item, false);
                        });
                        instance.Configuration.Quests.Add(q);
                        if (instance.IsRunning)
                        {
                            instance.StartQuestList();
                        }
                    }
                    break;
                case State.Remove:
                    int removeId = int.Parse(instance.ResolveVars(QuestID));
                    Game.Data.Quest existing = instance.Configuration.Quests.FirstOrDefault(q => q.Id == removeId);
                    if (existing != null)
                        instance.Configuration.Quests.Remove(existing);
                    break;
                case State.Clear:
                    instance.Configuration.Quests.Clear();
                    break;
            }
            await Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            switch (state)
            {
                case State.Add:
                    return $"Add to quest list: {QuestID}, {(ItemID != null ? ItemID : null)}";
                case State.Remove:
                    return $"Remove from quest list: {QuestID}, {(ItemID != null ? ItemID : null)}";
                case State.Clear:
                    return "Clear quest list";
            }
            return "Quest list";
        }
    }
}

namespace Grimoire.Botting.Commands.Quest
{
    // Some Rev builds declare CmdQuestlist under Commands.Quest instead of Commands.Item.
    public class CmdQuestlist : global::Grimoire.Botting.Commands.Item.CmdQuestlist { }
}
