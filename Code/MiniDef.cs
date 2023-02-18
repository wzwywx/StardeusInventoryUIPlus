using System.Collections.Generic;
using Game;
using Game.UI;
using KL.Utils;

namespace InventoryUIPlus.Data
{

    public struct MiniDef
    {
        public string Id;
        public string DefId;
        public string IconId;

        public string Category;
        public bool IsExperimental;

    }
    public sealed class Materials
    {

        // singleton pattern
        private static Materials _instance;


        // thread unsafe singleton for now
        // TODO: Better fix this later
        public static Materials Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Materials();
                    _instance.LoadKnownMaterials();
                }
                return _instance;
            }

        }
        private Materials()
        {

        }

        private bool _isMaterialsLoaded;

        public bool IsMaterialsLoaded => _isMaterialsLoaded;

        // the key will be the defId while the whole MiniDef will be the key
        private readonly Dictionary<string, MiniDef> All = new Dictionary<string, MiniDef>();

        // TODO: Just realized that I don't need this
        private void LoadKnownMaterials()
        {
            D.Warn("Loading known materials");
            string configParent = "Config/Materials";
            // string configChild = "Building";

            if (_isMaterialsLoaded)
            {
                return;
            }

            List<MiniDef> tempVals = new List<MiniDef> { };

            string[] configChilds = new string[] { "Building", "Organic", "Ore", "Electronics" };
            foreach (string childPath in configChilds)
            {
                string fullConfigPath = $"{configParent}/{childPath}";

                D.Warn($"Trying to load {fullConfigPath}");

                foreach (MiniDef item in The.ModLoader.LoadObjects<MiniDef>(fullConfigPath))
                {
                    string id = item.DefId is not null ? item.DefId : item.IconId;
                    if (item.IsExperimental && !A.IsExperimentalOn)
                    {
                        continue;
                    }
                    if (All.ContainsKey(id))
                    {
                        string text = "Trying to load Equipment with Id that already exists: " + id;
                        D.Err(text);
                        UIPopupWidget.Spawn("Icons/Color/Warning", "GAME BREAKING BUG", text);
                        continue;
                    }

                    MiniDef itemCopy = item;
                    itemCopy.Category = childPath;

                    All.Add(id, itemCopy);

                }
            }

            _isMaterialsLoaded = true;




        }

        public string GetCategory(string defId)
        {
            MiniDef miniDef;
            if (All.TryGetValue(defId, out miniDef))
            {
                return miniDef.Category;
            }
            else
            {
                return "Undefined";
            }
        }
    }
}