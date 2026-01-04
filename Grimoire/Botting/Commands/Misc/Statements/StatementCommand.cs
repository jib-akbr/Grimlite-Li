using System.Text.RegularExpressions;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    public class StatementCommand
    {
        public string Tag
        {
            get;
            set;
        }

        public string Text
        {
            get;
            set;
        }

        public string Value1
        {
            get;
            set;
        }

        public string Value2
        {
            get;
            set;
        }

        public string TauntOrder
        {
            get;
            set;
        }

        public string Delay
        {
            get;
            set;
        }

        public string Description1
        {
            get;
            set;
        }

        public string Description2
        {
            get;
            set;
        }

        /// <summary>
        /// Called when the bot starts. Override to initialize state.
        /// </summary>
        public virtual void OnBotStarted()
        {
        }

        /// <summary>
        /// Called when the bot stops. Override to clean up state.
        /// </summary>
        public virtual void OnBotStopped()
        {
        }
    }
}