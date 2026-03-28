using System;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdGotoLabel : IBotCommand
    {
        public string Label
        {
            get;
            set;
        }

        public Task Execute(IBotEngine instance)
        {
            string Value1 = Label;
            int num = instance.Configuration.Commands.FindIndex((IBotCommand c) => c is CmdLabel label && label.Name.EqualsIgnoreCase(instance.ResolveVars(Value1)));
            if (num > -1)
            {
                instance.Index = num;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Goto label: " + Label;
        }
    }
}