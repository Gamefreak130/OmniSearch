namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class WishPickerDialogExtender : ModalExtender<WishPickerDialog, IInitialMajorWish>
    {
        protected override IEnumerable<IInitialMajorWish> Materials => Modal.mAvailableWishes;

        public WishPickerDialogExtender(WishPickerDialog modal) : base(modal)
        {
            Modal.mAvailableWishesItemGrid.VisibleRows -= 1;
            int verticalOffset = 25;
            Modal.mAvailableWishesBGWindow.Area = new(Modal.mAvailableWishesBGWindow.Area.TopLeft + new Vector2(0, verticalOffset), Modal.mAvailableWishesBGWindow.Area.BottomRight);
        }

        protected override void ClearItems() => Modal.mAvailableWishesItemGrid.Clear();

        protected override void ProcessResultsTask(IEnumerable<IInitialMajorWish> results)
        {
            List<IInitialMajorWish> allWishes = Modal.mAvailableWishes;
            Modal.mAvailableWishes = results.ToList();
            Modal.UpdateAvailableWishesGrid(0);
            Modal.mAvailableWishes = allWishes;
        }

        protected override Document<IInitialMajorWish> SelectDocument(IInitialMajorWish material)
            => new($"{material.GetMajorWishName(Modal.mCurrentSimDesc)}", 
                $"{material.GetMajorWishDescription(Modal.mCurrentSimDesc)}\n{material.GetMajorWishBullet1(Modal.mCurrentSimDesc)}\n{material.GetMajorWishBullet2(Modal.mCurrentSimDesc)}", 
                material);

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(5, Modal.mbInCAS ? 10 : 75, 280);

        protected override void SetSearchModel() => SetSearchModel(new TFIDF<IInitialMajorWish>(Corpus));
    }
}
