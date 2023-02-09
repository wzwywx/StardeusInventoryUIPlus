using System.Collections.Generic;
using System;
using Game.Systems;
using Game.Constants;
using Game.Data;
using Game.UI;
using Game.Utils;
using KL.Utils;
using KL.Collections;
using System.Linq;
using Game.Components;
using Game;
// using ExampleMod.Systems;

namespace SearchableInventory
{
    public sealed class SearchableInventoryUI : IUIDataProvider
    {
        private readonly SearchableInventorySys sys;
        private readonly GameState S;

        private readonly HashSet<string> foo = new();

        public SearchableInventoryUI(SearchableInventorySys sys)
        {
            this.sys = sys;
            S = sys.S;
        }

        // This doesn't belong to an entity, so let's return a null
        public Entity Entity => null;
        private UDB header;

        private void DoSomethig()
        {
            // For this example, let's increment the number and rebuild the UI
            sys.SomeVariable++;
            header.NeedsListRebuild = true;
        }

        private void OnButtonClick()
        {
            UIPopupWidget.Spawn(IconId.CWarning, "some.popup".T(),
                "Popups should be used extremely sparingly, since nobody reads this. Also, if you put the text in like this, it will be impossible to translate, use \"some.text\".T() instead.");
        }

        private void HideOverlay()
        {
            S.Sig.HideTargetOverlay.Send(sys.Id);
        }

        public void ShowUI()
        {
            S.Sig.ShowCenterPanel.Send(this);
        }

        public void HideUI()
        {
            S.Sig.HideCenterPanel.Send(this);
        }

        public string GetName()
        {
            return sys.Id;
        }

        // This only shows the context menu, so it doesn't need a main UI block
        public UDB GetUIBlock()
        {
            return null;
        }

        // private UDB sortModeBlock;

        // private S.Sys.Inventory.SortMode sortMode;

        // private System.Action ToggleSort()
        // {
        //     // S.Sys.Inventory.
        //     // sortMode = KL.Utils.Enums.Cycle(sortMode);
        //     return S.Sig.ShowCenterPanel.Send(this);
        // }

        private readonly List<IMatStorage> materialStorages = new List<IMatStorage>();
        private readonly Dictionary<MatType, int> availableCache = new Dictionary<MatType, int>();

        // Pass the cache to load remaining materials in the cache i.e. clear and fill back the materials
        // public void LoadRemainingMaterials(Dictionary<MatType, int> remainingMaterials)
        // {
        //     remainingMaterials.Clear();
        //     int count = materialStorages.Count;
        //     D.Warn($"{count} storages");

        //     foreach (MatType knownMaterial in S.Sys.Inventory.KnownMaterials)
        //     {
        //         remainingMaterials.Add(knownMaterial, 0);
        //     }
        //     for (int i = 0; i < count; i++)
        //     {
        //         IMatStorage matStorage = materialStorages[i];
        //         if (matStorage is UnstoredMatComp unstoredMatComp)
        //         {
        //             MatType type = unstoredMatComp.Type;
        //             if (type != null)
        //             {
        //                 if (remainingMaterials.TryGetValue(type, out var value))
        //                 {
        //                     remainingMaterials[type] = value + unstoredMatComp.Available(type);
        //                 }
        //                 else
        //                 {
        //                     remainingMaterials.Add(type, unstoredMatComp.Available(type));
        //                 }
        //             }
        //         }
        //         else
        //         {
        //             if (matStorage.Contents == null)
        //             {
        //                 continue;
        //             }
        //             foreach (KeyValuePair<MatType, int> content in matStorage.Contents)
        //             {
        //                 int value2 = content.Value;
        //                 MatType key = content.Key;
        //                 int value3;
        //                 if (key == null)
        //                 {
        //                     D.Err("Invalid material: {0} in {1}", value2, matStorage.Entity);
        //                 }
        //                 else if (remainingMaterials.TryGetValue(key, out value3))
        //                 {
        //                     remainingMaterials[key] = value3 + value2;
        //                 }
        //                 else
        //                 {
        //                     remainingMaterials.Add(key, value2);
        //                 }
        //             }
        //         }
        //     }
        // }
        void ToggleSort()
        {
            sortMode = Enums.Cycle(sortMode);
            S.Sig.ShowCenterPanel.Send(this);
        }
        private readonly Dictionary<MatType, UDB> blocks = new Dictionary<MatType, UDB>();

        private SortMode sortMode;
        private enum SortMode
        {
            CountsAsc, CountsDesc, Alphabetic, Category
        }

        private string sortText
        {
            get
            {
                switch (sortMode)
                {

                    case SortMode.CountsDesc:
                    case SortMode.CountsAsc:
                        return T.SortCount;
                    case SortMode.Alphabetic:
                        return T.SortName;
                    case SortMode.Category:
                        return "alternateinventory.sortmode.category".T();
                    default:
                        return string.Empty;
                }
            }
        }

        private UDB sortModeBlock;

