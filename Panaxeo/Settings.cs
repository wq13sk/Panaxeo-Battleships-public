using Newtonsoft.Json;

namespace Panaxeo
{
    internal class Settings
    {
        public const int GRID_SIZE = 12;

        public const int INITIAL_X = 7;

        public const int INITIAL_Y = 6;

        public const int MAX_SHIP_LENGTH = 5;

        public const string BASE_URL = "https://europe-west1-ca-2023-dev.cloudfunctions.net/battleshipsApi/fire";

        public const bool IS_TEST = false;

        public const bool IS_LOCAL_TEST = false;

        public const bool ENABLE_STATISTICS = true;

        public const string FILE_PATH = "data.txt";

        public const string Token = "";
    
        public static void SaveFile(FileResponse response)
        {
            System.IO.File.WriteAllText(FILE_PATH, JsonConvert.SerializeObject(response));
        }

        public static void DeleteFile()
        {
            File.Delete(FILE_PATH);
        }

        public static FileResponse LoadFile(int mapId)
        {
            if(!File.Exists(FILE_PATH))
            {
                return null;
            }

            var fileContent = File.ReadAllText(FILE_PATH);

            var data = JsonConvert.DeserializeObject<FileResponse>(fileContent);

            if(data.MapId != mapId)
            {
                DeleteFile();
                return null;
            }

            return data;
        }
    }

    public class FileResponse
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int MapId { get; set; }

        public int ShipSize { get; set; }
    }
}

