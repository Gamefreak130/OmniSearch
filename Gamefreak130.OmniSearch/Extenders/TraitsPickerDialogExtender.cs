using static Sims3.UI.TraitsPickerDialog;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class TraitsPickerDialogExtender : ModalExtender<TraitsPickerDialog, ITraitEntryInfo>
    {
        protected override IEnumerable<ITraitEntryInfo> Materials => Modal.mAvailableTraits.Where(Modal.TraitMatchesCategory);

        public TraitsPickerDialogExtender(TraitsPickerDialog modal) : base(modal)
        {
            int buttonOffset = -50;
            Modal.mAddTraitButton.Area = new(Modal.mAddTraitButton.Area.TopLeft + new Vector2(buttonOffset, 0), Modal.mAddTraitButton.Area.BottomRight + new Vector2(buttonOffset, 0));
            Modal.mRemoveTraitButton.Area = new(Modal.mRemoveTraitButton.Area.TopLeft + new Vector2(buttonOffset, 0), Modal.mRemoveTraitButton.Area.BottomRight + new Vector2(buttonOffset, 0));

            ControlIDs[] categoryButtonIds =
            {
                ControlIDs.ButtonSortAll,
                ControlIDs.ButtonSortIntellect,
                ControlIDs.ButtonSortSocial,
                ControlIDs.ButtonSortPhysical,
                ControlIDs.ButtonSortLifestyle
            };

            foreach (ControlIDs id in categoryButtonIds)
            {
                Button button = ParentWindow.GetChildByID((uint)id, true) as Button;
                button.Click += (_,_) => ResetSearchModel();
            }
        }

        protected override void ClearItems() => Modal.mAvailableTraitsItemGrid.Clear();

        protected override void ProcessResultsTask(IEnumerable<ITraitEntryInfo> results)
        {
            List<ITraitEntryInfo> allTraits = Modal.mAvailableTraits;
            Modal.mAvailableTraits = results.ToList();
            Modal.UpdateAvailableTraitsGrid();
            Modal.mAvailableTraits = allTraits;
        }

        protected override Document<ITraitEntryInfo> SelectDocument(ITraitEntryInfo material)
        {
            ILocalizationModel localization = Responder.Instance.LocalizationModel;
            string title = localization.LocalizeString(Modal.mIsFemale, material.TraitNameInfo),
                   description = localization.LocalizeString(Modal.mIsFemale, material.TraitDescriptionInfoForCAS);

            if (GameUtils.IsInstalled(ProductVersion.EP9) && !string.IsNullOrEmpty(material.TraitAcademicEffectsInfoForTooltip))
            {
                description += $"\n{localization.LocalizeString(Modal.mIsFemale, "Gameplay/Excel/traits/TraitList:" + material.TraitAcademicEffectsInfoForTooltip)}";
            }

            return new(title, description, material);
        }

        protected override void SetSearchBarLocation() 
            => SearchBar.SetLocation(130, (Modal.mbInCAS ? 85 : 150) + (26 * Modal.mCurrentTraitsItemGrid.VisibleRows), 150);

        protected override ISearchModel<ITraitEntryInfo> GetSearchModel() => new TFIDF<ITraitEntryInfo>(Corpus);
    }
}
