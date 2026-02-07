using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Grimoire.Game.Data
{
    /// <summary>
    /// Represents a saved skillset configuration
    /// </summary>
    [Serializable]
    public class SkillSetData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("skills")]
        public List<SavedSkill> Skills { get; set; } = new List<SavedSkill>();

        [JsonProperty("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.Now;

        public SkillSetData() { }

        public SkillSetData(string name, List<SavedSkill> skills)
        {
            Name = name;
            Skills = skills ?? new List<SavedSkill>();
            CreatedDate = DateTime.Now;
            LastModified = DateTime.Now;
        }
    }

    /// <summary>
    /// Represents a single saved skill entry
    /// </summary>
    [Serializable]
    public class SavedSkill
    {
        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; } = 0; // 0=Normal, 1=Safe, 2=Label

        [JsonProperty("safeType")]
        public int SafeType { get; set; } = 0;

        [JsonProperty("isSafeMp")]
        public bool IsSafeMp { get; set; } = false;

        [JsonProperty("safeValue")]
        public int SafeValue { get; set; } = 0;

        [JsonProperty("safeType2")]
        public int SafeType2 { get; set; } = 0;

        [JsonProperty("isSafeMp2")]
        public bool IsSafeMp2 { get; set; } = false;

        [JsonProperty("safeValue2")]
        public int SafeValue2 { get; set; } = 0;

        [JsonProperty("waitCooldown")]
        public bool WaitCooldown { get; set; } = false;

        [JsonProperty("waitDodge")]
        public bool WaitDodge { get; set; } = false;

        public SavedSkill() { }

        public SavedSkill(string index, string text)
        {
            Index = index;
            Text = text;
        }
    }

    /// <summary>
    /// Container for all saved skillsets
    /// </summary>
    [Serializable]
    public class SkillSetsCollection
    {
        [JsonProperty("skillsets")]
        public List<SkillSetData> SkillSets { get; set; } = new List<SkillSetData>();

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
