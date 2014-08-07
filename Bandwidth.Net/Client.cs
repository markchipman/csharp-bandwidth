﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Bandwidth.Net.Clients;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Bandwidth.Net
{
    public sealed class Client: IDisposable
    {
        private readonly HttpClient _client;
        private readonly string _userPath;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

  
        public Client(string userId, string apiToken, string secret, string host = "api.catapult.inetwork.com")
        {
            if (userId == null) throw new ArgumentNullException("userId");
            if (apiToken == null) throw new ArgumentNullException("apiToken");
            if (secret == null) throw new ArgumentNullException("secret");
            _userPath = string.Format("users/{0}", userId);
            _client = new HttpClient {BaseAddress = new UriBuilder("https", host, 443, "/v1/").Uri};
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:{1}", apiToken, secret))));
            _jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            _jsonSerializerSettings.Converters.Add(new StringEnumConverter{CamelCaseText = true, AllowIntegerValues = false});
            Calls = new Calls(this);
            Recordings = new Recordings(this);
            Account = new Account(this);
            Applications = new Applications(this);
        }

        
        #region Base Http methods
        internal async Task<HttpResponseMessage> MakeGetRequest(string path, IDictionary<string, string> query = null, string id = null, bool disposeResponse = false)
        {
            var urlPath = FixPath(path);
            if(id != null)
            {
                urlPath = urlPath + "/" + id;
            }
            if (query != null && query.Count > 0)
            {
                urlPath = string.Format("{0}?{1}", urlPath, string.Join("&", from p in query select string.Format("{0}={1}", p.Key, Uri.EscapeDataString(p.Value))));
            }
            var response = await _client.GetAsync(urlPath);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                response.Dispose();
                throw;
            }
            if (!disposeResponse) return response;
            response.Dispose();
            return null;

        }

        
        internal async Task<TResult> MakeGetRequest<TResult>(string path, IDictionary<string, string> query = null,
            string id = null)
        {
            using (var response = await MakeGetRequest(path, query, id))
            {
                if (response.Content.Headers.ContentType.MediaType == "application/json")
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json.Length > 0
                        ? JsonConvert.DeserializeObject<TResult>(json, _jsonSerializerSettings)
                        : default(TResult);
                }
            }
            return default(TResult);
        }

        internal async Task<HttpResponseMessage> MakePostRequest(string path, object data, bool disposeResponse = false)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.None, _jsonSerializerSettings);
            var response = await _client.PostAsync(FixPath(path), new StringContent(json, Encoding.UTF8, "application/json"));
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                response.Dispose();
                throw;
            }
            if (!disposeResponse) return response;
            response.Dispose();
            return null;
        }

        internal async Task<TResult> MakePostRequest<TResult>(string path, object data)
        {
            using (var response = await MakePostRequest(path, data))
            {
                if (response.Content.Headers.ContentType.MediaType == "application/json")
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return json.Length > 0
                        ? JsonConvert.DeserializeObject<TResult>(json, _jsonSerializerSettings)
                        : default(TResult);
                }
            }
            return default(TResult);
        }

        internal async Task MakeDeleteRequest(string path)
        {
            using (var response = await _client.DeleteAsync(FixPath(path)))
            {
                response.EnsureSuccessStatusCode();
            }
        }
        #endregion


        internal string ConcatUserPath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (path[0] == '/')
            {
                return _userPath + path;
            }
            return string.Format("{0}/{1}", _userPath, path);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
        private static string FixPath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            return (path[0] == '/') ? path.Substring(1) : path;
        }

        public Calls Calls { get; private set; }
        public Recordings Recordings { get; private set; }
        public Account Account { get; private set; }
        public Applications Applications { get; private set; }
    }
}
