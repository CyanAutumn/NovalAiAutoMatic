using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json;
using System.Runtime.Remoting.Proxies;
using System.Net;
using System.Net.Http;

namespace AutoNai3Tools.utils
{
    public static class Request
    {
        public static RestResponse Post(string url, string path, string jsonBody, string token = null, string proxy = null)
        {
            return PostAsync(url, path, jsonBody, token, proxy).GetAwaiter().GetResult();
        }

        public static async Task<RestResponse> PostAsync(
            string url,
            string path,
            string jsonBody,
            string token = null,
            string proxy = null,
            CancellationToken cancellationToken = default)
        {
            var options = new RestClientOptions(url) {
                Timeout = TimeSpan.FromSeconds(120),
                UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
                ThrowOnAnyError = false,
            };

            if (!string.IsNullOrEmpty(proxy))
                options.Proxy = new WebProxy(proxy);

            var client = new RestClient(options);

            var request = new RestRequest(path, Method.Post);
            request.AddHeader("accept", "*/*");
            request.AddHeader("accept-language", "zh-CN,zh;q=0.9,en;q=0.8");
            if (!string.IsNullOrEmpty(token))
                request.AddHeader("authorization", $"Bearer {token}");

            request.AddHeader("content-type", "application/json");
            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://novelai.net");
            request.AddHeader("priority", "u=1, i");
            request.AddHeader("referer", "https://novelai.net/");
            request.AddHeader("sec-ch-ua", "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"");
            request.AddHeader("sec-ch-ua-mobile", "?0");
            request.AddHeader("sec-ch-ua-platform", "\"Windows\"");
            request.AddHeader("sec-fetch-dest", "empty");
            request.AddHeader("sec-fetch-mode", "cors");
            request.AddHeader("sec-fetch-site", "same-site");

            request.AddStringBody(jsonBody, DataFormat.Json);

            var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (response == null)
                throw new HttpRequestException("请求未得到服务器响应。");

            if (!response.IsSuccessful) {
                string detail = ExtractErrorDetail(response);
                string status = response.StatusCode != 0 ? $"{(int)response.StatusCode} ({response.StatusCode})" : "未知";
                throw new HttpRequestException($"Request failed with status code {status}. Server message: {detail}");
            }

            return response;
        }

        public static async Task<RestResponse> GetAsync(
            string url,
            string path,
            string token = null,
            string proxy = null,
            CancellationToken cancellationToken = default)
        {
            var options = new RestClientOptions(url) {
                Timeout = TimeSpan.FromSeconds(60),
                UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
                ThrowOnAnyError = false,
            };

            if (!string.IsNullOrEmpty(proxy))
                options.Proxy = new WebProxy(proxy);

            var client = new RestClient(options);

            var request = new RestRequest(path, Method.Get);
            request.AddHeader("accept", "*/*");
            request.AddHeader("accept-language", "zh-CN,zh;q=0.9,en;q=0.8");
            if (!string.IsNullOrEmpty(token))
                request.AddHeader("authorization", $"Bearer {token}");

            request.AddHeader("dnt", "1");
            request.AddHeader("origin", "https://novelai.net");
            request.AddHeader("referer", "https://novelai.net/");

            var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            if (response == null)
                throw new HttpRequestException("请求未得到服务器响应。");

            if (!response.IsSuccessful) {
                string detail = ExtractErrorDetail(response);
                string status = response.StatusCode != 0 ? $"{(int)response.StatusCode} ({response.StatusCode})" : "未知";
                throw new HttpRequestException($"Request failed with status code {status}. Server message: {detail}");
            }

            return response;
        }
        private static string ExtractErrorDetail(RestResponse response) {
            if (!string.IsNullOrWhiteSpace(response.Content)) {
                string content = response.Content.Trim();
                if (content.Length > 1000)
                    content = content.Substring(0, 1000) + "...";
                return content;
            }

            if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
                return response.ErrorMessage;

            if (response.ErrorException != null)
                return response.ErrorException.Message;

            return "服务器未返回错误详情。";
        }
    }
}
