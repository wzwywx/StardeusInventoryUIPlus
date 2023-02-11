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
using Game.Rendering;
using InventoryUIPlus;
using UnityEngine;

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

        private void DoSomething()
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

        private readonly List<IMatStorage> materialStorages = new List<IMatStorage>();
        private readonly Dictionary<MatType, int> availableCache = new Dictionary<MatType, int>();
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

        // need to change this to return a category
        // TODO: I should cache the categories
        private void Categorize(MatType mat, out string compType)
        {
            Def def = The.Defs.TryGet(mat.DefId);

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
                                return;
                            }
                        }
                    }
                }
            }

            compType = mat.Group;

            // prioritize categorizing with mat.Group before everything else
            if (mat.Group is not null)
            {
                return;
            }

            if (Craftable.IsCraftable(def.Id))
            {
                // TODO: make this a local string later
                compType = "Craftable";
                return;
            }

            D.Warn(mat.DefId);
            // TODO: put this in the same namespace to avoid namespace collision
            // Or just initialize an instance of materials
            D.Warn(InventoryUIPlus.Data.Materials.Instance.GetCategory(mat.DefId));

            // if fail to categorize at the end, give the "Unknown" category to material
            if (mat.Group is null)
            {
                compType = InventoryUIPlus.Data.Materials.Instance.GetCategory(mat.DefId);
                return;
            }
        }
        private UDB sortModeBlock;

        public void GetUIDetails(List<UDB> res)
        {
            S.Sys.Inventory.LoadRemainingMaterials(availableCache);
            res.Add(header ?? (header = UDB.Create(this, UDBT.DTextRBHeader, IconId.WMaterials, "inventoryuiplus.ui.header").AsHeader().WithRBFunction(S.Sig.HideOverlay.Send)));

            S.Sys.Codex.EnhanceOverlay("Resources", res);

            if (availableCache.Count == 0 && blocks.Count == 0)
            {
                res.Add(UDB.Create(this, UDBT.DText, "Icons/Color/Warning", T.AvailableMaterialsNone));
            }
            if (sortModeBlock == null)
            {
                sortModeBlock = UDB.Create(this, UDBT.DTextBtn, "Icons/White/Priority", T.Sort).WithText2(T.Toggle).WithClickFunction(ToggleSort).WithText2(T.Toggle).WithClickFunction(ToggleSort);
            }
            sortModeBlock.UpdateText(sortText);

            res.Add(sortModeBlock);

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
                        Def def = The.Defs.TryGet(m.Key.DefId);
                        string category;
                        Categorize(m.Key, out category);
                        return category;
                    });
                    break;

            }

            string prevCategory = "";
            string currentCategory;
            foreach (KeyValuePair<MatType, int> mat in orderedEnumerable)
            {
                var def = The.Defs.TryGet(mat.Key.DefId);
                if (sortMode == SortMode.Category)
                {
                    Categorize(mat.Key, out currentCategory);

                    if (prevCategory == "" || prevCategory != currentCategory)
                        res.Add(UDB.Create(this, UDBT.DLabel, "Icons/Color/Warning", $"{currentCategory}"));
                    prevCategory = currentCategory;
                }


                UDB uDB = blocks.GetValueOrDefault(mat.Key, null);

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
                        chartMat = mat.Key;
                        chartUDB.UpdateTitle(mat.Key.NameT);
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
            CreateChart(res);
        }

        private UDB chartUDB;
        private MatType chartMat;

        private void CreateChart(List<UDB> res)
        {
            res.Add(chartUDB ?? (chartUDB = UDB.Create(this, UDBT.DChart, null).WithChartFunction(delegate (UDB b, RTChart chart)
            {
                if (chartMat != null)
                {
                    SortedDictionary<long, int> history = chartMat.History;
                    if (history == null || history.Count >= 2)
                    {
                        SortedDictionary<long, int> history2 = chartMat.History;
                        List<RTChartNode> list = new List<RTChartNode>();
                        KeyValuePair<long, int> keyValuePair = history2.First();
                        long key = keyValuePair.Key;
                        int num = keyValuePair.Value;
                        KeyValuePair<long, int> keyValuePair2 = history2.Last();
                        long num2 = S.Ticks - 30240;
                        if (num2 < 0)
                        {
                            num2 = 0L;
                        }
                        foreach (KeyValuePair<long, int> item in history2)
                        {
                            if (item.Key >= num2)
                            {
                                int value = item.Value;
                                list.Add(new RTChartNode
                                {
                                    Type = RTChartNode.NodeType.Line,
                                    From = new Vector2(key, num),
                                    To = new Vector2(item.Key, value),
                                    Color = Color.green,
                                    Thickness = 3f
                                });
                                key = item.Key;
                                num = value;
                            }
                        }
                        list.Add(new RTChartNode
                        {
                            Type = RTChartNode.NodeType.Line,
                            From = new Vector2(keyValuePair.Key, 0f),
                            To = new Vector2(keyValuePair2.Key, 0f),
                            Color = Color.red,
                            SkipTooltip = true,
                            Thickness = 3f
                        });
                        chart.TooltipFunc = delegate (Vector2 xy, Vector2 xyVal, float minX, float minY, float maxX, float maxY, List<RTChartNode> nodes)
                        {
                            if (nodes.Count != 1)
                            {
                                return null;
                            }
                            string arg = Units.TicksAgo(Mathf.FloorToInt(maxX - xyVal.x));
                            float f = nodes[0].ValueAt(xy.x);
                            f = Mathf.RoundToInt(f);
                            return $"{f}<br>{arg}";
                        };
                        chart.Render(list, hasTitle: true);
                        return;
                    }
                }
                chart.Render(null, hasTitle: false);
            }).Attached()));
        }
    }


}