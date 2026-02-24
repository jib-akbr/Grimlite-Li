using Grimoire.Game;
using Grimoire.UI;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Item
{
    public class CmdMapItem : IBotCommand
    {
        public int ItemId
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            LogForm.Instance?.devDebug($"[CmdMapItem.Execute] Getting map item ID: {ItemId}");
            BotData.BotState = BotData.State.Others;
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.GetMapItem));
            Player.GetMapItem(ItemId);
            await Task.Delay(1500);
        }

        public override string ToString()
        {
            return $"Get map item: {ItemId}";
        }
    }
}