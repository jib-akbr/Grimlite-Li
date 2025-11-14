using System.Threading.Tasks;
using Grimoire.UI;

namespace Grimoire.Botting.Commands.Misc
{
    /// <summary>
    /// Scriptable command to control the auto-zone "Special handlers" feature
    /// from the bot script (Misc 2 tab).
    /// </summary>
    public class CmdSpecialHandler : IBotCommand
    {
        public enum SpecialMode
        {
            Start,
            Stop,
            Switch
        }

        /// <summary>
        /// Display name of the special handler/zone, e.g. "Auto Zone - Ultradage".
        /// </summary>
        public string Zone { get; set; }

        /// <summary>
        /// How this command should modify the special handler.
        /// </summary>
        public SpecialMode Mode { get; set; }

        public async Task Execute(IBotEngine instance)
        {
            if (string.IsNullOrEmpty(Zone))
                return;

            bool? enabled = null;

            switch (Mode)
            {
                case SpecialMode.Start:
                    enabled = true;
                    break;
                case SpecialMode.Stop:
                    enabled = false;
                    break;
                case SpecialMode.Switch:
                    enabled = null; // keep current on/off state, just switch zone
                    break;
            }

            BotManager.SetSpecialHandlerFromScript(Zone, enabled);

            await Task.CompletedTask;
        }

        public override string ToString()
        {
            string prefix;
            switch (Mode)
            {
                case SpecialMode.Start:
                    prefix = "Special Start";
                    break;
                case SpecialMode.Stop:
                    prefix = "Special Stop";
                    break;
                case SpecialMode.Switch:
                    prefix = "Special Switch";
                    break;
                default:
                    prefix = "Special";
                    break;
            }

            return string.IsNullOrEmpty(Zone)
                ? prefix
                : string.Format("{0}: {1}", prefix, Zone);
        }
    }
}
