using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Panaxeo.Program;

namespace Panaxeo
{
    public class MapAnalysis
    {
        public class MapAnalyzer
        {
            public int Index { get; set; }

            public int Count { get; set; }
        }

        public class MapAnalyzerWholeMap
        {
            public int MapId { get; set; }

            public int Batch { get; set; }

            public string Grid { get; set; }
        }

        public class MapAnalyzerWholeMapComparison
        {
            public int MapId1 { get; set; }

            public int Batch1 { get; set; }

            public int MapId2 { get; set; }

            public int Batch2 { get; set; }

            public int LevensteinDistance { get; set; }
        }

        public static List<MapAnalyzer> AnalyzeMaps()
        {
            var array = new int[144];

            var list = new List<MapAnalyzer>();

            for (var i = 0; i < 144; i++)
            {
                list.Add(new MapAnalyzer() { Index = i });
            }

            var directoryNames = new List<string>()
            { "maps4/", "maps5/", "maps6/", "maps7/", "maps8/", "maps9/", "maps10/", "maps11/", "maps12/", "maps13/", "maps14/", "maps15/"};


            foreach (var direcroryName in directoryNames)
            {
                foreach (var file in Directory.EnumerateFiles(direcroryName))
                {
                    var fileContent = File.ReadAllText($"{file}");

                    for (var i = 0; i < fileContent.Count(); i++)
                    {
                        list.Single(d => d.Index == i).Count += fileContent[i] == 'X' ? 1 : 0;
                    }
                }
            }

            list.ForEach(i => i.Count = i.Count / directoryNames.Count);

            //Console.WriteLine(string.Join("\r\n", list.OrderByDescending(i => i.Index).Select(i => $"{i.Index}: {i.Count}")));
            //Console.WriteLine();
            //Console.WriteLine();

            //for (var i = 0; i < 144; i++)
            //{
            //    Console.Write($"{list.Single(d => d.Index == i).Count}\t");

            //    if (i % 12 == 11)
            //    {
            //        Console.WriteLine();
            //        Console.WriteLine();
            //    }
            //}



            return list;
            //array.ToList().ForEach(i => Console.WriteLine(i));
        }

        public static List<MapAnalyzer> AnalyzeAllMaps()
        {
            var array = new int[144];

            var list = new List<MapAnalyzer>();
            var mapList = new List<MapAnalyzerWholeMap>();

            var directoryNames = new List<string>()
            { "maps4/", "maps5/", "maps6/", "maps7/", "maps8/", "maps9/", "maps10/", "maps11/", "maps12/", "maps13/", "maps14/", "maps15/"};

            for (var i = 0; i < 144; i++)
            {
                list.Add(new MapAnalyzer() { Index = i });
            }

            for (var batch = 0; batch < directoryNames.Count; batch++)
            {
                foreach (var file in Directory.EnumerateFiles(directoryNames[batch]))
                {
                    var fileContent = File.ReadAllText($"{file}");

                    mapList.Add(new MapAnalyzerWholeMap() { Batch = batch, Grid = fileContent, MapId = int.Parse(new FileInfo(file).Name.Replace(new FileInfo(file).Extension, "")) });
                }
            }

            mapList.ForEach(i => i.Grid.ToList().ForEach(d => d = d == 'X' ? 'X' : '.'));

            var comparisonList = new List<MapAnalyzerWholeMapComparison>();

            for (var indexMap1 = 0; indexMap1 < mapList.Count; indexMap1++)
            {
                for (var indexMap2 = 0; indexMap1 + indexMap2 + 1 < mapList.Count; indexMap2++)
                {
                    var counterChar = 0;

                    for (var indexChar = 0; indexChar < 144; indexChar++)
                    {
                        if (mapList[indexMap1].Grid[indexChar] == 'X' && mapList[indexMap1].Grid[indexChar] == mapList[indexMap1 + indexMap2 + 1].Grid[indexChar])
                        {
                            counterChar++;
                        }

                        if (indexChar == 143)
                        {
                            comparisonList.Add(new MapAnalyzerWholeMapComparison() { Batch1 = mapList[indexMap1].Batch, MapId1 = mapList[indexMap1].MapId, Batch2 = mapList[indexMap2].Batch, MapId2 = mapList[indexMap2].MapId, LevensteinDistance = counterChar });
                        }
                    }
                }
            }

            var ordered = comparisonList.OrderByDescending(i => i.LevensteinDistance).ToList();

            var grouped = comparisonList
                .GroupBy(i => i.LevensteinDistance)
                .Select(i => new { i.Key, Count = i.Count() })
                .OrderByDescending(i => i.Key)
                .ToList();


            return list;
        }
    }
}
