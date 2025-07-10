using AutoUpload.Models;
using AutoUpload.Models.JsonModels;
using AutoUpload.Models.ResponseModels;
using log4net;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AutoUpload.WinForm;

/// <summary>
/// 提供文件上传、元数据查询、上传记录管理等功能的单例帮助类。
/// 该类负责与主窗体解耦，通过事件委托方式向UI层反馈上传进度、状态和结果。
/// </summary>
public class UploadHelper
{
    #region 变量
    private static readonly ILog log = LogManager.GetLogger(typeof(UploadHelper));

    /// <summary>
    /// 获取 UploadHelper 单例实例。
    /// </summary>
    public static UploadHelper Instance { get; } = new UploadHelper();

    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// 上传提示信息事件委托。
    /// </summary>
    /// <param name="message">要显示在主窗体上的提示文本，通常用于反馈上传进度、错误或结果。</param>
    public delegate void LabelUploadHintPrintDelegate(string message);
    /// <summary>
    /// 上传提示信息事件。
    /// 订阅此事件可在UI层显示上传相关的提示信息。
    /// </summary>
    public event LabelUploadHintPrintDelegate? LabelUploadHintPrint;

    /// <summary>
    /// 待上传文件列表更新事件委托。
    /// </summary>
    /// <param name="responseInfos">
    /// 包含待上传文件及其相关元数据的元组列表。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    public delegate void ListBoxPendingUploadDelegate(List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos);
    /// <summary>
    /// 待上传文件列表更新事件。
    /// 订阅此事件可在UI层同步更新待上传文件列表。
    /// </summary>
    public event ListBoxPendingUploadDelegate? ListBoxPendingUpload;

    /// <summary>
    /// 已上传文件列表更新事件委托。
    /// </summary>
    /// <param name="uploadedFiles">已上传文件的模型列表，每个元素包含文件名和上传时间等信息。</param>
    public delegate void ListBoxUploadedDelegate(List<UploadJsonModel>? uploadedFiles);
    /// <summary>
    /// 已上传文件列表更新事件。
    /// 订阅此事件可在UI层同步更新已上传文件列表。
    /// </summary>
    public event ListBoxUploadedDelegate? ListBoxUploaded;

    private string? watchPath;
    /// <summary>
    /// 获取或设置当前监控的本地文件夹路径。
    /// 若未设置则默认为C盘根目录。
    /// </summary>
    public string? WatchPath
    {
        get
        {
            if (string.IsNullOrEmpty(watchPath)) return "C:\\";// 默认路径为C盘根目录
            return watchPath;
        }
        set => watchPath = value;
    }
    /// <summary>
    /// 上传的目标URL。
    /// </summary>
    private string? targetURL;
    /// <summary>
    /// 获取或设置上传目标URL，必须为有效的绝对URI。
    /// 用于文件实际上传的接口地址。
    /// </summary>
    public string? TargetURL
    {
        get => targetURL;
        set
        {
            if (string.IsNullOrEmpty(value) || !Uri.TryCreate(value, UriKind.Absolute, out _)) throw new Exception("配置错误: 目标URL、写入URL、查询URL或列表URL未设置");

            targetURL = value;
            log.Info($"读取配置文件: <TargetURL> {value}");
        }
    }
    /// <summary>
    /// 写入的URL。
    /// </summary>
    private string? writeURL;
    /// <summary>
    /// 获取或设置写入操作的URL，必须为有效的绝对URI。
    /// 用于写入上传记录或元数据的接口地址。
    /// </summary>
    public string? WriteURL
    {
        get => writeURL;
        set
        {
            if (string.IsNullOrEmpty(value) || !Uri.TryCreate(value, UriKind.Absolute, out _)) throw new Exception("配置错误: 目标URL、写入URL、查询URL或列表URL未设置");
            writeURL = value;
            log.Info($"读取配置文件: <WriteURL> {value}");
        }
    }
    /// <summary>
    /// 查询新型号的接口地址。
    /// </summary>
    private string? queryURL;
    /// <summary>
    /// 获取或设置查询新型号的URL，必须为有效的绝对URI。
    /// 用于根据旧型号查找新型号。
    /// </summary>
    public string? QueryURL
    {
        get => queryURL;
        set
        {
            if (string.IsNullOrEmpty(value) || !Uri.TryCreate(value, UriKind.Absolute, out _)) throw new Exception("配置错误: 目标URL、写入URL、查询URL或列表URL未设置");
            queryURL = value;
            log.Info($"读取配置文件: <QueryURL> {value}");
        }
    }
    /// <summary>
    /// 查询 mouldSizeId 的接口地址。
    /// </summary>
    private string? listURL;
    /// <summary>
    /// 获取或设置 mouldSizeId 查询接口的URL，必须为有效的绝对URI。
    /// 用于首次上传时查询刀模尺寸编号。
    /// </summary>
    public string? ListURL
    {
        get => listURL;
        set
        {
            if (string.IsNullOrEmpty(value) || !Uri.TryCreate(value, UriKind.Absolute, out _)) throw new Exception("配置错误: 目标URL、写入URL、查询URL或列表URL未设置");
            listURL = value;
            log.Info($"读取配置文件: <ListURL> {value}");
        }
    }
    #endregion

