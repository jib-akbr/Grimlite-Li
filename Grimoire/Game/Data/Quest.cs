using Grimoire.Tools;
using Newtonsoft.Json;
using System.Collections.Generic;
using Grimoire.Networking;
using System.Linq;
using System;
using Newtonsoft.Json.Linq;

namespace Grimoire.Game.Data
{
    public class Quest
    {
        [JsonProperty("sFaction")]
        public string Faction
        {
            get;
            set;
        }

        [JsonProperty("iClass")]
        public int? ClassPointsReward
        {
            get;
            set;
        }

        //[JsonProperty("oRewards")]
        //[JsonConverter(typeof(QuestRewardConverter))]
        //public List<ItemBase> oRewards
        //{
        //    get;
        //    set;
        //} //= new List<ItemBase>();
        [JsonProperty("oRewards")]
        public JObject oRewardsRaw
        {
            get;
            set;
        } //= new List<ItemBase>();

        [JsonIgnore]
        public List<InventoryItem> selectRewards =>
            oRewardsRaw?["itemsC"]?
                .ToObject<Dictionary<string, InventoryItem>>()?
                .Values.ToList()
            ?? new List<InventoryItem>();
        [JsonIgnore]
        public List<ItemBase> oRewards => oRewardsRaw?
                .Children<JProperty>()
                .SelectMany(p => p.Value.ToObject<Dictionary<string, ItemBase>>().Values)
                .ToList()
            ?? new List<ItemBase>();

        //[JsonProperty("oReqd")]
        //[JsonConverter(typeof(QuestReqConverter))]
        //public List<ItemBase> oReqd
        //{
        //    get;
        //    set;
        //} //= new List<ItemBase>();

        [JsonProperty("sDesc")]
        public string Description
        {
            get;
            set;
        }

        [JsonProperty("iReqRep")]
        public int? RequiredReputation
        {
            get;
            set;
        }

        [JsonProperty("iRep")]
        public int? ReputationReward
        {
            get;
            set;
        }

        [JsonProperty("iLvl")]
        public int? Level
        {
            get;
            set;
        }

        //[JsonProperty("turnin")] changed to the custom one from the SWF
        [JsonProperty("RequiredItems")]
        public List<InventoryItem> RequiredItems
        {
            get;
            set;
        }

        [JsonProperty("iGold")]
        public int? GoldReward
        {
            get;
            set;
        }

        [JsonProperty("iReqCP")]
        public int? RequiredClassPoints
        {
            get;
            set;
        }

        [JsonProperty("QuestID", NullValueHandling = NullValueHandling.Ignore)]
        public int Id
        {
            get;
            set;
        }

        [JsonConverter(typeof(BoolConverter))]
        [JsonProperty("bOnce")]
        public bool IsNotRepeatable
        {
            get;
            set;
        }

        [JsonProperty("iExp")]
        public int? ExperienceReward
        {
            get;
            set;
        }

        [JsonProperty("reward")]
        public List<InventoryItem> Rewards
        {
            get;
            set;
        }

        [JsonProperty("sName")]
        public string Name
        {
            get;
            set;
        }

        [JsonConverter(typeof(BoolConverter))]
        [JsonProperty("bUpg")]
        public bool IsMemberOnly
        {
            get;
            set;
        }

        [JsonProperty("FactionID")]
        public int? FactionId
        {
            get;
            set;
        }

        private int? _iSlot;
        [JsonProperty("iSlot")]
        public int? ISlot
        {
            get => (_iSlot == null || _iSlot == -1) ? 0 : _iSlot;
            set => _iSlot = value;
        }

        [JsonProperty("iValue")]
        public int? IValue
        {
            get;
            set;
        }

        public string ItemId
        {
            get;
            set;
        }
        public bool CompleteInBlank
        {
            get;
            set;
        }

        public bool SafeRelogin
        {
            get;
            set;
        }

        public string Text
        {
            get;
            set;
        }

