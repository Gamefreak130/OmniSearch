namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class EditTownExtender : DocumentSearchExtender<object>
    {
        protected override IEnumerable<object> Materials
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
                                        .Where(obj => (obj as BuildBuyProduct).IsVisibleInWorldBuilder);
                }
                else
                {
                    return Responder.Instance.EditTownModel.GetExportBinInfoList()
                                                           .Where(EditTownLibraryPanel.Instance.ItemFilter)
                                                           .OrderBy(binInfo => binInfo, new EditTownLibraryPanel.GridComparer())
                                                           .Cast<object>();
                }
            }
        }

        public EditTownExtender() : base(EditTownPuck.Instance.GetChildByIndex(3), "EditTown")
        {
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

        protected override void ProcessResultsTask(IEnumerable<object> results)
        {
            ItemGrid itemGrid = null;
            ItemGridPopulateCallback populateCallback = null;
            ResourceKey layoutKey = default;

            if (EditTownLibraryPanel.Instance.Visible)
            {
                itemGrid = EditTownLibraryPanel.Instance.mGrid;
                populateCallback = AddGridItem;
            }
            else if (EditTownNeighborhoodPloppablesPanel.Instance.Visible && EditTownNeighborhoodPloppablesPanel.Instance.mTabSelection is not EditTownNeighborhoodPloppablesPanel.ControlID.LotsTab)
            {
                itemGrid = EditTownNeighborhoodPloppablesPanel.Instance.mItemGrid;
                populateCallback = EditTownNeighborhoodPloppablesPanel.Instance.PopulateObjectCallback;
                layoutKey = ResourceKey.CreateUILayoutKey("BuildCatalogItem", 0);
            }

            // TODO performance improvement
            // CONSIDER Filter by lot size?
            itemGrid.BeginPopulating(populateCallback, results, 5, layoutKey, null);
        }

        protected override Document<object> SelectDocument(object material)
        {
            string name, description;

            switch (material)
            {
                case UIBinInfo binInfo:
                    name = $"{binInfo.HouseholdName};{binInfo.LotName} {Responder.Instance.EditTownModel.CommercialSubTypeLocalizedName(binInfo.CommercialLotSubType)}";
                    description = $"{binInfo.HouseholdBio};{binInfo.LotDescription}";
                    break;
                case BuildBuyProduct bbp:
                    name = bbp.CatalogName;
                    description = "";
                    break;
                default:
                    throw new ArgumentException($"{material.GetType().Name} is not a valid Edit Town object", nameof(material));
            }

            return new(name, description, material);
        }

        protected override void SetSearchBarVisibility() 
            => SetSearchBarVisibility(EditTownLibraryPanel.Instance.Visible || (EditTownNeighborhoodPloppablesPanel.Instance.Visible && EditTownNeighborhoodPloppablesPanel.Instance.mTabSelection is not EditTownNeighborhoodPloppablesPanel.ControlID.LotsTab));

        protected override void SetSearchBarLocation() 
            => SearchBar.SetLocation(EditTownLibraryPanel.Instance.Visible ? 350 : 405, -35, 250);

        protected override void SetSearchModel() 
            => SetSearchModel(new TFIDF<object>(Corpus));

        protected override void ClearItems()
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

        private static bool AddGridItem(ItemGrid _, object current, ResourceKey __, object ___)
        {
            if (current is null)
            {
                return false;
            }
            bool flag = false;
            EditTownLibraryPanel.Instance.AddGridItem(current as UIBinInfo, ref flag);
            return true;
        }
    }
}
