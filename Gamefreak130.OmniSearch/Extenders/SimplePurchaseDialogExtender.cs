namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class SimplePurchaseDialogExtender : ModalExtender<SimplePurchaseDialog, ObjectPicker.RowInfo>
    {
        protected override IEnumerable<ObjectPicker.RowInfo> Materials => Modal.mTable.mItems[Modal.mTable.mSortedTab].RowInfo;

        public SimplePurchaseDialogExtender(SimplePurchaseDialog modal) : base(modal)
            => Modal.mTable.mComboBox.SelectionChange += (_,_) => ResetSearchModel();

        protected override void ClearItems() => Modal.mTable.mItems[Modal.mTable.mSortedTab].RowInfo = null;

        protected override void ProcessResultsTask(IEnumerable<ObjectPicker.RowInfo> results)
        {
            Modal.mTable.mItems[Modal.mTable.mSortedTab].RowInfo = results.ToList();
            if (Modal.mTable.mItems[Modal.mTable.mSortedTab].RowInfo.Count > 0)
            {
                Modal.mTable.RepopulateTable();
            }
            else
            {
                Modal.mTable.mTable.Clear();
            }
        }

        protected override Document<ObjectPicker.RowInfo> SelectDocument(ObjectPicker.RowInfo material) 
            => new((material.ColumnInfo[0] as ObjectPicker.ThumbAndTextColumn).BodyText, "", material);

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(50, 95, 270);

        protected override ISearchModel<ObjectPicker.RowInfo> GetSearchModel() => new ExactMatch<ObjectPicker.RowInfo>(Corpus);
    }
}
