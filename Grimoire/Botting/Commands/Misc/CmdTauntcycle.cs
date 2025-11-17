using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdTauntcycle : IBotCommand
    {
        public enum option
        {
            start,
            stop
        }
        public option type { get; set; }

        public int cycle { get; set; } = 2;
        public string target { get; set; } = "*";
        public int second { get; set; } = 14;
        //public int order { get; set; } = World.PlayersInMap?.IndexOf(Player.Username.ToLower())+1 ?? 1;

        public Task Execute(IBotEngine instance)
        {
            int order = World.PlayersInMap?.IndexOf(Player.Username.ToLower()) + 1 ?? 1;
            switch (type)
            {
                case option.start:
                    TauntCycle.Instance.StartTaunt(cycle, target, second, order - 1);
                    break;
                case option.stop:
                    TauntCycle.Instance.Dispose();
                    break;
            }
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            //string desc = type == option.start ? $"{type} Tauntcycle : {cycle}T [{target}] {second}" : "Stop Tauntcycle";
            return type == option.start ? $"{type} Tauntcycle : {cycle}T [{target}] {second}" : $"{type} Tauntcycle";
        }
    }
}