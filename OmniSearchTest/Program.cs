using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace OmniSearchTest
{
    class Program
    {
        static void Main(string[] args)
        {
            RunQuery(@"Furniture Scrape\Buy\Appliances__Export_ABC106B2.xml", "fridge");
        }

        static void RunQuery(string inFile, string query)
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
            IEnumerable<object> results = new ExactMatch<object>().Search(docs, query);

            foreach (object _ in results)
            {
            }

            Debugger.Break();
        }
    }
}
