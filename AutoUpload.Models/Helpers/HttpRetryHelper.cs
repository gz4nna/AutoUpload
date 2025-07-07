// ../AutoUpload.Models/Helpers/HttpRetryHelper.cs
using log4net;
using System.Text.Json;  // ������

public class HttpRetryHelper
{
    /// <summary>
    /// ��־��¼��
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(HttpRetryHelper));
    /// <summary>
    /// ��̬HttpClientʵ����������������
    /// </summary>
    private static readonly HttpClient client;

    /// <summary>
    /// ��̬���캯������ʼ��HttpClient
    /// </summary>
    static HttpRetryHelper()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests
        };

        client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30); // ����Ĭ�ϳ�ʱʱ��
    }

    /// <summary>
    /// ����ִ��HTTP����
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
                log.Info($"[���� {i + 1}/{maxRetries + 1}] {requestInfo ?? "����HTTP����"}");

                var response = await httpRequest();
                var content = await response.Content.ReadAsStringAsync();

                log.Info($"[��Ӧ״̬] {response.StatusCode}");
                log.Info($"[��Ӧ����] {content}");

                if (!response.IsSuccessStatusCode)
                {
                    if (i == maxRetries)
                    {
                        log.Error($"����ʧ�ܣ�״̬�룺{response.StatusCode}����Ӧ���ݣ�{content}");
                        throw new HttpRequestException($"Status: {response.StatusCode}, Content: {content}");
                    }
                    log.Warn($"����ʧ�ܣ����� {initialDelayMs * Math.Pow(2, i)}ms ������");
                    exceptions.Add(new HttpRequestException($"Status: {response.StatusCode}"));
                    continue;
                }

                var result = JsonSerializer.Deserialize<T>(content);
                log.Info($"[�����л����] {JsonSerializer.Serialize(result)}");
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (i == maxRetries)
                {
                    log.Error($"���Դ������þ������һ���쳣��{ex.Message}");
                    throw;
                }
                log.Error($"�����쳣��{ex.Message}������ {initialDelayMs * Math.Pow(2, i)}ms ������");
                exceptions.Add(ex);
            }

            var delayMs = initialDelayMs * Math.Pow(2, i);
            await Task.Delay((int)delayMs);
        }

        throw new AggregateException("�������Զ�ʧ��", exceptions);
    }

    /// <summary>
    /// ����HttpClient������ͷ��Ϣ
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
    /// ��ȡ���úõ�HttpClientʵ��
    /// </summary>
    /// <returns></returns>
    public static HttpClient GetClient() => client;
}
