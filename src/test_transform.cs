using System;
using System.Collections.Generic;
using EverythingFastAlias.Services;
using EverythingFastAlias.Models;

class Program
{
    static void Main()
    {
        var options = new SearchOptions
        {
            UseFastAlias = true,
            IncludeRecycleBin = false,
            Scope = SearchScope.All
        };
        options.TargetDrives.Add(""P:"");
        options.MediaPresets = new List<string> { ""폴더"" };

        var mappings = new Dictionary<string, List<string>>
        {
            { ""마키 호조"", new List<string> { ""Maki Hojo"", ""北条麻妃"" } }
        };

        var query = QueryTransformer.Transform(""마키 호조"", options, mappings);
        Console.WriteLine(""Generated Query:"");
        Console.WriteLine(query);
    }
}