    /// <summary>
    /// 私有构造函数，确保单例模式。
    /// </summary>
    private UploadHelper() { }

    #region 获取必要参数
    /// <summary>
    /// 检查待上传文件是否存在，并查询每个文件所需的元数据，准备上传。
    /// </summary>
    /// <param name="pendingFiles">待上传文件名集合（通常为文件名，包含型号和规格）。</param>
    /// <param name="responseInfos">
    /// 用于收集每个文件相关元数据的元组列表。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    /// <returns>异步任务，返回包含所有准备好上传文件及其元数据的元组列表。</returns>
    public async Task<List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>?> PrepareToUpload(
        ConcurrentBag<string>? pendingFiles,
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos)
    {
        foreach (var file in pendingFiles?.ToList() ?? [])
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(Path.Combine(watchPath, file))) throw new Exception($"文件 {file} 不存在");

                // 获取文件名并拆分,文件名中包含型号和规格
                string[]? fileNameParts = Path.GetFileNameWithoutExtension(file)?.Split();

                // 为防止使用协同中不存在的旧型号,首先尝试获取对应的新型号
                // 方法内存在异常处理,无论能否找到都会进行下一步
                var newPartsCode = await QueryNewPartsCodeByOldPartsCode(fileNameParts?[0]);

                // 查询已有列表,判断是否有必要上传
                // 没必要上传的文件会抛出异常,在异常处理里进行记录,并进入下一个文件的处理
                var queryData = await QueryContainedMouldList(newPartsCode, fileNameParts);

                //  获取刀模编号,作为上传的主键,这里使用了新型号去查询,所以可以从结果中取新型号
                var listData = await QueryMouldSizeId(newPartsCode, fileNameParts?[1].TrimEnd('F', 'Z'));

