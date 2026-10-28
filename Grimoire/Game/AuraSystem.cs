using Grimoire.Botting;
using Grimoire.UI;
using Grimoire.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Grimoire.Game
{
    public class PlayerAura
    {
        public string Name { get; private set; }
        public double Value { get; set; }
        public double Duration { get; private set; }
        public int ExpireTime { get; private set; }
        public PlayerAura(string name, double value, double duration)
        {
            Name = name;
            Value = value;
            Duration = duration;
            Refresh(value, duration);
        }
        public void Refresh(double value, double duration)
        {
            Value = value;
            Duration = duration;
            ExpireTime = (int)(Environment.TickCount + duration * 1000);
        }
        public void Refresh(double duration)
        {
            Duration = duration;
            ExpireTime = (int)(Environment.TickCount + duration * 1000);
        }

        public bool IsExpired()
        {
            return Environment.TickCount >= ExpireTime;
        }

        //in miliseconds
        public int RemainingTime()
        {
            int remain = ExpireTime - Environment.TickCount;
            return remain > 0 ? remain : 0;
        }
    }
    public class AuraManager
    {
        public static AuraManager SelfAura { get; } = new AuraManager();
        private static readonly Dictionary<int, AuraManager> _enemyAuras =
        new Dictionary<int, AuraManager>();

        private Dictionary<string, PlayerAura> auras = new
        Dictionary<string, PlayerAura>(StringComparer.OrdinalIgnoreCase);

        private AuraManager() { }
        public static AuraManager EnemyAura(int monsterId)
        {
            if (!_enemyAuras.TryGetValue(monsterId, out AuraManager manager))
            {
                manager = new AuraManager();
                _enemyAuras[monsterId] = manager;
            }
            return manager;
        }
        public static void resetMonsterAura()
        {
            _enemyAuras.Clear();
        }
       /*void debug(string msg, PlayerAura aura = null)
        {
            if (aura != null)
                LogForm.Instance.devDebug($"[AuraRefreshed] {msg} {aura.Name} (val : {aura.Value})");
            else
                LogForm.Instance.devDebug($"[AuraAdded] {msg} {aura.Name}");
        }*/

        public void ApplyAura(string name, double value, double duration)
        {
            if (auras.TryGetValue(name, out PlayerAura aura))
                aura.Refresh(value, duration);
            else
                auras[name] = new PlayerAura(name, value, duration);
            //debug($"{name} aura added", aura);
        }
        public void AuraIncrement(string name)
        {
            if (auras.TryGetValue(name, out PlayerAura aura))
                aura.Value++;
            aura.Refresh(aura.Duration);
            //debug("+1 stack for", aura);
        }

        public void RemoveAura(string name)
        {
            auras.Remove(name);
        }

        public bool HasAura(string name)
        {
            return auras.ContainsKey(name);
        }

        public double GetValue(string name)
        {
            if (auras.TryGetValue(name, out PlayerAura aura) && !aura.IsExpired())
            {
                if (aura.Value <= 1)
                    aura.Value = 1;
                return aura.Value;
            }

            return 0;
        }

        public int GetRemainingTime(string name)
        {
            return auras.TryGetValue(name, out PlayerAura aura)
                ? aura.RemainingTime()
                : 0;
        }

        public void ClearAllAura(bool forceClean = false)
        {
            Dictionary<string, PlayerAura> addback = new Dictionary<string, PlayerAura>(StringComparer.OrdinalIgnoreCase);
            if (forceClean)
            {
                auras = addback;
                return;
            }

            foreach (PlayerAura aura in auras.Values)
            {
                if (TE.Any(filter => aura.Name.ContainsIgnoreCase(filter)))
                    addback.Add(aura.Name, aura);
            }

            auras = addback;
        }
        private string[] TE = { "Fate", "Sage", "Body", "Might", "Elixir" };

        public void Update()
        {
            var expired = auras
                .Where(a => a.Value.IsExpired())
                .Select(a => a.Key)
                .ToList();

            foreach (var key in expired)
                RemoveAura(key);
        }
    }
}

