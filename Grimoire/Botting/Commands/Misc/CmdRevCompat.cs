using Grimoire.Game;
using Grimoire.UI;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    // Compat shim for bots saved by Grimlite Rev.
    public class CmdLoadMapSwf : IBotCommand
    {
        public string Name
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            string name = instance.ResolveVars(Name);
            if (string.IsNullOrEmpty(name) || name == "Map filename (.swf)")
            {
                return;
            }
            await instance.WaitUntil(() => World.IsActionAvailable(LockActions.Transfer));
            World.LoadMap(name);
            await instance.WaitUntil(() => !World.IsMapLoading, null, 40);
        }

        public override string ToString()
        {
            return $"Load Map SWF: {Name}";
        }
    }

    // Compat shim for bots saved by Grimlite Rev.
    // Li has no client-level cheat, so this loads and runs as a no-op.
    public class CmdSetClientLevel : IBotCommand
    {
        public Task Execute(IBotEngine instance)
        {
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Set client level to max";
        }
    }

    // Compat shim for bots saved by Grimlite Rev (mapped onto Li's provoke).
    public class CmdToggleProvokeInMap : IBotCommand
    {
        public int Type
        {
            get;
            set;
        }

        public string ProvokePacket
        {
            get;
            set;
        }

        public async Task Execute(IBotEngine instance)
        {
            bool on = OptionsManager.ProvokeMonsters;
            switch (Type)
            {
                case 0:
                    on = false;
                    Player.MoveToCell(Player.Cell, Player.Pad);
                    break;
                case 1:
                    on = true;
                    break;
                default:
                    on = !on;
                    break;
            }
            OptionsManager.ProvokeMonsters = on;
            BotManager.Instance.chkProvoke.Checked = on;
            await Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            switch (Type)
            {
                case 0:
                    return "Provoke in map: Off";
                case 1:
                    return "Provoke in map: On";
                default:
                    return "Provoke in map: Toggle";
            }
        }
    }
}
