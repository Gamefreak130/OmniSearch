using Gamefreak130.Common.Loggers;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI;
using System;
using System.Linq;
using System.Xml;

namespace Gamefreak130
{
    public class FurnitureScraper
    {
        [Tunable]
        private static readonly bool kCJackB;

        private static bool sDone;

        static FurnitureScraper() => World.OnWorldLoadFinishedEventHandler += OnWorldLoadFinished;

        private static void OnWorldLoadFinished(object sender, EventArgs e) 
            => EventTracker.AddListener(EventTypeId.kEnterInWorldSubState, delegate {
                   Simulator.AddObject(new OneShotFunctionTask(StartFurnitureScraper, StopWatch.TickStyles.Seconds, 1));
                   return ListenerAction.Keep;
               });

        private static void StartFurnitureScraper()
        {
            if (BuyController.sController is not null)
            {
                BuyController.sController.Tick += ScrapeFurniture;
                SimpleMessageDialog.Show("Furniture Scraper", "Ready to Scrape");
            }
            if (BuildController.sController is not null)
            {
                BuildController.sController.Tick += ScrapeFurniture;
                SimpleMessageDialog.Show("Furniture Scraper", "Ready to Scrape");
            }
        }

        private static void ScrapeFurniture(WindowBase sender, UIEventArgs __)
        {
            try
            {
                if (sender is BuyController buyController && buyController.mCurrCategoryFilter != buyController.BUY_CATEGORY_ALL && (buyController.mCurrSubCategoryFilter == buyController.BUY_SUBCATEGORY_ALL || buyController.mCurrCategoryFilter == 1 << buyController.GetCategoryFromName("Debug").mFlagBit))
                {
                    if (!(buyController.mCatalogGrid.Tag as Button).Enabled)
                    {
                        sDone = false;
                        return;
                    }

                    if (!sDone)
                    {
                        uint fileHandle = 0;
                        try
                        {
                            BuyController.Category category = BuyController.sCategoryList.Find(cat => 1 << cat.mFlagBit == buyController.mCurrCategoryFilter || (buyController.mCurrCategoryFilter == buyController.BUY_CATEGORY_ALL && cat.mName == "All"));
                            BuyController.SubCategory subCat = category.mSubCategoryList.Find(cat => buyController.mCurrSubCategoryFilter == 1u << cat.mFlagBit);
                            Simulator.CreateExportFile(ref fileHandle, $"{(category.mName == "Debug" ? subCat.mName : category.mName)}__");
                            if (fileHandle != 0)
                            {
                                CustomXmlWriter xmlWriter = new(fileHandle);
                                xmlWriter.WriteStartDocument();
                                xmlWriter.WriteStartElement("ProductList");
                                foreach (BuildBuyProduct product in from item
                                                                    in buyController.mCatalogGrid.Items
                                                                    select item.mTag as BuildBuyProduct)
                                {
                                    xmlWriter.WriteStartElement("Item");
                                    xmlWriter.WriteElementString("Title", XmlConvert.EncodeName(product.CatalogName));
                                    xmlWriter.WriteElementString("Description", XmlConvert.EncodeName(product.Description));
                                    xmlWriter.WriteEndElement();
                                }
                                xmlWriter.WriteEndDocument();
                                xmlWriter.FlushBufferToFile();
                                sDone = true;
                                SimpleMessageDialog.Show("FurnitureScraper", "Furniture Logged");
                            }
                        }
                        finally
                        {
                            if (fileHandle != 0)
                            {
                                Simulator.CloseScriptErrorFile(fileHandle);
                            }
                        }
                    }
                }
                if (sender is BuildController buildController)
                {
                    if (buildController.mCurrentCatalogGrid is null || !(buildController.mCurrentCatalogGrid.Tag as Button).Enabled)
                    {
                        sDone = false;
                        return;
                    }

                    if (!sDone)
                    {
                        uint fileHandle = 0;
                        try
                        {
                            Simulator.CreateExportFile(ref fileHandle, $"{buildController.mItemState}__");
                            if (fileHandle != 0)
                            {
                                CustomXmlWriter xmlWriter = new(fileHandle);
                                xmlWriter.WriteStartDocument();
                                xmlWriter.WriteStartElement("ProductList");
                                foreach (object tag in from item
                                                       in buildController.mCurrentCatalogGrid.Items
                                                       select item.mTag)
                                {
                                    BuildBuyProduct product = tag is BuildBuyProduct ? tag as BuildBuyProduct : (tag as BuildBuyPreset).Product;
                                    xmlWriter.WriteStartElement("Item");
                                    xmlWriter.WriteElementString("Title", XmlConvert.EncodeName(product.CatalogName));
                                    if (tag is BuildBuyProduct)
                                    {
                                        xmlWriter.WriteElementString("Description", XmlConvert.EncodeName(product.Description));
                                    }
                                    xmlWriter.WriteEndElement();
                                }
                                xmlWriter.WriteEndDocument();
                                xmlWriter.FlushBufferToFile();
                                sDone = true;
                                SimpleMessageDialog.Show("FurnitureScraper", "Furniture Logged");
                            }
                        }
                        finally
                        {
                            if (fileHandle != 0)
                            {
                                Simulator.CloseScriptErrorFile(fileHandle);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }
    }
}