using Grimoire.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimoire.Game
{
    public class PlayerAura
    {
        public string Name { get; private set; }
        public double Value { get; set; }
        public int Duration { get; private set; }
        public int ExpireTime { get; private set; }
        public PlayerAura(string name, double value, int duration)
        {
            Name = name;
            Value = value;
            Duration = duration;
            Refresh(value, duration);
        }
        public void Refresh(double value, int duration)
        {
            Value = value;
            Duration = duration;
            ExpireTime = Environment.TickCount + duration * 1000;
        }
        public void Refresh(int duration)
        {
            Duration = duration;
            ExpireTime = Environment.TickCount + duration * 1000;
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
        public static AuraManager Instance { get; } = new AuraManager();

        private readonly Dictionary<string, PlayerAura> auras = new 
		Dictionary<string, PlayerAura>(StringComparer.OrdinalIgnoreCase);

        private AuraManager() { }

        /*void debug(string msg, PlayerAura aura = null)
        {
            if (aura != null)
                LogForm.Instance.devDebug($"[AuraRefreshed] {msg} {aura.Name} (val : {aura.Value})");
            else
                LogForm.Instance.devDebug($"[AuraAdded] {msg} {aura.Name}");
        }*/

        public void ApplyAura(string name, double value, int duration)
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

		public void ClearAllAura()
		{
			auras.Clear();
		}

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

