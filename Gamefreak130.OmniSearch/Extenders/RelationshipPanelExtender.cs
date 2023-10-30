using Sims3.Gameplay.Objects.HobbiesSkills;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // TODO Add OccultType to document description?
    public class RelationshipPanelExtender : DocumentSearchExtender<Tuple>
    {
        protected override IEnumerable<Tuple> Materials 
            => RelationshipsPanel.Instance.mHudModel.CurrentRelationships.Select(x => new Tuple(x.Key, x.Value));

        public RelationshipPanelExtender() : base(RelationshipsPanel.Instance, "Relationships")
        {
            try
            {
                RelationshipsPanel.Instance.VisibilityChange += OnVisibilityChange;
                RelationshipsPanel.Instance.mRelItemGrid.ItemRowsChanged += OnItemsChanged;
            }
            catch (Exception ex)
            {
                ExceptionLogger.sInstance.Log(ex);
            }
        }

        private bool mDirty;

        private bool mSearching;

        public static void InjectIfVisible(WindowBase _, UIVisibilityChangeEventArgs args)
        {
            if (args.Visible)
            {
                new RelationshipPanelExtender();
            }
        }

        private void OnVisibilityChange(WindowBase _, UIVisibilityChangeEventArgs __) => Dispose();

        private void OnDirtyTick(WindowBase _, UIEventArgs __)
        {
            ResetSearchModel();
            mDirty = false;
            RelationshipsPanel.Instance.Tick -= OnDirtyTick;
        }

        private void OnItemsChanged()
        {
            if (!mDirty && !mSearching)
            {
                mDirty = true;
                RelationshipsPanel.Instance.Tick += OnDirtyTick;
            }
        }

        public override void Dispose()
        {
            if (RelationshipsPanel.Instance is not null)
            {
                RelationshipsPanel.Instance.VisibilityChange -= OnVisibilityChange;
                RelationshipsPanel.Instance.mRelItemGrid.ItemRowsChanged -= OnItemsChanged;
                RelationshipsPanel.Instance.Tick -= OnDirtyTick;
            }
            base.Dispose();
        }

        protected override void ClearItems() => RelationshipsPanel.Instance.mRelItemGrid.Clear();

        protected override void ProcessResultsTask(IEnumerable<Tuple> results)
        {
            mSearching = true;
            RelationshipsPanel.Instance.Repopulate(results.ToDictionary(x => x.mParam1 as IMiniSimDescription, x => x.mParam2 as IRelationship));
            mSearching = false;
        }

        protected override Document<Tuple> SelectDocument(Tuple material)
        {
            IMiniSimDescription simDescription = material.mParam1 as IMiniSimDescription;
            IRelationship relationship = material.mParam2 as IRelationship;
            string _ = null;
            return new($"{relationship.SimName}", $"{relationship.CurrentLTRNameLocalized};{relationship.FamilialString};{Photograph.GameUtilsGetLocalizedWorldName(simDescription.HomeWorld, ref _)}", material);
        }

        protected override void SetSearchBarLocation()
        {
            SearchBar.SetLocation(61, -60, 213);
            SearchBar.MoveToBack();
        }

        protected override ISearchModel<Tuple> GetSearchModel() => new ExactMatch<Tuple>(Corpus);
    }
}
