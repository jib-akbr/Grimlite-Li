using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Grimoire.Game.Data;

namespace Grimoire.Tools
{
    /// <summary>
    /// Manages saving, loading, and persistence of skillsets
    /// Keeps game state separate from data persistence operations
    /// </summary>
    public class SkillSetManager
    {
        private static SkillSetManager _instance;
        private readonly string _skillsetsDir;
        private readonly string _collectionPath;
        private SkillSetsCollection _collection;

        public static SkillSetManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SkillSetManager();
                }
                return _instance;
            }
        }

        public SkillSetManager()
        {
            _skillsetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skillsets");
            _collectionPath = Path.Combine(_skillsetsDir, "skillsets.json");
            
            // Ensure directory exists
            if (!Directory.Exists(_skillsetsDir))
            {
                Directory.CreateDirectory(_skillsetsDir);
            }

            LoadCollection();
        }

        /// <summary>
        /// Loads the skillsets collection from disk
        /// </summary>
        private void LoadCollection()
        {
            try
            {
                if (File.Exists(_collectionPath))
                {
                    string json = File.ReadAllText(_collectionPath);
                    _collection = JsonConvert.DeserializeObject<SkillSetsCollection>(json) ?? new SkillSetsCollection();
                }
                else
                {
                    _collection = new SkillSetsCollection();
                }
            }
            catch
            {
                _collection = new SkillSetsCollection();
            }
        }

        /// <summary>
        /// Saves the skillsets collection to disk
        /// </summary>
        private void SaveCollection()
        {
            try
            {
                _collection.LastUpdated = DateTime.Now;
                string json = JsonConvert.SerializeObject(_collection, Formatting.Indented);
                File.WriteAllText(_collectionPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving skillsets collection: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves a new skillset with the given name and skills
        /// Does NOT affect bot game state
        /// </summary>
        public bool SaveSkillSet(string name, List<SavedSkill> skills)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return false;

                // Remove existing skillset with same name
                _collection.SkillSets.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                // Add new skillset
                var skillSetData = new SkillSetData(name, skills);
                _collection.SkillSets.Add(skillSetData);

                SaveCollection();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving skillset: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a skillset by name
        /// Returns null if not found
        /// </summary>
        public SkillSetData LoadSkillSet(string name)
        {
            try
            {
                return _collection.SkillSets.FirstOrDefault(s => 
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading skillset: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all available skillset names
        /// </summary>
        public List<string> GetAllSkillSetNames()
        {
            try
            {
                return _collection.SkillSets.Select(s => s.Name).OrderBy(n => n).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets all skillsets
        /// </summary>
        public List<SkillSetData> GetAllSkillSets()
        {
            try
            {
                return _collection.SkillSets.OrderByDescending(s => s.LastModified).ToList();
            }
            catch
            {
                return new List<SkillSetData>();
            }
        }

        /// <summary>
        /// Deletes a skillset by name
        /// </summary>
        public bool DeleteSkillSet(string name)
        {
            try
            {
                if (_collection.SkillSets.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    SaveCollection();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting skillset: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a skillset exists
        /// </summary>
        public bool SkillSetExists(string name)
        {
            return _collection.SkillSets.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reloads skillsets from disk
        /// </summary>
        public void Reload()
        {
            LoadCollection();
        }
    }
}
