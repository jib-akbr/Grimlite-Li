using Grimoire.Game;
using Grimoire.Tools;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grimoire.Botting.Commands.Misc.Statements
{
    // Compat shims for bots saved by Grimlite Rev (old names for Li's monster counters).
    public class CmdAvailableMonstersGreaterThan : CmdMonstersGreaterThan { }
    public class CmdAvailableMonstersLessThan : CmdMonstersLessThan { }
    public class CmdVisibleMonstersGreaterThan : CmdMonstersGreaterThan { }
    public class CmdVisibleMonstersLessThan : CmdMonstersLessThan { }

    // Compat shims for bots saved by Grimlite Rev (plural name for Li's my-cell counters).
    public class CmdPlayersInMyCellGreaterThan : CmdPlayerInMyCellGreaterThan { }
    public class CmdPlayersInMyCellLessThan : CmdPlayerInMyCellLessThan { }

    // Compat shims for bots saved by Grimlite Rev (old PlayerAura value scheme).
    public class CmdPlayerAuraValIs : CmdPlayerAuraEquals { }
    public class CmdPlayerAuraValIsGreaterThan : CmdPlayerAuraGreaterThan { }
    public class CmdPlayerAuraValIsLessThan : CmdPlayerAuraLessThan { }

    // Compat shims for bots saved by Grimlite Rev (old TargetAura value scheme).
    public class CmdTargetAuraValIs : CmdTargetAuraEquals { }
    public class CmdTargetAuraValIsGreaterThan : CmdTargetAuraGreaterThan { }
    public class CmdTargetAuraValIsLessThan : CmdTargetAuraLessThan { }

    public class CmdPlayerAuraValIsNot : StatementCommand, IBotCommand
    {
        public CmdPlayerAuraValIsNot()
        {
            Tag = "Aura";
            Text = "Player aura value is not";
        }

        public Task Execute(IBotEngine instance)
        {
            string aura = instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1;
            string auraValue = instance.IsVar(Value2) ? Configuration.Tempvariable[instance.GetVar(Value2)] : Value2;
            int x = 0;
            int.TryParse(auraValue, out x);
            if (Player.GetAuras(true, aura) == x)
                instance.Index++;
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player aura is not: {Value1}, {Value2}";
        }
    }

    public class CmdTargetAuraValIsNot : StatementCommand, IBotCommand
    {
        public CmdTargetAuraValIsNot()
        {
            Tag = "Aura";
            Text = "Target aura value is not";
        }

        public Task Execute(IBotEngine instance)
        {
            string aura = instance.IsVar(Value1) ? Configuration.Tempvariable[instance.GetVar(Value1)] : Value1;
            string auraValue = instance.IsVar(Value2) ? Configuration.Tempvariable[instance.GetVar(Value2)] : Value2;
            int x = 0;
            int.TryParse(auraValue, out x);
            if (Player.GetAuras(false, aura) == x)
                instance.Index++;
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Target aura is not: {Value1}, {Value2}";
        }
    }

    public class CmdHealthPercentageGreaterThan : StatementCommand, IBotCommand
    {
        public CmdHealthPercentageGreaterThan()
        {
            Tag = "This player";
            Text = "Health percentage is greater than";
        }

        public Task Execute(IBotEngine instance)
        {
            if ((Player.Health / (double)Player.HealthMax * 100.0) <= int.Parse(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Health percentage is greater than: " + Value1;
        }
    }

    public class CmdHealthPercentageLessThan : StatementCommand, IBotCommand
    {
        public CmdHealthPercentageLessThan()
        {
            Tag = "This player";
            Text = "Health percentage is less than";
        }

        public Task Execute(IBotEngine instance)
        {
            if ((Player.Health / (double)Player.HealthMax * 100.0) >= int.Parse(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Health percentage is less than: " + Value1;
        }
    }

    public class CmdGetClassWithVar : StatementCommand, IBotCommand
    {
        public CmdGetClassWithVar()
        {
            Tag = "This player";
            Text = "Get equipped class with Variable";
        }

        public Task Execute(IBotEngine instance)
        {
            if (!Configuration.Tempvariable.ContainsKey(Value1))
                Configuration.Tempvariable.Add(Value1, Player.EquippedClass);
            else
                Configuration.Tempvariable[Value1] = Player.EquippedClass;
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Get equipped class with Variable: {Value1}";
        }
    }

    public class CmdSkillIsAvailable : StatementCommand, IBotCommand
    {
        public CmdSkillIsAvailable()
        {
            Tag = "This player";
            Text = "Skill is available";
        }

        public Task Execute(IBotEngine instance)
        {
            if (Player.SkillAvailable(instance.ResolveVars(Value1)) > 0)
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Skill is available:" + Value1;
        }
    }

    public class CmdSkillIsNotAvailable : StatementCommand, IBotCommand
    {
        public CmdSkillIsNotAvailable()
        {
            Tag = "This player";
            Text = "Skill is not available";
        }

        public Task Execute(IBotEngine instance)
        {
            if (Player.SkillAvailable(instance.ResolveVars(Value1)) <= 0)
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return "Skill is not available:" + Value1;
        }
    }

    public class CmdPlayerInCombat : StatementCommand, IBotCommand
    {
        public CmdPlayerInCombat()
        {
            Tag = "Player";
            Text = "Player is in combat";
        }

        public Task Execute(IBotEngine instance)
        {
            if (!IsPlayerInCombat(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player is in combat: {Value1}";
        }

        internal static bool IsPlayerInCombat(string name)
        {
            try
            {
                string reqs = Flash.Call<string>("CheckPlayerInCombat", new string[] { name });
                if (bool.TryParse(reqs, out bool result))
                    return result;
            }
            catch { }
            if ((name ?? "").Trim().EqualsIgnoreCase(Player.Username))
                return Player.CurrentState == Player.State.InCombat;
            return false;
        }
    }

    public class CmdPlayerNotInCombat : StatementCommand, IBotCommand
    {
        public CmdPlayerNotInCombat()
        {
            Tag = "Player";
            Text = "Player is not in combat";
        }

        public Task Execute(IBotEngine instance)
        {
            if (CmdPlayerInCombat.IsPlayerInCombat(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player is not in combat: {Value1}";
        }
    }

    public class CmdPlayerEquippedClass : StatementCommand, IBotCommand
    {
        public CmdPlayerEquippedClass()
        {
            Tag = "Player";
            Text = "Player's equipped class is";
        }

        public Task Execute(IBotEngine instance)
        {
            if (!HasClass(instance.ResolveVars(Value1), instance.ResolveVars(Value2)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player's equipped class is: {Value1}, {Value2}";
        }

        internal static bool HasClass(string player, string className)
        {
            try
            {
                string reqs = Flash.Call<string>("CheckPlayerClass", new string[] { player, className });
                if (bool.TryParse(reqs, out bool result))
                    return result;
            }
            catch { }
            if ((player ?? "").Trim().EqualsIgnoreCase(Player.Username))
                return (Player.EquippedClass ?? "").Trim().EqualsIgnoreCase((className ?? "").Trim());
            return false;
        }
    }

    public class CmdPlayerNotEquippedClass : StatementCommand, IBotCommand
    {
        public CmdPlayerNotEquippedClass()
        {
            Tag = "Player";
            Text = "Player's equipped class is not";
        }

        public Task Execute(IBotEngine instance)
        {
            if (CmdPlayerEquippedClass.HasClass(instance.ResolveVars(Value1), instance.ResolveVars(Value2)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player's equipped class is not: {Value1}, {Value2}";
        }
    }

    public class CmdPlayersInCellGreaterThan : StatementCommand, IBotCommand
    {
        public CmdPlayersInCellGreaterThan()
        {
            Tag = "Player";
            Text = "Player count in cell is greater than";
        }

        public Task Execute(IBotEngine instance)
        {
            if (CountPlayersInCell(instance.ResolveVars(Value1)) <= int.Parse(instance.ResolveVars(Value2)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player count in {Value1} is greater than: {Value2}";
        }

        internal static int CountPlayersInCell(string cell)
        {
            int count = 0;
            try
            {
                if (cell == "Blank")
                    cell = "Wait";
                List<string> players = World.PlayersInMap;
                if (players == null)
                    return 0;
                foreach (string pl in players)
                {
                    string reqs = Flash.Call<string>("CheckCellPlayer", new string[] { pl, cell });
                    if (bool.TryParse(reqs, out bool result) && result)
                    {
                        count++;
                    }
                }
            }
            catch { }
            return count;
        }
    }

    public class CmdPlayersInCellLessThan : StatementCommand, IBotCommand
    {
        public CmdPlayersInCellLessThan()
        {
            Tag = "Player";
            Text = "Player count in cell is less than";
        }

        public Task Execute(IBotEngine instance)
        {
            if (CmdPlayersInCellGreaterThan.CountPlayersInCell(instance.ResolveVars(Value1)) >= int.Parse(instance.ResolveVars(Value2)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player count in {Value1} is less than: {Value2}";
        }
    }

    internal static class RevAura
    {
        public static bool PlayerActive(string aura)
        {
            return Player.GetAuras(true, aura) > 0;
        }

        public static bool TargetActive(string aura)
        {
            return Player.GetAuras(false, aura) > 0;
        }
    }

    public class CmdPlayerAuraIsActive : StatementCommand, IBotCommand
    {
        public CmdPlayerAuraIsActive()
        {
            Tag = "Player Aura";
            Text = "Player Aura is active";
        }

        public Task Execute(IBotEngine instance)
        {
            if (!RevAura.PlayerActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player Aura is active: {Value1}";
        }
    }

    public class CmdPlayerAuraIsNotActive : StatementCommand, IBotCommand
    {
        public CmdPlayerAuraIsNotActive()
        {
            Tag = "Player Aura";
            Text = "Player Aura is not active";
        }

        public Task Execute(IBotEngine instance)
        {
            if (RevAura.PlayerActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player Aura is not active: {Value1}";
        }
    }

    public class CmdPlayerAuraStringValIs : StatementCommand, IBotCommand
    {
        public CmdPlayerAuraStringValIs()
        {
            Tag = "Player Aura";
            Text = "Player Aura's value is (string)";
        }

        public Task Execute(IBotEngine instance)
        {
            if (!RevAura.PlayerActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player Aura's value is (string): {Value1}, {Value2}";
        }
    }

    public class CmdPlayerAuraStringValIsNot : StatementCommand, IBotCommand
    {
        public CmdPlayerAuraStringValIsNot()
        {
            Tag = "Player Aura";
            Text = "Player Aura's value is not (string)";
        }

        public Task Execute(IBotEngine instance)
        {
            if (RevAura.PlayerActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Player Aura's value is not (string): {Value1}, {Value2}";
        }
    }

    public class CmdTargetAuraIsActive : StatementCommand, IBotCommand
    {
        public CmdTargetAuraIsActive()
        {
            Tag = "Target Aura";
            Text = "Target Aura is active";
        }

        public Task Execute(IBotEngine instance)
        {
            if (!RevAura.TargetActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Target Aura is active: {Value1}";
        }
    }

    public class CmdTargetAuraIsNotActive : StatementCommand, IBotCommand
    {
        public CmdTargetAuraIsNotActive()
        {
            Tag = "Target Aura";
            Text = "Target Aura is not active";
        }

        public Task Execute(IBotEngine instance)
        {
            if (RevAura.TargetActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Target Aura is not active: {Value1}";
        }
    }

    public class CmdTargetAuraStringValIs : StatementCommand, IBotCommand
    {
        public CmdTargetAuraStringValIs()
        {
            Tag = "Target Aura";
            Text = "Target Aura's value is (string)";
        }

        public Task Execute(IBotEngine instance)
        {
            if (!RevAura.TargetActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Target Aura's value is (string): {Value1}, {Value2}";
        }
    }

    public class CmdTargetAuraStringValIsNot : StatementCommand, IBotCommand
    {
        public CmdTargetAuraStringValIsNot()
        {
            Tag = "Target Aura";
            Text = "Target Aura's value is not (string)";
        }

        public Task Execute(IBotEngine instance)
        {
            if (RevAura.TargetActive(instance.ResolveVars(Value1)))
            {
                instance.Index++;
            }
            return Task.FromResult<object>(null);
        }

        public override string ToString()
        {
            return $"Target Aura's value is not (string): {Value1}, {Value2}";
        }
    }
}
