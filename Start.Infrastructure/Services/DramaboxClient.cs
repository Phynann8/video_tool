using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Start.Infrastructure.Services
{
    public class DramaboxClient
    {
        private readonly HttpClient _httpClient;
        private string _baseUrl = "https://sapi.dramaboxdb.com";
        private string? _token;
        private string? _cookie;
        private string? _deviceId;
        private string? _androidId;
        private string _lang = "en";

        public bool IsInitialized => !string.IsNullOrEmpty(_token);

        public void SetSession(string? token = null, string? cookie = null)
        {
            if (!string.IsNullOrEmpty(token)) _token = token.Replace("Bearer ", "");
            if (!string.IsNullOrEmpty(cookie)) _cookie = cookie;
        }

        public DramaboxClient(string lang = "en")
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            _lang = lang;
        }

        public async Task EnsureInitializedAsync()
        {
            if (!string.IsNullOrEmpty(_token)) return;

            // Check if user has provided a premium token in a text file
            try
            {
                if (System.IO.File.Exists("dramabox_token.txt"))
                {
                    string fileToken = System.IO.File.ReadAllText("dramabox_token.txt").Trim();
                    if (!string.IsNullOrEmpty(fileToken))
                    {
                        _token = fileToken;
                        Console.WriteLine("DEBUG: Loaded user token from dramabox_token.txt");
                        return; // Use the provided token, skip guest bootstrap
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read token file: {ex.Message}");
            }

            // Master Fallback Token (Extracted from Epic Box)
            _token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1aWQiOiI0MzA0MzA1MjcifQ.MASTER_GUEST_TOKEN_PLACEHOLDER"; 
            
            // Note: In a real scenario, we'd still try to bootstrap first
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _androidId = DramaboxCrypto.GetRandomAndroidId();
            _deviceId = DramaboxCrypto.GenerateUUID();

            string bodyJson = "{\"distinctId\":null}";
            
            try 
            {
                string signData = $"timestamp={timestamp}{bodyJson}{_deviceId}{_androidId}";
                string sn = DramaboxCrypto.Sign(signData);

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/drama-box/ap001/bootstrap?timestamp={timestamp}");
                AddDefaultHeaders(request.Headers, _deviceId, _androidId);
                request.Headers.Add("sn", sn);
                request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                string? newToken = json["data"]?["user"]?["token"]?.ToString() ?? json["data"]?["anonymous_token"]?.ToString();
                if (!string.IsNullOrEmpty(newToken))
                {
                    _token = newToken;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bootstrap failed: {ex.Message}");
            }
        }

        private void AddDefaultHeaders(HttpRequestHeaders headers, string deviceId, string androidId, string? token = null)
        {
            headers.Add("tn", token != null ? (token.StartsWith("Bearer ") ? token : $"Bearer {token}") : "");
            if (!string.IsNullOrEmpty(_cookie))
            {
                headers.Add("Cookie", _cookie);
            }
            headers.Add("version", "470");
            headers.Add("vn", "4.7.0");
            headers.Add("cid", "DAUAF1064291");
            headers.Add("package-Name", "com.storymatrix.drama");
            headers.Add("Apn", "1");
            headers.Add("device-id", deviceId);
            headers.Add("language", _lang);
            headers.Add("current-Language", _lang);
            headers.Add("p", "48");
            headers.Add("Time-Zone", "+0700");
            headers.Add("md", "Redmi Note 8");
            headers.Add("ov", "13");
            headers.Add("over-flow", "new-fly");
            headers.Add("brand", "Xiaomi");
            headers.Add("android-id", androidId);
            headers.Add("User-Agent", "okhttp/4.10.0");
            
            // Random IP spoofing for regional block bypass (using Singapore-based range)
            var random = new Random();
            string spoofedIp = $"111.223.{random.Next(1, 255)}.{random.Next(1, 255)}";
            headers.Add("X-Forwarded-For", spoofedIp);
            headers.Add("X-Real-IP", spoofedIp);
        }

        public async Task<JObject> PostAsync(string endpoint, object payload)
        {
            await EnsureInitializedAsync();
            if (!IsInitialized)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["code"] = 403,
                    ["msg"] = "Client is not initialized with a valid token"
                };
            }

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string url = $"{_baseUrl}{endpoint}?timestamp={timestamp}";
            string bodyJson = JsonConvert.SerializeObject(payload);

            // Signature: timestamp={timestamp}{body}{deviceId}{androidId}{tn}
            string tn = $"Bearer {_token}";
            string signData = $"timestamp={timestamp}{bodyJson}{_deviceId}{_androidId}{tn}";
            string sn = DramaboxCrypto.Sign(signData);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddDefaultHeaders(request.Headers, _deviceId!, _androidId!, _token);
            request.Headers.Add("sn", sn);
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["code"] = (int)response.StatusCode,
                    ["msg"] = $"HTTP error {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            string content = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(content) || (!content.Trim().StartsWith("{") && !content.Trim().StartsWith("[")))
            {
                Console.WriteLine($"DEBUG: API returned non-JSON content: {content.Substring(0, Math.Min(content.Length, 200))}");
                return new JObject { ["code"] = -1, ["msg"] = "Server returned non-JSON response (possible block)" };
            }

            try 
            {
                var json = JObject.Parse(content);
                return json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: JSON Parse Error: {ex.Message}. Content: {content.Substring(0, Math.Min(content.Length, 100))}");
                return new JObject { ["code"] = -1, ["msg"] = "Invalid JSON format from server" };
            }
        }

        private static bool IsSuccessResponse(JObject json)
        {
            // The DramaBox API uses "success: true" and/or "status: 0"
            // Check "success" first since that's the primary indicator
            if (json["success"] != null)
                return json["success"]!.Value<bool>() == true;
            if (json["status"] != null)
                return json["status"]!.Value<int>() == 0;
            if (json["code"] != null)
                return json["code"]!.Value<int>() == 0;
            // If response has data with chapterList, consider it success
            if (json["data"]?["chapterList"] != null || json["chapterList"] != null)
                return true;
            return false;
        }

        public async Task<bool> UnlockChapterAsync(string bookId, string chapterId)
        {
            try
            {
                var payload = new
                {
                    bookId = bookId,
                    chapterId = chapterId
                };

                var response = await PostAsync("/drama-box/chapterv2/watch/reward", payload);
                return response["code"]?.Value<int>() == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<DramaBoxEpisode>> GetEpisodesAsync(string bookId, bool autoUnlock = false)
        {
            return (await GetEpisodesWithBookNameAsync(bookId, autoUnlock)).Episodes;
        }

        public async Task<(List<DramaBoxEpisode> Episodes, string? BookName)> GetEpisodesWithBookNameAsync(string bookId, bool autoUnlock = false)
        {
            var allEpisodes = new List<DramaBoxEpisode>();
            string? bookName = null;
            int index = 1;
            int totalChapterCount = 0;
            int payChapterNum = 0;
            int retryCount = 0;
            const int maxRetries = 3;

            // Fetch first batch to get metadata
            var firstBatch = await FetchBatchAsync(bookId, index);
            if (firstBatch == null || !IsSuccessResponse(firstBatch))
            {
                // Try refreshing token once
                _token = null;
                await EnsureInitializedAsync();
                firstBatch = await FetchBatchAsync(bookId, index);
                if (firstBatch == null || !IsSuccessResponse(firstBatch))
                {
                    Console.WriteLine("DEBUG: Failed to fetch first batch even after token refresh");
                    return (allEpisodes, bookName);
                }
            }

            // API returns metadata either inside "data" or at top level
            bookName = firstBatch["data"]?["bookName"]?.ToString() ?? firstBatch["bookName"]?.ToString();
            totalChapterCount = firstBatch["data"]?["chapterCount"]?.Value<int>() ?? firstBatch["chapterCount"]?.Value<int>() ?? 0;
            payChapterNum = firstBatch["data"]?["payChapterNum"]?.Value<int>() ?? firstBatch["payChapterNum"]?.Value<int>() ?? 0;
            Console.WriteLine($"DEBUG: Drama '{bookName}' | Total: {totalChapterCount} eps | Pay starts at: {payChapterNum}");

            AddChaptersFromBatch(firstBatch, allEpisodes);
            index = 6; // Reference implementation starts second batch at index 6

            while (index <= totalChapterCount)
            {
                var batchData = await FetchBatchAsync(bookId, index);
                
                var chapters = batchData["data"]?["chapterList"] as JArray
                    ?? batchData["chapterList"] as JArray;
                
                bool isEndOfBook = index + 5 >= totalChapterCount && totalChapterCount != 0;
                
                // DramaBox returns <= 2 items when the guest token limit is reached
                if (chapters != null && chapters.Count <= 2 && index != payChapterNum && !isEndOfBook)
                {
                    Console.WriteLine($"DEBUG: Data limited ({chapters.Count}). Reached guest token limit.");
                    break;
                }

                if (batchData == null || !IsSuccessResponse(batchData))
                {
                    retryCount++;
                    if (retryCount <= maxRetries)
                    {
                        Console.WriteLine($"DEBUG: Batch at index {index} failed. Retry {retryCount}/{maxRetries}...");
                        // Refresh token
                        _token = null;
                        await EnsureInitializedAsync();

                        // Re-warm session by hitting payChapterNum if we have it
                        if (payChapterNum > 0 && index != payChapterNum)
                        {
                            await FetchBatchAsync(bookId, payChapterNum);
                            await Task.Delay(1500);
                        }
                        await Task.Delay(2000);
                        continue; // Retry same index
                    }
                    else
                    {
                        Console.WriteLine($"DEBUG: Skipping index {index} after {maxRetries} retries");
                        index += 5;
                        retryCount = 0;
                        continue;
                    }
                }

                if (chapters == null || chapters.Count == 0)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        index += 5;
                        retryCount = 0;
                    }
                    else
                    {
                        await Task.Delay(2000);
                    }
                    continue;
                }

                AddChaptersFromBatch(batchData, allEpisodes);
                index += 5;
                retryCount = 0;
                await Task.Delay(800); // Respectful delay between batches
            }

            Console.WriteLine($"DEBUG: Total episodes fetched (raw): {allEpisodes.Count}");

            // Deduplicate by ChapterId
            var uniqueEpisodes = allEpisodes
                .GroupBy(e => e.ChapterId)
                .Select(g => g.First())
                .OrderBy(e => e.ChapterIndex)
                .ToList();

            Console.WriteLine($"DEBUG: Unique episodes: {uniqueEpisodes.Count}");
            return (uniqueEpisodes, bookName);
        }

        private async Task<JObject?> FetchBatchAsync(string bookId, int index)
        {
            try
            {
                var payload = new
                {
                    boundaryIndex = 0,
                    comingPlaySectionId = -1,
                    index = index,
                    currencyPlaySource = "discover_new_rec_new",
                    needEndRecommend = 0,
                    currencyPlaySourceName = "",
                    preLoad = false,
                    rid = "",
                    pullCid = "",
                    loadDirection = 1,
                    bookId = bookId
                };

                return await PostAsync("/drama-box/chapterv2/batch/load", payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: FetchBatch exception at index {index}: {ex.Message}");
                return null;
            }
        }

        private void AddChaptersFromBatch(JObject batchResponse, List<DramaBoxEpisode> allEpisodes)
        {
            // chapterList can be inside "data" or at the top level
            var chapterList = batchResponse["data"]?["chapterList"] as JArray 
                ?? batchResponse["chapterList"] as JArray;
            if (chapterList == null) return;

            foreach (var item in chapterList)
            {
                var ep = new DramaBoxEpisode
                {
                    ChapterId = item["chapterId"]?.ToString() ?? item["id"]?.ToString() ?? "",
                    ChapterIndex = item["chapterIndex"]?.Value<int>() ?? item["index"]?.Value<int>() ?? 0,
                    ChapterName = item["chapterName"]?.ToString() ?? item["name"]?.ToString() ?? "",
                    IsCharge = item["isCharge"]?.Value<int>() ?? (item["unlock"]?.Value<int>() == 1 ? 0 : 1),
                    ChapterImg = item["chapterImg"]?.ToString() ?? item["cover"]?.ToString() ?? "",
                    ChargeChapter = item["chargeChapter"]?.Value<bool>() ?? false
                };

                // Map CDN list if present
                var cdns = item["cdnList"] as JArray;
                if (cdns != null)
                {
                    foreach (var cdnItem in cdns)
                    {
                        var cdn = new DramaBoxCdn { CdnDomain = cdnItem["cdnDomain"]?.ToString() ?? "" };
                        var paths = cdnItem["videoPathList"] as JArray;
                        if (paths != null)
                        {
                            foreach (var p in paths)
                            {
                                cdn.VideoPathList.Add(new DramaBoxVideoPath
                                {
                                    Quality = p["quality"]?.Value<int>() ?? 0,
                                    VideoPath = p["videoPath"]?.ToString() ?? ""
                                });
                            }
                        }
                        ep.CdnList.Add(cdn);
                    }
                }

                allEpisodes.Add(ep);
            }
        }
    }
}
