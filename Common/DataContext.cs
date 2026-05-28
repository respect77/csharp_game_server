using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Common
{
    public enum DataTypeEnum
    {
        None = 0,
        Character = 1,
    }

    public abstract class DataBase
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }

    public class CharacterData : DataBase
    {
        [JsonPropertyName("attack_type")]
        public int AttackType { get; set; }

        [JsonPropertyName("character_affinity_type")]
        public int AffinityType { get; set; }

        [JsonPropertyName("character_grade_type")]
        public int Grade { get; set; }
        [JsonPropertyName("character_min_max_level")]
        public List<int> MinMaxLevel { get; set; } = new();
        
    }
    public class DataContext
    {
        private static readonly Lazy<DataContext> _instance = new(() => new DataContext());
        public static DataContext Instance => _instance.Value;

        private string _basePath = string.Empty;

        private readonly ConcurrentDictionary<DataTypeEnum, Dictionary<int, DataBase>> _dataSetDic = new();

        private DataContext()
        {
        }

        public void LoadData(string path)
        {
            _basePath = path;
            List<Task> tasks = [
                //LoadData<CharacterData>(DataTypeEnum.Character, "Characters.json")
                ];

            Task t = Task.WhenAll(tasks);
            try
            {
                t.Wait();
            }
            catch (Exception ex)
            {
                throw new Exception($"Task Error: {ex.Message}");
            }
        }

        private static readonly JsonSerializerOptions s_writeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly HttpClient s_httpClient = new HttpClient();

        private async Task LoadData<T>(DataTypeEnum type, string filePath) where T : DataBase
        {
            Stream dataStream; // 데이터를 읽어올 스트림 변수

            // basePath가 웹 주소(URL)인지 확인
            if (Uri.IsWellFormedUriString(_basePath, UriKind.Absolute))
            {
                // 웹에서 데이터를 가져옵니다.
                string fullUrl = Path.Combine(_basePath, filePath).Replace('\\', '/'); // URL 경로 구분자는 '/' 입니다.
                try
                {
                    dataStream = await s_httpClient.GetStreamAsync(fullUrl);
                }
                catch (HttpRequestException ex)
                {
                    // 네트워크 오류 또는 404/500 등의 오류 처리
                    throw new Exception($"Failed to fetch data from web: {fullUrl}", ex);
                }
            }
            else
            {
                // 로컬 파일에서 데이터를 가져옵니다.
                string fullLocalPath = Path.Combine(_basePath, filePath);
                if (!File.Exists(fullLocalPath))
                {
                    throw new FileNotFoundException($"Local data file not found: {fullLocalPath}");
                }
                dataStream = File.OpenRead(fullLocalPath);
            }

            // 스트림을 사용하여 Deserialize 하는 부분은 동일합니다.
            // using 문을 사용하여 스트림을 안전하게 닫도록 보장합니다.
            using (dataStream)
            {
                var dataList = await JsonSerializer.DeserializeAsync<List<T>>(dataStream, s_writeOptions)
                               ?? throw new Exception($"DeserializeAsync Error: {filePath}");

                switch (type)
                {
                    default:
                        _dataSetDic.TryAdd(type, dataList.ToDictionary(data => data.Id, data => (DataBase)data));
                        break;
                }
            }
        }

        private DataBase? GetData(DataTypeEnum type, int id)
        {
            if (!_dataSetDic.TryGetValue(type, out var dataDic))
            {
                return null;
            }
            if (!dataDic.TryGetValue(id, out var data))
            {
                return null;
            }
            return data;
        }

        //public CardData? GetCardData(int id) => GetData(DataTypeEnum.Card, id) as CardData;
    }
}
