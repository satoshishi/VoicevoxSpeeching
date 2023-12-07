namespace Speech.Speaking
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using Cysharp.Threading.Tasks;
    using Proyecto26;
    using UnityEngine;

    public class VoiceVoxAPI
    {
        public static readonly string AUDIOQUERY = "audio_query";

        public static readonly string SYNTHESIS = "synthesis";

        public static readonly string SPEAKER = "speakers";

        public static readonly string LOCAL_URL = "http://localhost:50021";

        private string URL;

        public VoiceVoxAPI(string url)
        {
            this.URL = url;
        }

        /// <summary>
        /// VoiceVoxに音声合成をリクエストするためのクエリを生成する
        /// </summary>
        /// <param name="speaker">スピーカーid</param>
        /// <param name="text">読み上げて欲しいテキスト</param>
        /// <returns>クエリ</returns>
        public UniTask<string> GetAudioQuery(int speaker, string text)
        {
            string requestURL = $"{this.URL}/{AUDIOQUERY}?text={text}&speaker={speaker}";

            RequestHelper requestHelper = new RequestHelper()
            {
                Uri = requestURL,
                Method = "POST",
            };

            UniTaskCompletionSource<string> completionSource = new UniTaskCompletionSource<string>();

            RestClient.Post(requestHelper, (requestException, responseHelper) =>
            {
                if (responseHelper.StatusCode == 200)
                {
                    Debug.Log("Voicevox GetAudioQuery : 200");
                    completionSource.TrySetResult(responseHelper.Text);
                }
                else
                {
                    Debug.Log($"Voicevox GetAudioQuery : {responseHelper.Error}");
                }
            });

            return completionSource.Task;
        }

        /// <summary>
        /// クエリに基づいた音声合成をリクエストする
        /// </summary>
        /// <param name="speaker">スピーカーid</param>
        /// <param name="audioQuery">クエリ情報</param>
        /// <returns>AudioClip</returns>
        public async UniTask<AudioClip> Synthesis(int speaker, string audioQuery)
        {
            string requestURL = $"{this.URL}/{SYNTHESIS}?speaker={speaker}";
            HttpClient httpClient = new HttpClient();

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestURL);
            request.Content = new StringContent(audioQuery, Encoding.UTF8, "application/json");
            HttpResponseMessage response = null;

            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Voicevox Synthesis : 200");
                Stream stream = await response.Content.ReadAsStreamAsync();
                return await stream.ToAudioClip();
            }
            else
            {
                string message = await response.Content.ReadAsStringAsync();
                Debug.Log($"Voicevox Synthesis : {message}");
            }

            return null;
        }

        /// <summary>
        /// スピーカーのidと名称群をリクエストする
        /// </summary>
        /// <returns>情報群</returns>
        public UniTask<List<(int, string)>> GetSpeakers()
        {
            string requestURL = $"{this.URL}/{SPEAKER}";
            HttpClient httpClient = new HttpClient();

            RequestHelper requestHelper = new RequestHelper()
            {
                Uri = requestURL,
                Method = "GET",
            };

            UniTaskCompletionSource<List<(int, string)>> completionSource = new UniTaskCompletionSource<List<(int, string)>>();

            RestClient.Get(requestHelper, (requestException, responseHelper) =>
            {
                Debug.Log(responseHelper.StatusCode);
                Debug.Log(responseHelper.Text);

                if (responseHelper.StatusCode == 200)
                {
                    Debug.Log("Voicevox GetSpeakers : 200");
                    Speakers response = new Speakers(responseHelper.Text);
                    completionSource.TrySetResult(response.Gets());
                }
                else
                {
                    Debug.Log($"Voicevox Synthesis : {responseHelper.Error}");
                }
            });

            return completionSource.Task;
        }

        [System.Serializable]
        public class Speakers
        {
            [SerializeField]
            private List<Speaker> speakers;

            public Speakers(string json)
            {
                this.speakers = JsonDeserializer.FromJsonArray<Speaker>(json);
            }

            public List<(int, string)> Gets()
            {
                List<(int, string)> speakers = new List<(int, string)>();

                foreach (Speaker speaker in this.speakers)
                {
                    speakers.AddRange(speaker.GetStyles());
                }

                return speakers;
            }

            [System.Serializable]
            public class Speaker
            {
                [SerializeField]
                private string name;

                [SerializeField]
                private Style[] styles;

                public List<(int, string)> GetStyles()
                {
                    List<(int, string)> results = new List<(int, string)>();

                    foreach (Style style in this.styles)
                    {
                        results.Add((style.Id, $"{style.Name}な{this.name}"));
                    }

                    return results;
                }

                [System.Serializable]
                public class Style
                {
                    [SerializeField]
                    private string name;

                    [SerializeField]
                    private int id;

                    public string Name => this.name;

                    public int Id => this.id;
                }
            }
        }
    }
}