        public bool IsInProgress
        {
            get
            {
                return Flash.Call<bool>("IsInProgress", new string[1]
                {
                    Id.ToString()
                });
            }
        }

        public bool CanComplete
        {
            get
            {
                return Flash.Call<bool>("CanComplete", new string[1]
                {
                    Id.ToString()
                });
            }
        }

        public void Accept()
        {
            Flash.Call("Accept", Id.ToString());
        }

        public void GhostAccept()
        {
            _ = Proxy.Instance.SendToServer($"%xt%zm%acceptQuest%1%{Id}%");
        }

        public void Complete(int qty = 1, bool max = false, bool autoSelect = false)
        {
            if (max || SafeRelogin)
                qty = getMax();

            if (autoSelect || (ItemId != null && ItemId.Equals("-1")))
            {
                string _ItemId = "-1";
                if (q.selectRewards.Count > 0)
                {
                    var inv = Player.Inventory.Cache;
                    var bank = Player.Bank.Cache;
                    foreach (var item in q.selectRewards)
                    {
                        if (!Player.Inventory.ContainsItem(item, inv) &&
                            !Player.Bank.ContainsItem(item, bank))
                        {
                            _ItemId = item.Id.ToString();
                            break;
                        }
                    }
                }
                //Console.WriteLine($"INSIDE YOUR q.selectRewards count : {q.selectRewards.Count}");
                Flash.Call("Complete", Id.ToString(), qty, _ItemId);
            }
            else if (!string.IsNullOrEmpty(ItemId))
            {
                Flash.Call("Complete", Id.ToString(), qty, ItemId);
            }
            else
            {
                Flash.Call("Complete", Id.ToString(), qty);
            }
        }
        private Quest q => Player.Quests.Quest(Id);

        private int getMax()
        {
            //var quest = Player.Quests.Quest(Id);
            int complete = 10000;
            foreach (var req in q.RequiredItems)
            {
                //LogForm.Instance.devDebug($"{req.Name} is temp? {req.IsTemporary}");
                int owned = 0;
                if (req.IsTemporary)
                {
                    //var tempItem = Player.TempInventory.Items.Find(i => i.Id == req.Id);
                    owned = Player.TempInventory.Items.Find(i => i.Id == req.Id)?.Quantity ?? 0;
                }
                else
                {
                    //var invItem = Player.Inventory.Items.Find(i => i.Id == req.Id);
                    owned = Player.Inventory.Items.Find(i => i.Id == req.Id)?.Quantity ?? 0;
                }

                int required = req.Quantity;
                int possible = owned / required;

                complete = Math.Min(complete, possible);
            }
            int alt = getMaxCall ?? 0;
            complete = Math.Max(alt, complete);
            return complete;
        }
        private int? getMaxCall => int.Parse(Flash.CallGameFunction2("world.maximumQuestTurnIns", Id));

        public override string ToString()
        {
            string itemId = ItemId != null ? $": {ItemId}" : "";
            string safeRelogin = SafeRelogin ? " [SafeRelogin]" : "";
            return $"{Id}{itemId}{safeRelogin}";
        }

        #region ShouldSerialize
        public bool ShouldSerializeFaction() => false;
        public bool ShouldSerializeISlot() => false;

        public bool ShouldSerializeClassPointsReward() => false;

        public bool ShouldSerializeDescription() => false;

        public bool ShouldSerializeRequiredReputation() => false;

        public bool ShouldSerializeReputationReward() => false;

        public bool ShouldSerializeLevel() => false;

        public bool ShouldSerializeGoldReward => false;

        public bool ShouldSerializeRequiredClassPoints => false;

        public bool ShouldSerializeIsNotRepeatable => false;

        public bool ShouldSerializeExperienceReward => false;

        public bool ShouldSerializeRewards => false;

        public bool ShouldSerializeName => false;

        public bool ShouldSerializeIsMemberOnly => false;

        public bool ShouldSerializeFactionId => false;

        public bool ShouldSerializeIsInProgress => false;

        public bool ShouldSerializeCanComplete => false;
        #endregion
    }
}