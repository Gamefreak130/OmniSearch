using Sims3.SimIFace.BuildBuy;
using Sims3.UI.GameEntry;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class EditTownExtender : TitleDescriptionSearchExtender<object>
    {
        protected override IEnumerable<Document<object>> Corpus
        {
            get
            {
                if (EditTownPuck.Instance.mIsInPloppablesMode)
                {
                    uint buildCategoryFlag = EditTownNeighborhoodPloppablesPanel.Instance.mTabSelection switch
                    {
                        EditTownNeighborhoodPloppablesPanel.ControlID.TreesTab        => 0x800,
                        EditTownNeighborhoodPloppablesPanel.ControlID.RocksTab        => 0x2000,
                        EditTownNeighborhoodPloppablesPanel.ControlID.DecorationsTab  => 0x8000,
                        _                                                             => 0
                    };

                    return UserToolUtils.GetObjectProductListFiltered(uint.MaxValue, buildCategoryFlag, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 0UL, uint.MaxValue, 0U, 0U)
                                        .Where(obj => (obj as BuildBuyProduct).IsVisibleInWorldBuilder)
                                        .Select(SelectDocument);
                }
                else
                {
                    return Responder.Instance.EditTownModel.GetExportBinInfoList()
                                                           .Where(EditTownLibraryPanel.Instance.ItemFilter)
                                                           .OrderBy(binInfo => binInfo, new EditTownLibraryPanel.GridComparer())
                                                           .Select(SelectDocument);
                }
            }
        }

        public EditTownExtender() : base(EditTownPuck.Instance.GetChildByIndex(3), "EditTown")
        {
            SetSearchBarVisibility();
            SearchBar.MoveToBack();

            Responder.Instance.BinModel.OnUpdateExportBin += OnExportBinChanged;

            EditTownLibraryPanel.Instance.VisibilityChange += OnVisibilityChange;
            EditTownNeighborhoodPloppablesPanel.Instance.VisibilityChange += OnVisibilityChange;

            EditTownNeighborhoodPloppablesPanel.Instance.mTabContainer.TabSelect += OnTabSelect;
            for (uint controlId = (uint)EditTownLibraryPanel.ControlIDs.ShowHouseholds; controlId <= (uint)EditTownLibraryPanel.ControlIDs.UnhideAllPackages; controlId++)
            {
                (EditTownLibraryPanel.Instance.GetChildByID(controlId, true) as Button).Click += OnTabSelect;
            }
        }

        public override void Dispose()
        {
            Responder.Instance.BinModel.OnUpdateExportBin -= OnExportBinChanged;
            base.Dispose();
        }

        protected override void QueryEnteredTask()
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"));

#if DEBUG
                IEnumerable results = SearchModel.Search(SearchBar.Query)
                                                 .ToList();


                //DocumentLogger.sInstance.WriteLog();
#else
                IEnumerable results = SearchModel.Search(SearchBar.Query);
#endif
                ItemGrid itemGrid = null;
                ItemGridPopulateCallback populateCallback = null;
                ResourceKey layoutKey = default;

                if (EditTownLibraryPanel.Instance.Visible)
                {
                    itemGrid = EditTownLibraryPanel.Instance.mGrid;
                    bool _ = false;
                    populateCallback = (_, current, _, _) => {
                        if (current is null)
                        {
                            return false;
                        }
                        EditTownLibraryPanel.Instance.AddGridItem(current as UIBinInfo, ref _);
                        return true;
                    };
                }
                else if (EditTownNeighborhoodPloppablesPanel.Instance.Visible && EditTownNeighborhoodPloppablesPanel.Instance.mTabSelection is not EditTownNeighborhoodPloppablesPanel.ControlID.LotsTab)
                {
                    itemGrid = EditTownNeighborhoodPloppablesPanel.Instance.mItemGrid;
                    populateCallback = EditTownNeighborhoodPloppablesPanel.Instance.PopulateObjectCallback;
                    layoutKey = ResourceKey.CreateUILayoutKey("BuildCatalogItem", 0);
                }

                ClearItemGrid();
                // TODO items per tick?
                // CONSIDER Filter by lot size?
                itemGrid.BeginPopulating(populateCallback, results, 10, layoutKey, null);
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
            finally
            {
                TaskEx.Run(ProgressDialog.Close);
            }
        }

        protected override Document<object> SelectDocument(object obj)
        {
            string name, description;

            switch (obj)
            {
                case UIBinInfo binInfo:
                    name = $"{binInfo.HouseholdName};{binInfo.LotName} {Responder.Instance.EditTownModel.CommercialSubTypeLocalizedName(binInfo.CommercialLotSubType)}";
                    description = $"{binInfo.HouseholdBio};{binInfo.LotDescription}";
                    break;
                case BuildBuyProduct bbp:
                    name = bbp.CatalogName;
                    description = null;
                    break;
                default:
                    throw new ArgumentException($"{obj.GetType()} is not a valid Edit Town object", nameof(obj));
            }

            return new Document<object>(name, description, obj);
        }

        private void SetSearchBarVisibility()
        {
            SearchBar.Visible = EditTownLibraryPanel.Instance.Visible 
                || (EditTownNeighborhoodPloppablesPanel.Instance.Visible && EditTownNeighborhoodPloppablesPanel.Instance.mTabSelection is not EditTownNeighborhoodPloppablesPanel.ControlID.LotsTab);

            if (SearchBar.Visible)
            {
                SearchBar.SetLocation(EditTownLibraryPanel.Instance.Visible ? 350 : 405, -35, 250);
                SetSearchModel();
            }
            else
            {
                SearchBar.Clear();
            }
        }

        protected void SetSearchModel()
        {
            try
            {
                SearchModel = new TFIDF<object>(Corpus)
                {
                    Yielding = true
                };

                ProcessExistingQuery();
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        protected void ProcessExistingQuery()
        {
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearItemGrid();
                SearchBar.TriggerSearch();
            }
            else
            {
                SearchBar.Clear();
            }
        }

        protected void ClearItemGrid()
        {
            if (EditTownNeighborhoodPloppablesPanel.Instance.Visible)
            {
                EditTownNeighborhoodPloppablesPanel.Instance.mItemGrid.AbortPopulating();
                EditTownNeighborhoodPloppablesPanel.Instance.mItemGrid.Clear();
            }
            else if (EditTownLibraryPanel.Instance.Visible)
            {
                EditTownLibraryPanel.Instance.mGrid.Clear();
            }
        }

        private void OnVisibilityChange(WindowBase _, UIVisibilityChangeEventArgs __)
        {
            SearchBar.Clear();
            // TODO Perf improvement
            if (EditTownPuck.Instance.IsInPloppablesMode)
            {
                EditTownNeighborhoodPloppablesPanel.Instance.PopulateGrid();
            }
            else
            {
                EditTownLibraryPanel.Instance.PopulateGrid();
            }
            SetSearchBarVisibility();
        }

        private void OnExportBinChanged(List<UIBinInfo> _) => SetSearchModel();

        private void OnTabSelect(object _, object __) => SetSearchBarVisibility();
    }
}
