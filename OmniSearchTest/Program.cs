using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace OmniSearchTest
{
    class Program
    {
        static void Main(string[] _)
        {
            var exactMatch = new ExactMatch<object>();
            var tf = new TermFrequency<object>();
            var tfidf = new TFIDFUnigram<object>();
            RunQuery(@"Furniture Scrape\Buy\Appliances__Export_ABC106B2.xml", "kno", exactMatch);
            RunQuery(@"Furniture Scrape\Buy\Appliances__Export_ABC106B2.xml", "kno", tf);
            RunQuery(@"Furniture Scrape\Build\Trees__Export_3CF703D8.xml", "small tree", exactMatch);
            RunQuery(@"Furniture Scrape\Build\Trees__Export_3CF703D8.xml", "small      \ntree", tf);
            RunQuery(@"Furniture Scrape\Build\Trees__Export_3CF703D8.xml", "small      \ntree", tfidf);
            RunQuery(@"Furniture Scrape\Buy\Entertainment__Export_702F7408.xml", "gnubb", tfidf);
            RunQuery(@"Furniture Scrape\Build\WallPaint__Export_5E06D03C.xml", "baseboard", tfidf);
            RunQuery(@"Furniture Scrape\Buy\Comfort__Export_1A1B0653.xml", "modern chair", tfidf);
            RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "rustic painting", tf);
            RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "rustic painting", tfidf);
            RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "small wall with incredibly thick paint and a shining rock face on the cliff facing the sky and smiling", tfidf);
            RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "arcturus tiktok", tfidf);
            Debugger.Break();
        }

        static void RunQuery(string inFile, string query, ISearchModel<object> model)
        {
            List<Document<object>> docs = new();
            using (XmlReader reader = XmlReader.Create(inFile))
            {
                for (bool notEnd = reader.ReadToFollowing("Item"); notEnd; notEnd = reader.ReadToNextSibling("Item"))
                {
                    using (XmlReader curNode = reader.ReadSubtree())
                    {
                        curNode.ReadToDescendant("Title");
                        string title = XmlConvert.DecodeName(curNode.ReadElementContentAsString());
                        string description = curNode.ReadToNextSibling("Description") ? XmlConvert.DecodeName(curNode.ReadElementContentAsString()) : "";
                        docs.Add(new(title, description, null));
                    }
                }
            }

            Debugger.Log(0, "", $"RESULTS FOR \"{query}\":\n\n");
            if (new List<object>(model.Search(docs, query)).Count == 0)
            {
                Debugger.Log(0, "", "N/A");
            }
            Debugger.Log(0, "", "\n\n");
        }
    }
}