        public void GetUIDetails(List<UDB> res)
        {
            S.Sys.Inventory.LoadRemainingMaterials(availableCache);
            res.Add(header ?? (header = UDB.Create(this, UDBT.DTextRBHeader, IconId.WMaterials, "searchableinventory.ui.header").AsHeader().WithRBFunction(S.Sig.HideOverlay.Send)));

            S.Sys.Codex.EnhanceOverlay("Resources", res);

            if (availableCache.Count == 0 && blocks.Count == 0)
            {
                res.Add(UDB.Create(this, UDBT.DText, "Icons/Color/Warning", T.AvailableMaterialsNone));
            }
            if (sortModeBlock == null)
            {
                sortModeBlock = UDB.Create(this, UDBT.DTextBtn, "Icons/White/Priority", T.Sort).WithText2(T.Toggle).WithClickFunction(ToggleSort).WithText2(T.Toggle).WithClickFunction(ToggleSort);
            }
            // sortModeBlock = UDB.Create(this, UDBT.DTextBtn, "Icons/White/Priority", T.Sort).WithText2(T.Toggle).WithClickFunction(ToggleSort);
            sortModeBlock.UpdateText(sortText);

            res.Add(sortModeBlock);

            // var materials = S.Sys.Inventory.KnownMaterials;

            IOrderedEnumerable<KeyValuePair<MatType, int>> orderedEnumerable = null;

            switch (sortMode)
            {
                case SortMode.CountsDesc:
                    orderedEnumerable = availableCache.OrderByDescending((KeyValuePair<MatType, int> m) => m.Value);
                    break;
                case SortMode.CountsAsc:
                    orderedEnumerable = availableCache.OrderBy((KeyValuePair<MatType, int> m) => m.Value);
                    break;
                case SortMode.Alphabetic:
                    orderedEnumerable = availableCache.OrderBy((KeyValuePair<MatType, int> m) => m.Key.NameT);
                    break;
                case SortMode.Category:
                    orderedEnumerable = availableCache.OrderBy((KeyValuePair<MatType, int> m) =>
                    {
                        // D.Log($"{m.Key.DefId} is checked for grindability");
                        Def def = The.Defs.TryGet(m.Key.DefId);
                        // D.Warn($"{def}");
                        // D.Warn($"{def.Id} is {def.HasComponent(T.Processable)} processable");
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
                                            D.Warn(def.Id);
                                            // D.Warn(component.ToString());
                                            // D.Warn(m.Key.Group);
                                            // D.Warn(p.ToString());
                                            D.Warn($"{p.String}");
                                            return p.String;

                                            // D.Warn(p.StringSet[0]);
                                            // D.Warn(p.StringSet[1]);

                                        }
                                    }
                                }

                                // if (property is null)
                                // {
                                //     D.Warn("No property");
                                //     continue;
                                // }

                                // foreach (var p in property)
                                // {

                                //     D.Warn($"{p}");
                                // }



                            }
                            // D.Warn($"{m.Key.DefId} is grindable");
                        }

