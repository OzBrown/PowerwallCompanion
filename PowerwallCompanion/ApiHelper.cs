﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage;
using Windows.Web.Http.Filters;

namespace PowerwallCompanion
{
    static class ApiHelper
    {
        public const string BaseUrl = "https://owner-api.teslamotors.com";

        public static async Task<JObject> CallGetApiWithTokenRefresh(string url, string demoId)
        {
            if (Settings.AccessToken == null)
            {
                throw new UnauthorizedAccessException();
            }

            try
            {
                return await CallGetApi(url, demoId);
            }
            catch (UnauthorizedAccessException)
            {
                // First fail - try refreshing
                await RefreshToken();
                return await CallGetApi(url, demoId);

            }
        }

        public static async Task<JObject> CallApiIgnoreCerts(string url)
        {
            var filter = new HttpBaseProtocolFilter();
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Expired);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);
            filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
            filter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;

            using (var client = new Windows.Web.Http.HttpClient(filter))
            {
                var response = await client.GetAsync(new Uri(url));
                var responseMessage = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    return JObject.Parse(responseMessage);
                }
                else
                {
                    throw new HttpRequestException(responseMessage);
                }
            }
        }

        private static async Task<JObject> CallGetApi(string url, string demoId)
        {
            if (Settings.AccessToken == "DEMO")
            {
                return await GetDemoDocument(demoId);
            }
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Settings.AccessToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("X-Tesla-User-Agent");
            var response = await client.GetAsync(url);
            var responseMessage = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return JObject.Parse(responseMessage);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException();
            }
            else
            {
                throw new HttpRequestException(responseMessage);
            }
        }

        private static async Task<JObject> GetDemoDocument(string demoId)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///DemoData/{demoId}.json"));
            using (var inputStream = await file.OpenReadAsync())
            using (var classicStream = inputStream.AsStreamForRead())
            using (var streamReader = new StreamReader(classicStream))
            {
                var content = await streamReader.ReadToEndAsync();
                return JObject.Parse(content);
            }
        }

        private static async Task RefreshToken()
        {
            var message = new JObject();
            message["grant_type"] = "refresh_token";
            message["refresh_token"] = Settings.RefreshToken;

            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("X-Tesla-User-Agent");
            var requestMessage = new StringContent(message.ToString(), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(BaseUrl  + "/oauth/token", requestMessage);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
                Settings.AccessToken = responseJson["access_token"].Value<string>();
                Settings.RefreshToken = responseJson["refresh_token"].Value<string>();
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }
    }

 
}
