using Sims3.Metadata;
using Sims3.SimIFace.CustomContent;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class CompositorExtender : DocumentSearchExtender<PatternInfo>
    {
        CASCompositorController Controller => mController ??= UIManager.GetParentWindow(ParentWindow) as CASCompositorController;

        protected override IEnumerable<PatternInfo> Materials
        {
            get
            {
                // Installed pattern
                foreach (Complate complate in Pattern.GetPatternCategories()
                                                     .Where(x => Controller.PatternFitsFilter(x, false))
                                                     .SelectMany(Pattern.GetPatternsForCategory)
                                                     .Where(x => !Controller.mFilterButton.Selected || UIUtils.IsCustomFiltered(UIUtils.GetCustomContentType(x)))
                                                     .Select(Pattern.GetComplate))
                {
                    yield return new(complate.Key, complate, null);
                }

                // Custom pattern preset
                foreach (PatternInfo info in ColorInfo.GetColorInfos(Sims3.ResourceTypes.kCASColorInfoResourceID)
                                                            .Where(x => !Controller.mFilterButton.Selected || UIUtils.IsCustomFiltered(UIUtils.GetCustomContentType(x)))
                                                            .Select(x => new { Key = x, ColorInfo = ColorInfo.FromResourceKey(x) })
                                                            .Where(x => x.ColorInfo?.Fabrics.Length <= 1)
                                                            .Select(x => new PatternInfo(x.Key, CompositorUtil.ColorInfoToComplate(x.ColorInfo), x.ColorInfo))
                                                            .Where(x => x.Complate.Key != ResourceKey.kInvalidResourceKey && Controller.PatternFitsFilter(x.Complate.Category, x.ColorInfo.Favorite)))
                {
                    yield return info;
                }
            }
        }

        public CompositorExtender() : base(CASCompositorController.Instance.mMaterialsBinWindow, "Design", false)
        {
            Controller.mDebugTooltips = true;
            Controller.mFilterButton.Click += OnMaterialChange;
            Controller.mMaterialComboBox.SelectionChange += OnMaterialChange;
            Controller.mMaterialsSaveButton.Click += OnMaterialChange;
            Controller.mMaterialsFavoriteButton.Click += OnMaterialChange;
            Controller.mMaterialsTrashButton.Click += OnMaterialChange;
            Controller.mMaterialsWindowGrid.InternalGrid.DragDrop += OnMaterialChange;
            Controller.EnterFullEditMode += OnReenter;
        }

        private CASCompositorController mController;

        private void OnMaterialChange(WindowBase _, UIEventArgs __) 
        {
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearItems();
            }

            TaskEx.Run(() => {
                // For some reason MouseUp won't register after a standard yield, leading to unintentional click-dragging when selecting a part
                // Delay(0) puts this thread on indefinite sleep before immediately waking, allowing the events to register properly
                TaskEx.Delay(0).Await();
                SetSearchModel();
            });
        }

        private void OnReenter()
        {
            SearchBar.Clear();
            ClearItems();
            ProcessExistingQuery();
        }

        public static void Inject()
        {
            CASCompositorController.Instance.EnterFullEditMode -= Inject;
            TaskEx.Run(() => new CompositorExtender());
        }

        public override void Dispose()
        {
            Controller.mFilterButton.Click -= OnMaterialChange;
            Controller.mMaterialComboBox.SelectionChange -= OnMaterialChange;
            Controller.mMaterialsSaveButton.Click -= OnMaterialChange;
            Controller.mMaterialsFavoriteButton.Click -= OnMaterialChange;
            Controller.mMaterialsTrashButton.Click -= OnMaterialChange;
            Controller.mMaterialsWindowGrid.InternalGrid.DragDrop -= OnMaterialChange;
            Controller.EnterFullEditMode -= OnReenter;
            base.Dispose();
        }

        protected override void ClearItems()
        {
            Controller.KillGridPopulateTask();
            Controller.mMaterialsWindowGrid.Clear();
        }

        protected override void ProcessResultsTask(IEnumerable<PatternInfo> results)
        {
            ResourceKey layoutKey = ResourceKey.CreateUILayoutKey("MaterialsGridItem", 0U);
            foreach (PatternInfo info in results)
            {
                // Installed pattern
                if (info.ColorInfo is null)
                {
                    uint patternThumbnail = CompositorUtil.GetPatternThumbnail(info.Complate.Category, info.Key.InstanceId, null);
                    if (patternThumbnail == 0U)
                    {
                        patternThumbnail = CompositorUtil.GetPatternThumbnail(info.Complate.Category, info.Key.InstanceId, Pattern.GetCompositor(info.Key));
                    }
                    Controller.AddMaterialBinGridItem(layoutKey, info.Complate, new UIImage(patternThumbnail), Pattern.GetComplateName(info.Key), UIUtils.GetCustomContentType(info.Key));
                }
                // Custom pattern preset
                else
                {
                    byte[] data = null;
                    if (info.Complate.CreateTextureCompositor(null, null, 0U) is TextureCompositor textureCompositor)
                    {
                        data = textureCompositor.ExportData(null, null, null);
                        ObjectDesigner.SetNewPattern(info.Complate.Name, data);
                        ObjectDesigner.UpdateTextures();
                        while (ObjectDesigner.IsProcessing)
                        {
                            TaskEx.Yield();
                        }
                    }
                    uint patternThumbnail = CompositorUtil.GetPatternThumbnail(info.Complate.Category, info.Key.InstanceId, data);
                    ResourceKeyContentCategory contentCategory = Controller.mCurrentFilter == "Favorite" 
                        ? (ResourceKeyContentCategory)info.ColorInfo.FavoriteContentType 
                        : UIUtils.GetCustomContentType(info.Key);

                    Controller.AddMaterialBinGridItem(layoutKey, info.Key, new UIImage(patternThumbnail), info.Complate.Name, contentCategory);
                }
                TaskEx.Yield();
            }
            if (Controller.mMaterialSkewerSelectedPattern >= 0)
            {
                Controller.SelectCurrentMaterialInBin(Controller.mMaterialsSaveButton.Enabled);
            }
            if (Controller.mMaterialsWindowGrid.Count == 0)
            {
                Controller.mMaterialsNoContentText.Visible = true;
            }
            Pattern.FlushPatternComplates();
        }

        protected override Document<PatternInfo> SelectDocument(PatternInfo material) 
            => new(material.Complate.Name, "", material);

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(200, 242, 125);

        protected override void SetSearchModel() => SetSearchModel(new ExactMatch<PatternInfo>(Corpus));
    }
}
