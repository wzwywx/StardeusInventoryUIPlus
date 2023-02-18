using System.Collections.Generic;
using Game.Components;
using Game.Data;
using Game.Systems;
using KL.Utils;
using UnityEngine;
using InventoryUIPlus.Data;
using Game;
using Game.Constants;
using System.Text.RegularExpressions;
using System.Linq;

namespace InventoryUIPlus
{
    public sealed class InventoryUIPlusSys : GameSystem, IOverlayProvider, ISaveable
    {
        // The convention is that all systems end with Sys, and SysId is equal to the class name
        public const string SysId = "InventoryUIPlusSys";
        public override string Id => SysId;
        // If your system can work in sandbox too, set this to false
        public override bool SkipInSandbox => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            GameSystems.Register(SysId, () => new InventoryUIPlusSys());
        }

        // If your system adds some mechanic that may not be compatible with
        // old saves, you have to set the MinRequiredVersion
        //public override string MinRequiredVersion => "0.6.89";

        private GameState state;

        public List<OverlayInfo> Overlays => overlays;
        private readonly List<OverlayInfo> overlays = new();

        private InventoryUIPlusUI ui;

        // public so that the UI can use it
        public int SomeVariable;

        private readonly Dictionary<MatType, int> availableCache = new Dictionary<MatType, int>();

        protected override void OnInitialize()
        {
            overlays.Clear();
            overlays.Add(overlayInfo);
            ui = new InventoryUIPlusUI(this);

            D.Warn($"Custom materials data is {(Materials.Instance.IsMaterialsLoaded ? "" : "not")} loaded");

            S.Sig.AfterLoadState.AddListener(OnLoadSave);
            S.Sig.AreasInitialized.AddListener(OnAreasInit);
            S.Sig.ToggleOverlay.AddListener(OnToggleOverlay);
        }
        // You will have to create Graphics/Icons/White/ExampleModIcon.png
        private readonly OverlayInfo overlayInfo = new OverlayInfo(9999, SysId,
            "Icons/White/MaterialsPlus");

        private void OnToggleOverlay(OverlayInfo info, bool on)
        {
            // D.Warn("Toggling overlay: {0} -> {1}", info.Id, on);
            if (!on || info.Id != SysId)
            {
                ui.HideUI();
            }
            else
            {
                ui.ShowUI();
            }
        }

        private void OnLoadSave(GameState state)
        {
            this.state = state;
        }

        // If your system depends on AreasSys, for example, you may want to
        // start ticking your system only after initial areas have been built
        private void OnAreasInit()
        {
            D.Warn("Areas initialized");
            InitializeCache();

        }

        public override void Unload()
        {
            // Release the resources here
        }

        public string GetName()
        {
            return Id;
        }

        // We use ComponentData for saving entity components, but it can be
        // used in systems as well, if the system is marked ISaveable
        private ComponentData data;

        public ComponentData Save()
        {
            data ??= new ComponentData(0, SysId);
            data.SetInt("SomeVariable", SomeVariable);
            return data;
        }

        public void Load(ComponentData data)
        {
            this.data = data;
            SomeVariable = data.GetInt("SomeVariable", 0);
        }

        public readonly Dictionary<string, MiniDef> PlusCache = new();

        private void InitializeCache()
        {
            D.Warn("InitializeCache");

            // Dictionary<string, MiniDef> cache = new();
            S.Sys.Inventory.LoadRemainingMaterials(availableCache);


            foreach (KeyValuePair<MatType, int> mat in availableCache)
            {
                var def = The.Defs.TryGet(mat.Key.DefId);
                var quantity = mat.Value;
                var category = Categorize(mat.Key);

                MiniDef miniDef = new MiniDef();
                // TODO: Make a constructor later
                miniDef.DefId = mat.Key.DefId;
                // miniDef.Quantity = quantity;
                miniDef.Category = category;

                D.Warn(miniDef.Quantity.ToString());

                PlusCache.Add(mat.Key.DefId, miniDef);

                // TODO: Bookmark
            }

        }

        private string Categorize(MatType mat)
        {
            Def def = The.Defs.TryGet(mat.DefId);
            string compType;

            // check processable
            if (def.HasComponent(T.Processable))
            {
                foreach (ComponentConfig component in def.Components)
                {
                    if (component.ToString() == "CompCfg[Processable]")
                    {
                        var properties = component.Properties;
                        if (properties is null)
                        {
                            D.Warn("No properties");
                            continue;
                        }
                        foreach (var p in properties)
                        {
                            if (p.Key == "Type")
                            {
                                compType = p.String;
                                return compType;
                            }
                        }
                    }
                }
            }

            compType = mat.Group;

            // prioritize categorizing with mat.Group before everything else
            if (mat.Group is not null)
            {
                compType = Regex.Replace(compType, "([A-Z])", " $1").Trim();
                return compType;
            }

            if (Craftable.IsCraftable(def.Id))
            {
                // TODO: make this a local string later
                compType = "Craftable";
                return compType;
            }

            // TODO: put this in the same namespace to avoid namespace collision
            // Or just initialize an instance of materials

            // if fail to categorize at the end, give the "Unknown" category to material

            // if group is not available, return custom category
            if (mat.Group is null)
            {
                var pascalCategory = InventoryUIPlus.Data.Materials.Instance.GetCategory(mat.DefId);
                compType = Regex.Replace(pascalCategory, "([A-Z])", " $1").Trim();
                return compType;
            }

            // if all else fails, return Undefined
            return "Undefined";
        }
    }
}
