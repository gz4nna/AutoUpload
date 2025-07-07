// ../AutoUpload.Models/Helpers/HttpRetryHelper.cs
using log4net;
using System.Text.Json;  // 添加这个

public class HttpRetryHelper
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(HttpRetryHelper));
    /// <summary>
    /// 静态HttpClient实例，用于重用连接
    /// </summary>
    private static readonly HttpClient client;

    /// <summary>
    /// 静态构造函数，初始化HttpClient
    /// </summary>
    static HttpRetryHelper()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests
        };

        client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30); // 设置默认超时时间
    }

    /// <summary>
    /// 重试执行HTTP请求
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="httpRequest"></param>
    /// <param name="maxRetries"></param>
    /// <param name="initialDelayMs"></param>
    /// <param name="requestInfo"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    /// <exception cref="AggregateException"></exception>
    public static async Task<T?> RetryHttpRequestAsync<T>(
        Func<Task<HttpResponseMessage>> httpRequest,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        string? requestInfo = null) where T : class
    {
        var exceptions = new List<Exception>();

        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                log.Info($"[尝试 {i + 1}/{maxRetries + 1}] {requestInfo ?? "发起HTTP请求"}");

                var response = await httpRequest();
                var content = await response.Content.ReadAsStringAsync();

                log.Info($"[响应状态] {response.StatusCode}");
                log.Info($"[响应内容] {content}");

                if (!response.IsSuccessStatusCode)
                {
                    if (i == maxRetries)
                    {
                        log.Error($"请求失败，状态码：{response.StatusCode}，响应内容：{content}");
                        throw new HttpRequestException($"Status: {response.StatusCode}, Content: {content}");
                    }
                    log.Warn($"请求失败，将在 {initialDelayMs * Math.Pow(2, i)}ms 后重试");
                    exceptions.Add(new HttpRequestException($"Status: {response.StatusCode}"));
                    continue;
                }

                var result = JsonSerializer.Deserialize<T>(content);
                log.Info($"[反序列化结果] {JsonSerializer.Serialize(result)}");
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (i == maxRetries)
                {
                    log.Error($"重试次数已用尽，最后一次异常：{ex.Message}");
                    throw;
                }
                log.Error($"请求异常：{ex.Message}，将在 {initialDelayMs * Math.Pow(2, i)}ms 后重试");
                exceptions.Add(ex);
            }

            var delayMs = initialDelayMs * Math.Pow(2, i);
            await Task.Delay((int)delayMs);
        }

        throw new AggregateException("所有重试都失败", exceptions);
    }

    /// <summary>
    /// 配置HttpClient的请求头信息
    /// </summary>
    /// <param name="tenantId"></param>
    /// <param name="traceId"></param>
    /// <param name="userId"></param>
    /// <param name="userName"></param>
    public static void ConfigureHttpClient(string tenantId, string traceId, string userId, string? userName = null)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        client.DefaultRequestHeaders.Add("X-Trace-Id", traceId);
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        if (!string.IsNullOrEmpty(userName))
        {
            client.DefaultRequestHeaders.Add("X-User-Name", userName);
        }
        client.DefaultRequestHeaders.Add("X-Timestamp", DateTime.Now.ToString());
    }

    /// <summary>
    /// 获取配置好的HttpClient实例
    /// </summary>
    /// <returns></returns>
    public static HttpClient GetClient() => client;
}
