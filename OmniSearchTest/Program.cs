﻿using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Xml;

#if DEBUG
Debugger.Break();
#endif
Helpers.RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "gnom");
Helpers.RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "gnome");
Helpers.RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "modern sculpture");
Helpers.RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "into the future sculpture");
Helpers.RunQuery(@"Furniture Scrape\Buy\Decor__Export_A2ED8725.xml", "small wall with incredibly thick paint and a shining rock face on the cliff facing the sky and smiling");
#if !DEBUG
Debugger.Break();
#endif

static class Helpers
{
    public static void RunQuery(string inFile, string query)
    {
        // Load documents from corpus XML
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

        // Get all available search models                                                                                          
        List<ConstructorInfo> models = new List<Type>(typeof(ISearchModel<>).Assembly.GetTypes()).FindAll(type => !type.IsAbstract && !type.IsNested && type.Namespace == "Gamefreak130.OmniSearchSpace.Models")
                                                                                                 .ConvertAll(type => type.MakeGenericType(typeof(object))
                                                                                                 .GetConstructor(new[] { typeof(IEnumerable<Document<object>>) }));
        // Conduct search using each available model
        foreach (ConstructorInfo ctor in models)
        {
            DateTime start = DateTime.Now;

            ISearchModel<object> model = ctor.Invoke(new[] { docs }) as ISearchModel<object>;
            Debugger.Log(0, "", $"Construction took about {(DateTime.Now - start).TotalMilliseconds} milliseconds\n\n");

            Type modelType = model.GetType();
            Debugger.Log(0, "", $"RESULTS FOR \"{query}\" using {modelType.Name.Substring(0, modelType.Name.IndexOf("`"))}:\n\n");

            start = DateTime.Now;
            int count = 0;

            // Although we're merely incrementing a count here, the procedure to select the item will log info to the debugger,
            // Simulating a significantly complex in-game procedure such as loading item thumbnails and generating UI elements
            foreach (object _ in model.Search(query))
            {
                count++;
            }

            Debugger.Log(0, "", $"{count} results\n\n");

            Debugger.Log(0, "", $"Search took about {(DateTime.Now - start).TotalMilliseconds} milliseconds\n\n");
#if DEBUG
            Debugger.Break();
#endif
        }
    }
}