                // 如果是新的编号记下来准备上传
                responseInfos?.Add((file, queryData, listData, null, newPartsCode));
            }
            catch (Exception ex)
            {
                // 这里的异常处理代替了原来的continue
                log.Error("上传过程发生错误", ex);
                LabelUploadHintPrint?.Invoke("上传失败，请查看日志");
            }
        }

        return responseInfos;
    }

    /// <summary>
    /// 异步查询旧型号对应的新型号。
    /// </summary>
    /// <param name="oldPartsCode">旧型号代码。</param>
    /// <returns>异步任务，返回新型号代码；如果查询失败，则返回原始旧型号代码。</returns>
    private async Task<string?> QueryNewPartsCodeByOldPartsCode(string? oldPartsCode)
    {
        var httpClient = HttpRetryHelper.GetClient();

        log.Info($"尝试获取对应的新型号...");
        log.Info($"访问地址: {queryURL}?mbomCode={oldPartsCode}");

        var partsAttrValueQueryData = await HttpRetryHelper.RetryHttpRequestAsync<PartsAttrValuePostResponseModel>(
            async () => await httpClient.PostAsync(
                $"{queryURL}?mbomCode={oldPartsCode}",
                new StringContent(string.Empty)
            ),
            requestInfo: $"获取新型号信息 - mbomCode: {oldPartsCode}"
        );

        // 查到了返回新型号,没查到依然使用旧型号
        return (partsAttrValueQueryData?.data?.Count == 0) ? oldPartsCode : partsAttrValueQueryData?.data?.First()?.partsCode ?? oldPartsCode;
    }

    /// <summary>
    /// 查询刀模尺寸编号。
    /// </summary>
    /// <param name="newPartsCode">新型号代码。</param>
    /// <param name="specification">刀模规格。</param>
    /// <returns>异步任务，返回刀模尺寸编号查询响应模型。</returns>
    /// <exception cref="Exception">如果查询失败或未找到匹配的刀模尺寸编号，则抛出异常。</exception>
    private async Task<MouldSizesListResponseModel?> QueryMouldSizeId(string? newPartsCode, string? specification)
    {
        var httpClient = HttpRetryHelper.GetClient();

        // 获取刀模编号
        log.Info($"获取刀模编号...");
        log.Info($"访问地址: {listURL}?current=1&partsCode={newPartsCode}&size=15");

        var listData = await HttpRetryHelper.RetryHttpRequestAsync<MouldSizesListResponseModel>(
            async () => await httpClient.GetAsync(
                $"{listURL}?current=1&partsCode={newPartsCode}&size=15"
            ),
            requestInfo: $"获取刀模编号 - partsCode: {newPartsCode}"
        );

        // 如果获取刀模编号失败,则跳过上传
        if (listData?.code != "00") throw new Exception($"获取刀模编号失败: {listData?.errors} {listData?.message}");

        // 如果没有找到对应的刀模编号,则跳过上传
        if (listData?.data?.records?.All(record => double.Parse(record?.specification ?? "0") != double.Parse(specification ?? "0")) ?? true)
            throw new Exception($"没有找到对应的刀模编号,请检查型号和规格是否已录入: {newPartsCode} {specification}");

        return listData;
    }

    /// <summary>
    /// 查询型号规格对应的刀模列表。
    /// </summary>
    /// <param name="newPartsCode">新型号代码。</param>
    /// <param name="fileNameParts">文件名拆分后的数组。</param>
    /// <returns>异步任务，返回型号规格查询响应模型。</returns>
    /// <exception cref="Exception">如果查询失败或文件已存在于型号规格中，则抛出异常。</exception>
    private async Task<MouldSizesCutterResponseModel?> QueryContainedMouldList(string? newPartsCode, string[]? fileNameParts)
    {
        // 去除型号后的正反标识
        string? specification = fileNameParts?[1].TrimEnd('F', 'Z');
        var httpClient = HttpRetryHelper.GetClient();

        log.Info($"查询型号规格对应的ID...");
        log.Info($"访问地址: {writeURL}?partsCode={newPartsCode}&specification={specification}");

        var queryData = await HttpRetryHelper.RetryHttpRequestAsync<MouldSizesCutterResponseModel>(
            async () => await httpClient.GetAsync(
                $"{writeURL}?partsCode={newPartsCode}&specification={specification}"
            ),
            requestInfo: $"查询型号规格 - partsCode: {newPartsCode}, specification: {specification}"
        );

        if (queryData?.code != "00") throw new Exception($"查询型号规格失败: {queryData?.error} {queryData?.message}");

        // 不存在任何数据的时候说明上传的肯定是新的型号规格,直接上传
        if (queryData?.data == null || queryData.data.Count == 0) return queryData;

        // 文件名中存在编号,而且已存在,那就不上传了
        if (fileNameParts?.Length != 2 &&
            (queryData?.data?.Select(d => d?.fileName?.Split().Last()).Contains(fileNameParts?.Last()) ?? true))
            throw new Exception($"文件已经存在于型号规格中，跳过上传");

        return queryData;
    }
    #endregion

    #region 上传文件
    /// <summary>
    /// 异步上传文件到远程服务器。
    /// </summary>
    /// <param name="responseInfos">
    /// 待上传文件及其相关元数据的元组列表。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    /// <returns>异步任务，返回上传成功的文件及其元数据的元组列表。</returns>
    public async Task<List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>?> UploadFile(
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos)
    {
        var httpClient = HttpRetryHelper.GetClient();

        // 对合法文件进行上传,用tolist拿一个拷贝出来,避免在上传过程中修改原列表导致异常
        foreach (var responseInfo in responseInfos?.ToList() ?? [])
        {
            var file = responseInfo?.Item1;

            // 调用上传接口
            try
            {
                // 构造上传的表体
                var form = CreateUploadMultipartFormContent(file, responseInfo);

                var uploadResponse = await HttpRetryHelper.RetryHttpRequestAsync<FileStorageUploadParamResponseModel>(
                    async () => await httpClient.PostAsync(targetURL, form),
                    requestInfo: $"上传文件 - {Path.GetFileName(file)}"
                );

                if (uploadResponse?.code == "00")
                {
                    // 响应结果存下来
#pragma warning disable CS8602 // 解引用可能出现空引用。
                    responseInfos[
                        responseInfos.IndexOf(
                            responseInfos.First(response => response?.Item1 == file)
                        )
                    ] = (file, responseInfos.Last()?.Item2, responseInfos.Last()?.Item3, uploadResponse, responseInfos.Last()?.Item5);
#pragma warning restore CS8602 // 解引用可能出现空引用。
                }
                // 如果上传失败,则从responseInfos中移除该文件
                else
                {
                    responseInfos?.Remove(responseInfos.First(response => response?.Item1 == file));
                }
            }
            // 上面是正常情况下状态码显示失败,当发生异常的时候也需要移除文件
            catch (Exception ex)
            {
                switch (ex)
                {
                    case AggregateException aggEx:
                        log.Warn("上传失败,从responseInfos中移除文件", aggEx);
                        break;
                    case HttpRequestException httpEx:
                        log.Error("上传文件时发生网络错误", httpEx);
                        break;
                    default:
                        log.Error("构造上传表体失败", ex);
                        break;
                }
                responseInfos?.Remove(responseInfos.First(response => response?.Item1 == file));
            }
        }

        return responseInfos;
    }

    /// <summary>
    /// 构造上传文件的表单内容。
    /// </summary>
    /// <param name="file">文件路径。</param>
    /// <param name="responseInfo">
    /// 文件相关元数据的元组。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    /// <returns>上传表单内容。</returns>
    /// <exception cref="Exception">如果文件路径为空，则抛出异常。</exception>
    private MultipartFormDataContent? CreateUploadMultipartFormContent(
        string? file,
        (string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)? responseInfo)
    {
        var form = new MultipartFormDataContent();

        // 读取文件内容
        log.Info($"读取文件内容...");
        if (file == null) throw new Exception("文件路径为空");
        var fileContent = new ByteArrayContent(File.ReadAllBytes(Path.Combine(watchPath, file)));
        log.Info($"读取文件内容完成，文件大小: {fileContent.Headers.ContentLength} 字节");

        // 设置请求头
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream");

        // 文件名和文件内容分开,如果是旧型号在上传的时候替换成第一个接口查出来的新型号                
        form.Add(
            fileContent,
            "files",
            (responseInfo?.Item2?.code != "00") ?
            // 如果没有查到数据并且可以上传,则直接使用原文件名
            Path.GetFileName(file) :
            // 如果查到了数据,直接使用新型号的文件名
            Path.GetFileName(file).Replace(
                Path.GetFileName(file).Split().First(),
                responseInfo?.Item3?.data?.records?[0].mouldName
            )
        );
        return form;
    }

    #endregion

    #region 写入服务器
    /// <summary>
    /// 写入上传成功的文件记录，并更新UI列表。
    /// </summary>
    /// <param name="responseInfos">
    /// 上传成功的文件及其相关元数据的元组列表。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    /// <returns>异步任务。</returns>
    public async Task WriteFile(
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos)
    {
        // 对上传成功的文件进行写入操作
        // 上传失败的前面已经移除了,写入时能看到的都是上传成功的
        try
        {
            // 留下来的都是上传成功的文件
            if (responseInfos?.Count == 0) throw new Exception("没有上传成功的文件!跳过写入!");

            // 获取当前月份的上传记录文件名
            var monthlyRecord = await LoadMonthlyRecordName();

            // 加载已上传的文件列表
            var uploadedFiles = LoadUploadFiles(monthlyRecord);

            // 写入并记录
            await WriteFileAndRecord(uploadedFiles, responseInfos);

            // 更新列表框和文件
            UpdateListBoxAndFile(uploadedFiles, monthlyRecord, responseInfos);
        }
        catch (Exception ex)
        {
            log.Error("处理上传结果时发生错误", ex);
            LabelUploadHintPrint?.Invoke("处理上传结果时发生错误");
            return;
        }
    }

    /// <summary>
    /// 异步加载当前月份的上传记录文件路径。
    /// </summary>
    /// <returns>异步任务，返回上传记录文件路径。</returns>
    private static async Task<string?> LoadMonthlyRecordName()
    {
        // 新要求,需要将上传记录按照月份保存
        // 按照当前上传的月份去选文件
        var monthlyRecord = Path.Combine(UploadTrackerPaths.UploadFolder, $"UploadRecords_{DateTime.Now:yyyyMM}.json");
        if (!File.Exists(monthlyRecord))
        {
            // 如果不存在就创建一个新的
            await File.WriteAllTextAsync(monthlyRecord, "[]");
            log.Info($"创建新的上传记录文件: {monthlyRecord}");
        }

        return monthlyRecord;
    }

    /// <summary>
    /// 加载已上传文件列表。
    /// </summary>
    /// <param name="monthlyRecord">当前月份的上传记录文件路径。</param>
    /// <returns>已上传文件的模型列表。</returns>
    private static List<UploadJsonModel>? LoadUploadFiles(string? monthlyRecord)
    {
        // 更新 uploaded.json 文件
        // 将 responseInfos 中的文件名添加到 uploaded.json 中
        var uploadedFiles = new List<UploadJsonModel>();

        if (!Directory.Exists(UploadTrackerPaths.UploadFolder))
        {
            log.Info($"不存在上传记录文件夹,创建: {UploadTrackerPaths.UploadFolder}");
            Directory.CreateDirectory(UploadTrackerPaths.UploadFolder);
        }

        try
        {
            // 读出这个月的上传记录
#pragma warning disable CS8604 // 可能的 null 引用参数
            var uploadedJson = File.ReadAllText(monthlyRecord);
#pragma warning restore CS8604 // 可能的 null 引用参数
            // 反序列化为 List<UploadJsonModel>
            uploadedFiles = JsonSerializer.Deserialize<List<UploadJsonModel>>(uploadedJson);
        }
        catch
        {
            log.Warn("uploaded.json 读取失败，已重置为空");
        }

        return uploadedFiles;
    }

    /// <summary>
    /// 写入文件记录并更新上传状态。
    /// </summary>
    /// <param name="uploadedFiles">已上传文件的模型列表。</param>
    /// <param name="responseInfos">
    /// 上传成功的文件及其相关元数据的元组列表。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    /// <returns>异步任务。</returns>
    private static async Task WriteFileAndRecord(
        List<UploadJsonModel>? uploadedFiles,
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos)
    {
        var pendingState = JsonSerializer.Deserialize<UploadState>(File.ReadAllText(UploadTrackerPaths.PendingPath));
        // 调用写入接口       
        // 所有文件分开写入,避免一次性全部失败
        foreach (var responseInfo in responseInfos ?? [])
        {
            // 构造写入接口需要的数据
            var jsonContent = CreateJsonContentForFileWritingInterface(responseInfo);

            // 开始写入
            await WriteFileToServer(jsonContent, responseInfo);

            // 添加这次上传成功的记录,文件名为空的情况不可能出现
            uploadedFiles?.Add(new()
            {
                // 这里使用原名
                fileName = Path.GetFileName(responseInfo?.Item1) ?? "",
                uploadTime = responseInfo?.Item4?.timestamp ?? DateTime.Now.ToString()
            });

            // 删除 pending.json 中上传成功的文件
            pendingState?.FilesToUpload.Remove(Path.GetFileName(responseInfo?.Item1) ?? string.Empty);
        }

        // 写回 pending.json
        await File.WriteAllTextAsync(UploadTrackerPaths.PendingPath, JsonSerializer.Serialize(pendingState, jsonSerializerOptions));
    }

    /// <summary>
    /// 构造写入接口的JSON内容。
    /// </summary>
    /// <param name="responseInfo">
    /// 文件相关元数据的元组。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    /// <returns>写入接口的JSON内容。</returns>
    private static string? CreateJsonContentForFileWritingInterface(
        (string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)? responseInfo)
    {
        // 构造写入接口需要的数据
        // 注意这个接口必须按照列表去输入
        List<MouldSizesCutterRequestModel> mouldSizesCutterRequestModelContentList = [];

#pragma warning disable CS8604 // 可能的 null 引用参数
        // 柜号,没有就是null
        var containerNum = responseInfo?.Item2?.data?.FirstOrDefault()?.containerNum;
        // 刀模规格,去掉结尾的字母,表示正反只会出现F和Z
        var cutterBlankSpec = Path.GetFileNameWithoutExtension(responseInfo?.Item1)?.Split()?[1].TrimEnd('F', 'Z');
        // 刀模类型,规格结尾带F的为2,带Z或者不带的都是1
        var cutterType = (Path.GetFileNameWithoutExtension(responseInfo?.Item1)?.Split()?[1].ToArray().Last() == 'F') ? 2 : 1;
        // 文件id,不要使用默认值,有空直接用空
        long? fileId = long.TryParse(responseInfo?.Item4?.data?.FirstOrDefault()?.fileId, out _) ?
            long.Parse(responseInfo?.Item4?.data?.FirstOrDefault()?.fileId) :
            null;
        // 文件名,将原始文件名中的型号替换为新型号
        var fileName = Path.GetFileName(responseInfo?.Item1)?.Replace(Path.GetFileNameWithoutExtension(responseInfo?.Item1)?.Split()?[0], responseInfo?.Item5);
        // 文件在文件服务器上的地址,有空直接输出null
        var fileUrl = responseInfo?.Item4?.data?.FirstOrDefault()?.fileUrl;
        // 刀模编号,如果没有就null
        long? mouldSizeCutterId = long.TryParse(responseInfo?.Item2?.data?.FirstOrDefault()?.mouldSizeCutterId, out _) ?
            long.Parse(responseInfo?.Item2?.data?.FirstOrDefault()?.mouldSizeCutterId) :
            null;
        // 刀模尺寸编号从刀模编号列表查询接口中获取
        var mouldSizeId = long.Parse(responseInfo?.Item3?.data?.records?
            .First(record => record?.specification?.Split('.')?[0] == Path.GetFileNameWithoutExtension(responseInfo?.Item1)?.Split()?[1].TrimEnd('F', 'Z'))?.mouldSizeId ?? "0");
        // 默认使用1
        var seq = responseInfo?.Item2?.data?.FirstOrDefault()?.seq ?? 1;

        mouldSizesCutterRequestModelContentList.Add(new MouldSizesCutterRequestModel()
        {
            containerNum = containerNum,
            cutterBlankSpec = cutterBlankSpec,
            cutterType = cutterType,
            fileId = fileId,
            fileName = fileName,
            fileUrl = fileUrl,
            mouldSizeCutterId = mouldSizeCutterId,
            mouldSizeId = mouldSizeId,
            seq = seq
        });
#pragma warning restore CS8604 // 可能的 null 引用参数

        var jsonContent = JsonSerializer.Serialize(mouldSizesCutterRequestModelContentList, jsonSerializerOptions);
        log.Info($"{jsonContent}");

        return jsonContent;
    }

    /// <summary>
    /// 异步写入文件到服务器。
    /// </summary>
    /// <param name="jsonContent">写入接口的JSON内容。</param>
    /// <param name="responseInfo">
    /// 文件相关元数据的元组。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    /// <returns>异步任务。</returns>
    /// <exception cref="Exception">如果写入失败，则抛出异常。</exception>
    private static async Task WriteFileToServer(
        string? jsonContent,
        (string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)? responseInfo)
    {
        var writeURL = Properties.Settings.Default.WriteURL;
        var httpClient = HttpRetryHelper.GetClient();

#pragma warning disable CS8604 // 可能的 null 引用参数
        HttpContent httpContent = new StringContent(jsonContent);
#pragma warning restore CS8604 // 可能的 null 引用参数
        httpContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");

        var writeResponse = await HttpRetryHelper.RetryHttpRequestAsync<MouldSizesCutterPostResponseModel>(
                async () => await httpClient.PostAsync(writeURL, httpContent),
                requestInfo: $"写入数据 - {responseInfo?.Item1}"
            );

        if (writeResponse?.code != "00") throw new Exception($"写入数据失败: {writeResponse?.error} {writeResponse?.message}");
    }

    /// <summary>
    /// 更新UI列表并写入已上传文件记录。
    /// </summary>
    /// <param name="uploadedFiles">已上传文件的模型列表。</param>
    /// <param name="monthlyRecord">当前月份的上传记录文件路径。</param>
    /// <param name="responseInfos">
    /// 上传成功的文件及其相关元数据的元组列表。
    /// <list type="bullet">
    /// <item><description>Item1: <c>string?</c> - 文件名（含扩展名）。</description></item>
    /// <item><description>Item2: <c>MouldSizesCutterResponseModel?</c> - 型号规格查询响应对象。</description></item>
    /// <item><description>Item3: <c>MouldSizesListResponseModel?</c> - 刀模编号查询响应对象。</description></item>
    /// <item><description>Item4: <c>FileStorageUploadParamResponseModel?</c> - 上传参数响应对象。</description></item>
    /// <item><description>Item5: <c>string?</c> - 新型号（如有）。</description></item>
    /// </list>
    /// </param>
    private async void UpdateListBoxAndFile(
        List<UploadJsonModel>? uploadedFiles,
        string? monthlyRecord,
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, string?)?>? responseInfos)
    {
        // 在列表框中显示已上传文件列表
        ListBoxUploaded?.Invoke(uploadedFiles);
        // 在列表框中删除已上传文件列表
        ListBoxPendingUpload?.Invoke(responseInfos);

        // 写入已上传文件
#pragma warning disable CS8604 // 可能的 null 引用参数
        await File.WriteAllTextAsync(monthlyRecord, JsonSerializer.Serialize(uploadedFiles, jsonSerializerOptions));
#pragma warning restore CS8604 // 可能的 null 引用参数
        log.Info($"更新 uploaded.json 文件成功，已添加 {uploadedFiles?.Count} 个文件名");
    }
    #endregion
}
