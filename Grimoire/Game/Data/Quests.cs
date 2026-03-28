using Grimoire.Networking;
using Grimoire.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Grimoire.Game.Data
{
    public class Quests
    {
        private List<Quest> _questCache;
        private Dictionary<int, Quest> _questById;
        private void RefreshCache(bool force = false)
        {
            if (!force && _questCache != null && !_dirty)
                return;

            //LogForm.Instance.devDebug("[Quest Tree Refreshed]");
            int oldCount = _questCache?.Count ?? 0;
            _questCache = Flash.Call<List<Quest>>("GetQuestTree", Array.Empty<string>())
                ?? new List<Quest>(); ;
            _questById = _questCache.ToDictionary(q => q.Id);

            if (oldCount != _questCache.Count || force)
                _dirty = false;

            //LogForm.Instance.devDebug($"Quests count : {_questCache.Count}");
        }
        private bool _dirty;
        public List<Quest> QuestTree
        {
            get
            {
                RefreshCache();
                return _questCache;
            }
        }
        public void QuestTreeRefresh() => RefreshCache(true);


        public List<Quest> AcceptedQuests => QuestTree.Where((Quest q) => q.IsInProgress).ToList();

        public List<Quest> UnacceptedQuests => QuestTree.Where((Quest q) => !q.IsInProgress).ToList();

        public List<Quest> CompletedQuests => QuestTree.Where((Quest q) => q.CanComplete).ToList();

        public event Action<List<Quest>> QuestsLoaded;

        public event Action<CompletedQuest> QuestCompleted;

        public void OnQuestsLoaded(List<Quest> quests)
        {
            this.QuestsLoaded?.Invoke(quests);
            _dirty = true;
        }

        public void OnQuestCompleted(CompletedQuest quest)
        {
            //RefreshCache();
            this.QuestCompleted?.Invoke(quest);
        }

        public void Accept(int questId) => Flash.Call("Accept", questId.ToString());
        public void Accept(string questId) => _ = Proxy.Instance.SendToServer($"%xt%zm%acceptQuest%1%{questId}%");

        public void Complete(int questId) => Flash.Call("Complete", questId.ToString());

        public void Complete(string questId) => Flash.Call("Complete", questId);

        public void Complete(string questId, string itemId) => Flash.Call("Complete", itemId, bool.TrueString);

        public void Load(int id)
        {
            Flash.Call("LoadQuest", id.ToString());
            _dirty = true;
        }

        public void Load(List<int> ids)
        {
            Flash.Call("LoadQuests", string.Join(",", ids));
            _dirty = true;
        }

        public void GetQuests(List<int> ids) => Flash.Call("GetQuests", string.Join(",", ids.Select(delegate (int i) { return i.ToString(); })));

        public bool IsInProgress(int id) => Flash.Call<bool>("IsInProgress", id.ToString());

        public bool IsInProgress(string id) => Flash.Call<bool>("IsInProgress", id);

        public bool CanComplete(int id) => Flash.Call<bool>("CanComplete", id.ToString());

        public bool CanComplete(string id) => Flash.Call<bool>("CanComplete", id);
        /// <summary>
        /// Checks if the quest is green/available/eligible to accept.
        /// </summary>
        public bool IsAvailable(int id)
        {
            RefreshCache();
            return Flash.Call<bool>("IsAvailable", id.ToString());
        }

        /// <summary>
        /// Returns player iValue of iSlot
        /// </summary>
        public int progress(int iSlot)
        {
            return int.Parse(
                Flash.CallGameFunction2("world.getQuestValue", iSlot)
            );
        }
        /// <summary>
        /// Checks for quest is already loaded or not
        /// </summary>
        public bool HasQuest(int id)
        {
            RefreshCache();
            //Console.WriteLine("Quest Found? " + _questById.ContainsKey(id));
            return _questById.ContainsKey(id);
        }
        /// <summary>
        /// Grabs Quest by id from dictionary for fast access.
        /// </summary>
        public Quest Quest(int id)
        {
            RefreshCache();
            _questById.TryGetValue(id, out var q);
            //LogForm.Instance.devDebug($"Quest {q.Id} = {q.Name}");
            
            return q;
        }
    }
}