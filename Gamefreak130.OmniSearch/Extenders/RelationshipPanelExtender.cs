using Sims3.UI.CAS;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class RelationshipPanelExtender : DocumentSearchExtender<IMiniSimDescription>
    {
        protected override IEnumerable<IMiniSimDescription> Materials => throw new NotImplementedException();

        public RelationshipPanelExtender(WindowBase parentWindow, bool visible = true, bool showFullPanel = true) : base(parentWindow, "Relationships", visible, showFullPanel)
        {
        }

        protected override void ClearItems() => throw new NotImplementedException();
        protected override void ProcessResultsTask(IEnumerable<IMiniSimDescription> results) => throw new NotImplementedException();
        protected override Document<IMiniSimDescription> SelectDocument(IMiniSimDescription material) => throw new NotImplementedException();
        protected override void SetSearchBarLocation() => throw new NotImplementedException();
        protected override void SetSearchModel() => throw new NotImplementedException();
    }
}
