using Sims3.UI.CAS;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Add OccultType to document description?
    public class RelationshipPanelExtender : DocumentSearchExtender<(IMiniSimDescription simDescription, IRelationship relationship)>
    {
        protected override IEnumerable<(IMiniSimDescription, IRelationship)> Materials 
            => RelationshipsPanel.Instance.mHudModel.CurrentRelationships.Select(x => (x.Key, x.Value));

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
            SetSearchModel();
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

        protected override void ProcessResultsTask(IEnumerable<(IMiniSimDescription simDescription, IRelationship relationship)> results)
        {
            mSearching = true;
            RelationshipsPanel.Instance.Repopulate(results.ToDictionary(x => x.simDescription, x => x.relationship));
            mSearching = false;
        }

        protected override Document<(IMiniSimDescription simDescription, IRelationship relationship)> SelectDocument((IMiniSimDescription simDescription, IRelationship relationship) material) 
            => new($"{material.relationship.SimName}", $"{material.relationship.CurrentLTRNameLocalized};{material.relationship.FamilialString}", material);

        protected override void SetSearchBarLocation()
        {
            SearchBar.SetLocation(61, -60, 213);
            SearchBar.MoveToBack();
        }

        protected override void SetSearchModel() => SetSearchModel(new ExactMatch<(IMiniSimDescription, IRelationship)>(Corpus));
    }
}
