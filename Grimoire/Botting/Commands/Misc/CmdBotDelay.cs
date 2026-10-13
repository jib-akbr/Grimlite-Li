using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdBotDelay : IBotCommand
    {
        public int Delay
        {
            get;
            set;
        }

        public Task Execute(IBotEngine instance)
        {
            instance.Configuration.BotDelay = Delay;
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Set bot delay: {Delay}";
        }
    }

    // Compat shim for bots saved by Grimlite Rev (delay as string/var).
    public class CmdBotDelay2 : IBotCommand
    {
        public string Delay
        {
            get;
            set;
        }

        public Task Execute(IBotEngine instance)
        {
            instance.Configuration.BotDelay = int.Parse(instance.ResolveVars(Delay));
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Set bot delay: {Delay}";
        }
    }
}