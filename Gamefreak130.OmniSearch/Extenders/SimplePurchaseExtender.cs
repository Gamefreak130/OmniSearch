namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class SimplePurchaseExtender : ModalExtender<ObjectPicker.RowInfo>
    {
        protected override IEnumerable<ObjectPicker.RowInfo> Materials => Table.mItems[Table.mSortedTab].RowInfo;

        private ObjectPicker Table => mTable ??= ParentWindow.GetChildByID(SimplePurchaseDialog.ITEM_TABLE, true) as ObjectPicker;

        public SimplePurchaseExtender() => Table.mComboBox.SelectionChange += (_,_) => SetSearchModel();

        private ObjectPicker mTable;

        protected override void ClearItems() => Table.mItems[Table.mSortedTab].RowInfo = null;

        protected override void ProcessResultsTask(IEnumerable<ObjectPicker.RowInfo> results)
        {
            Table.mItems[Table.mSortedTab].RowInfo = results.ToList();
            if (Table.mItems[Table.mSortedTab].RowInfo.Count > 0)
            {
                Table.RepopulateTable();
            }
            else
            {
                Table.mTable.Clear();
            }
        }

        protected override Document<ObjectPicker.RowInfo> SelectDocument(ObjectPicker.RowInfo material) 
            => new((material.ColumnInfo[0] as ObjectPicker.ThumbAndTextColumn).BodyText, "", material);

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(50, 95, 270);

        protected override void SetSearchModel() => SetSearchModel(new ExactMatch<ObjectPicker.RowInfo>(Corpus));
    }
}
