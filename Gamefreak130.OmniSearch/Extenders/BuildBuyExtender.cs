using Sims3.Gameplay.Abstracts;
using Sims3.UI.Store;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public abstract class BuildBuyExtender : DocumentSearchExtender<object>
    {
        protected class BuildBuySearchModel : TFIDF<object>
        {
            private readonly BuildBuyExtender mExtender;

            private readonly bool mSearchCollections;

            public BuildBuySearchModel(IEnumerable<Document<object>> documents, BuildBuyExtender extender, bool searchCollections) : base(documents)
            {
                mExtender = extender;
                mSearchCollections = searchCollections;
            }

            protected override IEnumerable<object> SearchTask(string query)
            {
                IEnumerable<object> results = mSearchCollections ? SearchCollections(query) : null;
                return results ?? base.SearchTask(query);
            }

            protected IEnumerable<object> SearchCollections(string query)
            {
                ITokenizer tokenizer = Tokenizer.Create();
                foreach (IBBCollectionData collection in Responder.Instance.BuildModel.CollectionInfo.CollectionData)
                {
                    if (tokenizer.Tokenize(query).SequenceEqual(tokenizer.Tokenize(collection.CollectionName)))
                    {
                        List<Document<object>> collectionDocs = collection.Items.ConvertAll(mExtender.SelectDocument);
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
        }

        protected const ulong kWallDescriptionHash = 0xDD1EAD49D9F75762;

        protected const ulong kFloorDescriptionHash = 0x2DE87A7A181E89C4;

        private IEnumerable<Document<object>> mDocuments;

        protected BuildBuyExtender(WindowBase parentWindow) : base(parentWindow, "BuildBuy", false)
        {
        }

        protected override Document<object> SelectDocument(object product)
        {
            string name, description;

            switch (product)
            {
                case BuildBuyProduct bbp:
                    name = Localization.FillInTokens(LocGenderType.Male, null, bbp.CatalogName);
                    description = bbp.Description;
                    break;
                case BuildBuyPreset bbp:
                    name = Localization.FillInTokens(LocGenderType.Male, null, bbp.Product.CatalogName);
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
                    throw new ArgumentException($"{product.GetType().Name} is not a valid Build/Buy product", nameof(product));
            }

            return new(name, description, product);
        }

        protected override void SetSearchModel()
        {
            mDocuments = Corpus;
            bool searchCollections = BuyController.sController is not null
                                   ? BuyController.sController.mCurrCatalogType is not (BuyController.CatalogType.Collections or BuyController.CatalogType.Inventory)
                                   : BuildController.sController is not null && BuildController.sController.mCollectionWindow.Visible;

            SetSearchModel(new BuildBuySearchModel(mDocuments, this, searchCollections));
        }
    }
}