                        // Alternate sorting: sort by material state i.e. solid, liquid, etc
                        return m.Key.Group;
                    });
                    break;

            }

            String category = "None";
            foreach (KeyValuePair<MatType, int> mat in orderedEnumerable)
            {
                // DEBUG
                // D.Warn($"{mat.Key.Id} {mat.GetType()} property {mat.Key.Property} group {mat.Key.Group} hgroup {mat.Key.HGroup}");
                D.Warn(mat.Key.ProcessableDefId);

                var def = The.Defs.TryGet(mat.Key.DefId);

                if (def.HasComponent(T.Processable))
                {
                    foreach (ComponentConfig component in def.Components)
                    {
                        if (component.ToString() == "CompCfg[Processable]")
                        {
                            var properties = component.Properties;
                            if (properties is null)
                            {
                                continue;
                            }
                            foreach (var p in properties)
                            {
                                if (p.Key == "Type")
                                {
                                    category = p.String;
                                }
                            }
                        }
                    }

                }
                else
                {
                    category = mat.Key.Group;
                }
                res.Add(UDB.Create(this, UDBT.DLabel, "Icons/Color/Warning", $"{category}"));
                UDB uDB = blocks.Get(mat.Key, null);
                if (uDB == null)
                {
                    uDB = UDB.Create(this, UDBT.ITextBtn, mat.Key.IconId, mat.Key.NameT).WithIconTint(mat.Key.IconTint).WithTooltipFunction((UDB b) => mat.Key.Def.ExtendedTooltip(S))
                        .WithIconClickFunction(The.Defs.Get(mat.Key.DefId).ShowManualEntry);
                    blocks[mat.Key] = uDB;
                }
                uDB.WithText(Units.Num(mat.Value));
                uDB.WithText2(T.Find);
                uDB.WithIconHoverFunction(delegate (bool s)
                {
                    if (s)
                    {
                        // chartMat = mat.Key;
                        // chartUDB.UpdateTitle(mat.Key.NameT);
                    }
                });
                uDB.WithClickFunction(delegate
                {
                    if (mat.Value == 0)
                    {
                        UISounds.PlayActionDenied();
                    }
                    else
                    {
                        IMatStorage matStorage = S.Sys.Inventory.FindNextMat(mat.Key);
                        if (matStorage != null)
                        {
                            EntityUtils.CameraFocusOn(matStorage.Entity);
                            S.Sig.SelectEntity.Send(matStorage.Entity);
                        }
                    }
                });
                res.Add(uDB);
            }
            // the chart is private
            // maybe I need to make my own
            // CreateChart(res);
            // S.Sys.Inventory.
            // foreach (var mat in materials)
            // {
            //     // obsolete but I'm going to stick with what I see in the decompiler
            //     UDB uDB = blocks.Get(mat, null);
            //     D.Warn(mat.Id);
            // }

            // D.Warn("{}{}", "Get sort mode block", sortModeBlock.Id);
            // // if (sortModeBlock == null)
            // // {
            // //     sortModeBlock = UDB.Create(this, UDBT.DTextBtn, "Icons/White/Priority", T.Sort).WithText2(T.Toggle).WithClickFunction(ToggleSort);
            // // }
            // res.Add(UDB.Create(this, UDBT.IText, IconId.CInfo, "some.info".T())
            //     .WithIconClickFunction(DoSomethig)
            //     // Never do someNumber.ToString(), always use Units.XNum or Units.Num
            //     // that way there will be no garbage
            //     .WithText(Units.XNum(sys.SomeVariable)));

            // // Just like above, but with an extra button
            // res.Add(UDB.Create(this, UDBT.ITextBtn, IconId.CInfo, "other.info".T())
            //     .WithTooltip("tooltips.are.great".T())
            //     // This would be the text on the right side, near  the button,
            //     // but it's optional
            //     //.WithText(null)
            //     // Button text goes here
            //     .WithText2(T.Execute)
            //     .WithClickFunction(OnButtonClick));

            // copying InventorySys

            // S.Sys.Inventory


            // if (sortModeBlock == null)
            // {
            //     sortModeBlock = UDB.Create(this, UDBT.DTextBtn, "Icons/White/Priority", T.Sort).WithText2(T.Toggle).WithClickFunction(ToggleSort);
            // }
            // sortModeBlock.UpdateText(sortText);
            // res.Add(sortModeBlock);
            // LoadRemainingMaterials(availableCache);
            // if (availableCache.Count == 0 && blocks.Count == 0)
            // {
            //     res.Add(UDB.Create(this, UDBT.DText, "Icons/Color/Warning", T.AvailableMaterialsNone));
            // }
            // foreach (KeyValuePair<MatType, UDB> block in blocks)
            // {
            //     if (!availableCache.ContainsKey(block.Key))
            //     {
            //         block.Value.WithText("0");
            //     }
            // }
            // IOrderedEnumerable<KeyValuePair<MatType, int>> orderedEnumerable = null;
            // switch (sortMode)
            // {
            //     case SortMode.CountsDesc:
            //         orderedEnumerable = availableCache.OrderByDescending((KeyValuePair<MatType, int> m) => m.Value);
            //         break;
            //     case SortMode.CountsAsc:
            //         orderedEnumerable = availableCache.OrderBy((KeyValuePair<MatType, int> m) => m.Value);
            //         break;
            //     case SortMode.Alphabetic:
            //         orderedEnumerable = availableCache.OrderBy((KeyValuePair<MatType, int> m) => m.Key.NameT);
            //         break;
            // }
            // foreach (KeyValuePair<MatType, int> mat in orderedEnumerable)
            // {
            //     UDB uDB = blocks.Get(mat.Key, null);
            //     if (uDB == null)
            //     {
            //         uDB = UDB.Create(this, UDBT.ITextBtn, mat.Key.IconId, mat.Key.NameT).WithIconTint(mat.Key.IconTint).WithTooltipFunction((UDB b) => mat.Key.Def.ExtendedTooltip(S))
            //             .WithIconClickFunction(The.Defs.Get(mat.Key.DefId).ShowManualEntry);
            //         blocks[mat.Key] = uDB;
            //     }
            //     uDB.WithText(Units.Num(mat.Value));
            //     uDB.WithText2(T.Find);
            //     uDB.WithIconHoverFunction(delegate (bool s)
            //     {
            //         if (s)
            //         {
            //             chartMat = mat.Key;
            //             chartUDB.UpdateTitle(mat.Key.NameT);
            //         }
            //     });
            //     uDB.WithClickFunction(delegate
            //     {
            //         if (mat.Value == 0)
            //         {
            //             UISounds.PlayActionDenied();
            //         }
            //         else
            //         {
            //             IMatStorage matStorage = FindNextMat(mat.Key);
            //             if (matStorage != null)
            //             {
            //                 EntityUtils.CameraFocusOn(matStorage.Entity);
            //                 S.Sig.SelectEntity.Send(matStorage.Entity);
            //             }
            //         }
            //     });
            //     res.Add(uDB);
            // }
            // CreateChart(res);
        }
    }
}