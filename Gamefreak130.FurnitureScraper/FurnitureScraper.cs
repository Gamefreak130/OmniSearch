using Gamefreak130.Common.Loggers;
using Sims3.Gameplay.EventSystem;
using Sims3.SimIFace;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI;
using System;
using System.Linq;
using System.Text;

namespace Gamefreak130
{
    public class FurnitureScraper
    {
        [Tunable]
        private static readonly bool kCJackB;

        static FurnitureScraper() => World.OnWorldLoadFinishedEventHandler += OnWorldLoadFinished;

        private static void OnWorldLoadFinished(object sender, EventArgs e) 
            => EventTracker.AddListener(EventTypeId.kEnterInWorldSubState, delegate {
                   Simulator.AddObject(new OneShotFunctionTask(StartFurnitureScraper, StopWatch.TickStyles.Seconds, 5));
                   return ListenerAction.Keep;
               });

        private static void StartFurnitureScraper()
        {
            if (BuyController.sController is not null)
            {
                BuyController.sController.mCatalogProductFilter.FiltersChanged += ScrapeFurniture;
                SimpleMessageDialog.Show("Furniture Scraper", "Ready to Scrape");
            }
        }

        private static void ScrapeFurniture()
        {
            foreach (BuildBuyProduct product in BuyController.sController.mCatalogGrid.Items.Select(item => item.mTag as BuildBuyProduct))
            {
                FurnitureLogger.sSingleton.Log(product);
            }
            FurnitureLogger.sSingleton.WriteLog();
        }
    }

    public class FurnitureLogger : Logger<BuildBuyProduct>
    {
        public static FurnitureLogger sSingleton = new();

        private readonly StringBuilder mBuilder = new();

        public override void Log(BuildBuyProduct input)
        {
            mBuilder.AppendLine($"  <Title>{input.CatalogName}</Title>");
            mBuilder.AppendLine("  <Description>");
            mBuilder.AppendLine(input.Description);
            mBuilder.AppendLine("  </Description>");
        }

        public void WriteLog() => base.WriteLog(mBuilder);
    }
}