using RestSharp;

namespace Panaxeo
{
    public class FireResponse
    {
        public string? Grid { get; set; }

        public string? Cell { get; set; }

        public bool Result { get; set; }

        public bool AvengerAvailable { get; set; }

        public int MapId { get; set; }

        public int MapCount { get; set; }

        public int MoveCount { get; set; }

        public bool Finished { get; set; }


        internal static FireResponse CallApi()
        {
            return CallApi(new MapPoint(Settings.INITIAL_X, Settings.INITIAL_Y));
        }

        internal static FireResponse CallApi(MapPoint point)
        {
            var url = $"/{point.X}/{point.Y}";

            if (point.UseAvenger != MapPoint.MapPointAvenger.None)
            {
                url = $"{url}/avenger/{point.UseAvenger.ToString()}";
            }

            if (Settings.IS_TEST)
            {
                url = $"{url}?test=yes";
            }

            if (point.UseAvenger == MapPoint.MapPointAvenger.ironman)
            {
                var response = ExecuteIronMan(url);

                var avengerResult = response.AvengerResult.First();

                Settings.SaveFile(new FileResponse() { MapId = response.MapId, X = avengerResult.MapPoint.X, Y = avengerResult.MapPoint.Y, ShipSize = point.CurrentMinShipSize });

                return CallApi(new MapPoint(avengerResult.MapPoint.X, avengerResult.MapPoint.Y));
            }

            return Execute(url);
        }

        private static FireResponse Execute(string url)
        {
            var client = new RestClient(Settings.BASE_URL);

            //var request = new RestRequest(new Uri(url), Method.Get);
            var request = new RestRequest(url, Method.Get);
            request.AddHeader("Authorization", $"Bearer {Settings.Token}");

            var result = client.Execute<FireResponse>(request);

            Directory.CreateDirectory("maps/");
            File.WriteAllText($"maps/{result.Data.MapId}.txt", result.Data.Grid);


            return result.Data;
        }

        private static AvengerFireResponse ExecuteIronMan(string url)
        {
            var client = new RestClient(Settings.BASE_URL);

            var request = new RestRequest(url, Method.Get);
            request.AddHeader("Authorization", $"Bearer {Settings.Token}");

            var result = client.Execute<AvengerFireResponse>(request);

            return result.Data;
        }

        internal static FireResponse Test()
        {

            var map = @"


*	.	*	*	*	*	*	*	*	*	*	*
*	*	*	*	*	*	*	x	*	*	*	*
*	*	*	*	x	x	x	x	x	.	*	*
*	*	*	*	*	*	.	*	*	*	*	*
.	*	*	*	*	*	*	*	*	x	*	*
*	*	*	*	*	*	*	*	*	*	*	*
*	*	.	.	*	*	*	*	*	*	*	*
*	*	x	*	*	*	*	*	.	*	*	*
*	*	x	*	*	*	*	.	*	.	*	*
*	*	.	*	x	x	x	x	.	*	*	*
*	*	*	*	*	*	*	*	*	*	*	*
*	*	*	*	*	*	*	*	*	*	*	*

";



            return new FireResponse()
            {
                Grid = map.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", string.Empty),
            };
        }
    }

    public class AvengerFireResponse : FireResponse
    {
        public List<AvengerResultResponse> AvengerResult { get; set; }


        public class AvengerResultResponse
        {
            public AvengerMapPoint MapPoint { get; set; }
        }

        public class AvengerMapPoint
        {
            public int X { get; set; }

            public int Y { get; set; }
        }
    }
}