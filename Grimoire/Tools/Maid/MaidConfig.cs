namespace Grimoire.Tools.Maid
{
	internal class MaidConfig
	{
		public string Target{ get; set; }
		public string SkillList{ get; set; }
		public string LockedZoneHandlerMaps{ get; set; }
        public string SafeSkillList { get; set; }
        public string BuffStopAttackList { get; set; }
        public string AttackPriorityMonster { get; set; }
        public string SpecialMsg { get; set; }
        public string WhitelistMapMaps{ get; set; }
		public int? RelogDelay{ get; set; }
        public int? SkillDelay { get; set; }
        public int? SafeSkillHP { get; set; }
        public int? SpecialAct { get; set; }
        public int? UltraBossExtra { get; set; }
        public bool? AntiCounter { get; set; }
        public bool? CopyWalk { get; set; }
        public bool? AttackPriority { get; set; }
        public bool? BuffStopAttack { get; set; }
        public bool? WaitSkill { get; set; }
        public bool? StopFailedGoto { get; set; }
        public bool? LockedZoneHandler { get; set; }
        public bool? WhitelistMap { get; set; }
        public bool? GlobalHotkey{ get; set; }
		public bool? SafeSkill{ get; set; }
		
    }
}
