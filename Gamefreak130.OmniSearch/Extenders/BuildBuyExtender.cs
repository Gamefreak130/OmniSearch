using Sims3.Gameplay.Abstracts;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI.Hud;
using Sims3.UI.Store;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public abstract class BuildBuyExtender : TitleDescriptionSearchExtender<object>
    {
        protected const ulong kWallDescriptionHash = 0xDD1EAD49D9F75762;

        protected const ulong kFloorDescriptionHash = 0x2DE87A7A181E89C4;

        private IEnumerable<Document<object>> mDocuments;

        protected BuildBuyExtender(WindowBase parentWindow) : base(parentWindow, "BuildBuy")
            => EventTracker.AddListener(EventTypeId.kExitInWorldSubState, delegate {
                Dispose();
                return ListenerAction.Remove;
            });

        public override void Dispose()
        {
            // TODO Cleanup if not needed
            base.Dispose();
        }

        protected override Document<object> SelectDocument(object product)
        {
            string name, description;

            switch (product)
            {
                case BuildBuyProduct bbp:
                    name = bbp.CatalogName;
                    description = bbp.Description;
                    break;
                case BuildBuyPreset bbp:
                    name = bbp.Product.CatalogName;
                    description = bbp.Product.Description;
                    // Filter out the generic descriptions of wall or floor patterns
                    if (description == Localization.LocalizeString(kWallDescriptionHash) || description == Localization.LocalizeString(kFloorDescriptionHash))
                    {
                        description = "";
                    }
                    break;
                case IFeaturedStoreItem fsi:
                    name = fsi.Name;
                    description = fsi.Description;
                    break;
                case IBBCollectionData bbcd:
                    name = bbcd.CollectionName;
                    description = bbcd.CollectionDesc;
                    break;
                case IInventoryItemStack iis:
                    name = GameObject.GetObject(iis.TopObject).ToTooltipString();
                    description = "";
                    break;
                default:
                    throw new ArgumentException($"{product.GetType()} is not a valid Build/Buy product", nameof(product));
            }

            return new Document<object>(name, description, product);
        }

        protected void SetSearchModel()
        {
            try
            {
                mDocuments = Corpus;
                SearchModel = new TFIDF<object>(mDocuments)
                {
                    Yielding = true
                };

                ProcessExistingQuery();
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        protected void ProcessExistingQuery()
        {
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearCatalogGrid();
                SearchBar.TriggerSearch();
            }
            else
            {
                SearchBar.Clear();
            }
        }

        protected IEnumerable<object> SearchCollections()
        {
            ITokenizer tokenizer = Tokenizer.Create();
            foreach (IBBCollectionData collection in Responder.Instance.BuildModel.CollectionInfo.CollectionData)
            {
                if (tokenizer.Tokenize(SearchBar.Query).SequenceEqual(tokenizer.Tokenize(collection.CollectionName)))
                {
                    List<Document<object>> collectionDocs = collection.Items.ConvertAll(SelectDocument);
                    IEnumerable<object> collectionProducts = from document in mDocuments
                                                             where collectionDocs.Contains(document)
                                                             select document.Tag;

                    if (collectionProducts.FirstOrDefault() is not null)
                    {
                        return collectionProducts;
                    }
                }
            }
            return null;
        }

        protected abstract void ClearCatalogGrid();
    }
}
