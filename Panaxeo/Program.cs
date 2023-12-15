using System.Diagnostics;

namespace Panaxeo
{
    public static class Program
    {
        public static void Main()
        {
            //AnalyzeMaps();
            //AnalyzeAllMaps();

            if (Settings.IS_LOCAL_TEST)
            {
                var apiResult = FireResponse.Test();

                var map = new Map(apiResult);

                map.OutputMap();

                map.CalculatePossibleTargets();

                var nextFirePoint = map.GetTargetPoint();
                map.GetPoint(nextFirePoint.X, nextFirePoint.Y).Type = MapPoint.MapPointType.Fire;

                map.OutputMap();

            }
            else
            {
                var apiResult = FireResponse.CallApi();
                var firstRun = true;

                while (!apiResult.Finished)
                {
                    if (apiResult.Grid.All(i => i == '*') && !firstRun)
                    {
                        apiResult = FireResponse.CallApi();
                    }

                    var map = new Map(apiResult);

                    Console.Clear();
                    map.OutputMap();
                    map.CalculatePossibleTargets();

                    map.OutputMap();
                    map.OutputMap(Map.MapOutputType.TargetMatchScore);

                    var nextFirePoint = map.GetTargetPoint();
                    map.OutputMap(Map.MapOutputType.TargetMatchScore);

                    if (apiResult.AvengerAvailable)
                    {
                        nextFirePoint.UseAvenger = MapPoint.MapPointAvenger.ironman;
                        //nextFirePoint.UseAvenger = MapPoint.MapPointAvenger.thor;
                    }

                    map.GetPoint(nextFirePoint.X, nextFirePoint.Y).Type = MapPoint.MapPointType.Fire;
                    map.OutputMap();

                    Console.WriteLine($"mapId:{apiResult.MapId}  count:{apiResult.MoveCount}");
                    Console.WriteLine($"remaining: {map.Points.Count(i => i.Type == MapPoint.MapPointType.Target)}/26");

                    apiResult = FireResponse.CallApi(nextFirePoint);

                    if (apiResult.MoveCount > 50)
                    {
                        var breakpoint = 0;
                    }

                    firstRun = false;

                    if (apiResult.Finished)
                    {
                        map = new Map(apiResult);
                        map.OutputMap();

                    }
                }
            }
        }
    }
}