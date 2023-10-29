﻿using Sims3.SimIFace.CAS;
using Sims3.SimIFace.CustomContent;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class PlayFlowExtender : DocumentSearchExtender<IExportBinContents>
    {
        protected override IEnumerable<IExportBinContents> Materials => Responder.Instance.BinModel.ExportBinContents
                                                                                                   .Where(ItemFilter)
                                                                                                   .Order(PlayFlowBinPanel.Singleton.mComparer);

        public PlayFlowExtender() : base(PlayFlowPuck.gSingleton.GetChildByIndex(0), "EditTown", false)
        {
            SetSearchBarLocation();
            SearchBar.MoveToBack();
            SetSearchModel();
            Responder.Instance.BinModel.OnUpdateExportBin += OnExportBinChanged;
            PlayFlowBinPanel.Singleton.VisibilityChange += (_, eventArgs) => SetSearchBarVisibility(eventArgs.Visible);
            PlayFlowBinPanel.Singleton.mCustomContent.Click += OnItemReshuffle;
            PlayFlowBinPanel.Singleton.mSortDifficulty.Click += OnItemReshuffle;
            PlayFlowBinPanel.Singleton.mSortFunds.Click += OnItemReshuffle;
            PlayFlowBinPanel.Singleton.mSortName.Click += OnItemReshuffle;
            PlayFlowBinPanel.Singleton.mSortTime.Click += OnItemReshuffle;
        }

        public override void Dispose()
        {
            Responder.Instance.BinModel.OnUpdateExportBin -= OnExportBinChanged;
            base.Dispose();
        }

        protected override void ClearItems()
        {
            PlayFlowBinPanel.Singleton.mBinGridControl.AbortPopulating();
            PlayFlowBinPanel.Singleton.mBinGridControl.Clear();
        }

        protected override void ProcessResultsTask(IEnumerable<IExportBinContents> results)
            => PlayFlowBinPanel.Singleton.mBinGridControl.BeginPopulating(AddGridItem, results, 1, default, null);

        protected override Document<IExportBinContents> SelectDocument(IExportBinContents material)
            => new($"{material.HouseholdName}\t{material.LotName}", $"{material.HouseholdBio}\t{material.LotDescription}", material);

        protected override void SetSearchBarVisibility(bool visible)
        {
            SearchBar.Visible = visible;
            if (!visible)
            {
                SearchBar.Clear();
            }
        }

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(640, -37, 250);

        protected override void SetSearchModel() => SetSearchModel(new ExportBinSearchModel<IExportBinContents>(Corpus, true));

        private void OnExportBinChanged(List<UIBinInfo> _) => SetSearchModel();

        private void OnItemReshuffle(WindowBase _, UIButtonClickEventArgs __) => SetSearchModel();

        private bool ItemFilter(IExportBinContents item) 
            => !UIUtils.IsContentTypeDisabled((ResourceKeyContentCategory)item.DownloadType) && ((int)item.Type & 0b11) != 0
                && (!PlayFlowBinPanel.Singleton.mCustomContentOnly || UIUtils.IsCustomFiltered((ResourceKeyContentCategory)item.DownloadType))
                && (GameUtils.IsInstalled(ProductVersion.EP5) || item.HouseholdSims.TrueForAll(sim => sim.Species is CASAgeGenderFlags.None or CASAgeGenderFlags.Human));

        private bool AddGridItem(ItemGrid _, object item, ResourceKey __, object ___)
        {
            if (item is null)
            {
                return false;
            }
            PlayFlowBinPanel.Singleton.AddGridItem(item as IExportBinContents);
            return true;
        }
    }
}
