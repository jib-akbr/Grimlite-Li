
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc
{
    public class CmdInt : IBotCommand
    {
        public enum Types
        {
            Set,
            Upper,
            Lower
        }

        public Types type
        {
            get;
            set;
        }

        public string Int
        {
            get;
            set;
        }

        public int Value
        {
            get;
            set;
        }

        public Task Execute(IBotEngine instance)
        {
            if (!Configuration.Tempvalues.ContainsKey(Int))
                Configuration.Tempvalues.Add(Int, 0);
            switch (type)
            {
                case Types.Set:
                    Configuration.Tempvalues[Int] = Value;
                    break;
                case Types.Upper:
                    Configuration.Tempvalues[Int]++;
                    break;
                case Types.Lower:
                    Configuration.Tempvalues[Int]--;
                    break;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            switch (type)
            {
                case Types.Set:
                    return $"Set {Int}: {Value}";
                case Types.Upper:
                    return $"Increase {Int} by 1";
                default:
                    return $"Decrease {Int} by 1";
            }
        }
    }
    public class CmdInt2 : IBotCommand
    {
        //this is the same as above, but value field is string also can uses var/int
        //Example [cycle] = [cycle]+2 => 2
        public string IntKey
        {
            get;
            set;
        }

        public string Value
        {
            get;
            set;
        }

        public Task Execute(IBotEngine instance)
        {
            if (!int.TryParse(instance.ResolveVars(Value), out int _value))
                return Task.FromResult<object>(null);
            //returned early if things arent Int

            //if (!Configuration.Tempvalues.ContainsKey(IntKey))
            //    Configuration.Tempvalues.Add(IntKey, 0);
            Configuration.Tempvalues[IntKey] = _value;
            currentvalue = _value.ToString();
            return Task.FromResult<object>(null);
        }
        private string currentvalue = null;
        public override string ToString()
        {
            return $"Set Int [{IntKey}] : {Value}" + (!string.IsNullOrWhiteSpace(currentvalue) ? $" => {currentvalue}" : "");
            /*switch (type)
            {
                case Types.Set:
                    return $"Set {IntKey}: {Value}";
                case Types.Upper:
                    return $"Increase {IntKey} by 1";
                default:
                    return $"Decrease {IntKey} by 1";
            }*/
        }
    }
}