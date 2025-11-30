using Grimoire.Game;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdTauntcycle : IBotCommand
    {
        public enum option
        {
            Start,
            Stop
        }
        public option type { get; set; }

        public int cycle { get; set; } = 2;
        public string target { get; set; } = "*";
        public int second { get; set; } = 14;
        public int order { get; set; } = -1;

        public Task Execute(IBotEngine instance)
        {
            int _order = order;
            if (order <= 0)
                _order = World.PlayersInMap?.IndexOf(Player.Username.ToLower()) + 1 ?? 1;

            switch (type)
            {
                case option.Start:
                    TauntCycle.Instance.StartTaunt(cycle, target, second, _order - 1);
                    break;
                case option.Stop:
                    TauntCycle.Instance.Dispose();
                    break;
            }
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            string desc = order <= 0 ? "Dynamic" : order.ToString();
            return type == option.Start ? $"{type} Tauntcycle : {cycle}T [{target}] every {second}s {desc}" : $"{type} Tauntcycle";
        }
    }
}
