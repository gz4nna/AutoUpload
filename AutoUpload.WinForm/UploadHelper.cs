using AutoUpload.Models;
using AutoUpload.Models.JsonModels;
using AutoUpload.Models.ResponseModels;
using log4net;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AutoUpload.WinForm;

/// <summary>
/// Provides functionality for managing and uploading files, including preparing files for upload, querying necessary
/// metadata, and handling the upload process.
/// </summary>
/// <remarks>This class implements a singleton pattern to ensure a single instance is used throughout the
/// application. It provides events for updating the UI with upload status and manages the upload of files to a remote
/// server.</remarks>
public class UploadHelper
{
    private static readonly ILog log = LogManager.GetLogger(typeof(UploadHelper));

    public static UploadHelper Instance { get; } = new UploadHelper();

    // 往主窗体上传提示信息
    public delegate void LabelUploadHintPrintDelegate(string message);
    // 用于打印上传提示信息的事件
    public event LabelUploadHintPrintDelegate? LabelUploadHintPrint;

    public delegate void ListBoxPendingUploadDelegate(List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos);
    public event ListBoxPendingUploadDelegate? ListBoxPendingUpload;

    public delegate void ListBoxUploadedDelegate(List<UploadJsonModel>? uploadedFiles);
    public event ListBoxUploadedDelegate? ListBoxUploaded;

    public string watchPath { get; set; } = string.Empty;
    /// <summary>
    /// 上传的目标URL
    /// </summary>
    private string? targetURL;
    /// <summary>
    /// Gets or sets the target URL for the operation.
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
    /// 写入的URL
    /// </summary>
    private string? writeURL;
    /// <summary>
    /// Gets or sets the URL used for write operations.
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
    /// 根据旧型号查找新型号的接口地址
    /// </summary>
    private string? queryURL;
    /// <summary>
    /// Gets or sets the query URL used for accessing the target resource.
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
    /// 第一次上传的时候用来查询 mouldSizeId 的接口地址
    /// </summary>
    private string? listURL;
    /// <summary>
    /// Gets or sets the URL for the list endpoint.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadHelper"/> class.
    /// </summary>
    /// <remarks>This constructor is private to enforce the singleton pattern, preventing direct instantiation
    /// of the <see cref="UploadHelper"/> class.</remarks>
    private UploadHelper()
    {
        // 私有构造函数，确保单例模式
    }

    #region 获取必要参数
    /// <summary>
    /// Prepares a list of files for upload by verifying their existence and gathering necessary metadata.
    /// </summary>
    /// <remarks>This method checks if each file in the pending files list exists in the specified directory.
    /// It retrieves updated part codes and checks if the files need to be uploaded based on existing data. Files that
    /// do not need to be uploaded are logged and skipped. Any errors encountered during the process are logged, and a
    /// failure message is displayed to the user.</remarks>
    /// <param name="responseInfos">A list to store tuples containing file information and metadata required for upload. Each tuple includes the
    /// file name, cutter response model, list response model, upload parameter response model, and cutter request
    /// model.</param>
    /// <returns></returns>
    public List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? PrepareToUpload(
        ConcurrentBag<string>? pendingFiles,
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
    {
        (pendingFiles?.ToList() ?? []).ForEach(async (file) =>
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
                responseInfos?.Add((file, queryData, listData, null, null));
            }
            catch (Exception ex)
            {
                // 这里的异常处理代替了原来的continue
                log.Error("上传过程发生错误", ex);
                LabelUploadHintPrint?.Invoke("上传失败，请查看日志");
            }
        });

        return responseInfos;
    }

    /// <summary>
    /// Asynchronously queries the new parts code corresponding to the specified old parts code.
    /// </summary>
    /// <remarks>This method attempts to retrieve the new parts code by making an HTTP request. If the new
    /// parts code cannot be found, the method returns the provided old parts code. Ensure that the <paramref
    /// name="oldPartsCode"/> is correctly formatted and not <see langword="null"/> if a valid query is
    /// expected.</remarks>
    /// <param name="oldPartsCode">The old parts code to query. Can be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the new parts code if found;
    /// otherwise, returns the original <paramref name="oldPartsCode"/>.</returns>
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
        return partsAttrValueQueryData?.data?.First()?.partsCode ?? oldPartsCode;
    }

    /// <summary>
    /// Queries the mould size identifier based on the specified parts code and specification.
    /// </summary>
    /// <param name="newPartsCode">The parts code used to identify the mould size. Can be <see langword="null"/>.</param>
    /// <param name="specification">The specification used to match the mould size. Can be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see
    /// cref="MouldSizesListResponseModel"/> if the query is successful; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="Exception">Thrown if the query for the mould size identifier fails or if no matching mould size is found.</exception>
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
    /// Queries the list of mould sizes and cutters based on the specified parts code and file name parts.
    /// </summary>
    /// <remarks>This method performs an HTTP request to retrieve data about mould sizes and cutters. If the
    /// query does not return any data, it indicates that the uploaded file is for a new specification and should be
    /// uploaded. If the file name parts contain a number and the file already exists, the upload is skipped.</remarks>
    /// <param name="newPartsCode">The parts code to query. This parameter can be <see langword="null"/>.</param>
    /// <param name="fileNameParts">An array of strings representing parts of the file name. The second element is used to determine the
    /// specification. This parameter can be <see langword="null"/>.</param>
    /// <returns>A <see cref="MouldSizesCutterResponseModel"/> containing the query results, or <see langword="null"/> if no data
    /// is found.</returns>
    /// <exception cref="Exception">Thrown if the query fails or if the file already exists in the specified mould specification.</exception>
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
    /// Asynchronously uploads a list of files to a remote server.
    /// </summary>
    /// <remarks>This method attempts to upload each file in the provided list. If a file upload is
    /// successful, the corresponding response information is updated with the upload response. If the upload fails or
    /// an exception occurs, the file is removed from the list. The method uses an HTTP client with retry logic to
    /// handle transient network errors.</remarks>
    /// <param name="responseInfos">A list of tuples containing file information and associated response models. Each tuple may contain: <list
    /// type="bullet"> <item><description>The file path as a string.</description></item> <item><description>A <see
    /// cref="MouldSizesCutterResponseModel"/> instance.</description></item> <item><description>A <see
    /// cref="MouldSizesListResponseModel"/> instance.</description></item> <item><description>A <see
    /// cref="FileStorageUploadParamResponseModel"/> instance.</description></item> <item><description>A <see
    /// cref="MouldSizesCutterRequestModel"/> instance.</description></item> </list></param>
    /// <returns></returns>
    public async Task<List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>?> UploadFile(
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
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
                    ] = (file, responseInfos.Last()?.Item2, responseInfos.Last()?.Item3, uploadResponse, null);
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
    /// Creates a <see cref="MultipartFormDataContent"/> object for uploading a file with optional metadata.
    /// </summary>
    /// <param name="file">The name of the file to be uploaded. Cannot be null.</param>
    /// <param name="responseInfo">A tuple containing optional metadata related to the file upload. The tuple may include: <list type="bullet">
    /// <item><description>A string representing additional information.</description></item> <item><description>A <see
    /// cref="MouldSizesCutterResponseModel"/> object for response data.</description></item> <item><description>A <see
    /// cref="MouldSizesListResponseModel"/> object for list data.</description></item> <item><description>A <see
    /// cref="FileStorageUploadParamResponseModel"/> object for upload parameters.</description></item>
    /// <item><description>A <see cref="MouldSizesCutterRequestModel"/> object for request data.</description></item>
    /// </list></param>
    /// <returns>A <see cref="MultipartFormDataContent"/> object containing the file and its metadata for upload. Returns <see
    /// langword="null"/> if the file path is empty.</returns>
    /// <exception cref="Exception">Thrown if <paramref name="file"/> is null.</exception>
    private MultipartFormDataContent? CreateUploadMultipartFormContent(
        string? file,
        (string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)? responseInfo)
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
    /// Writes the details of successfully uploaded files to a record and updates the UI lists accordingly.
    /// </summary>
    /// <remarks>This method updates the UI components to reflect the current state of uploaded and pending
    /// files. It also logs the operation and updates a JSON file with the list of uploaded files.</remarks>
    /// <param name="responseInfos">A list of tuples containing information about the files to be processed. Each tuple may contain: <list
    /// type="bullet"> <item><description>The file path as a string.</description></item> <item><description>A <see
    /// cref="MouldSizesCutterResponseModel"/> object.</description></item> <item><description>A <see
    /// cref="MouldSizesListResponseModel"/> object.</description></item> <item><description>A <see
    /// cref="FileStorageUploadParamResponseModel"/> object.</description></item> <item><description>A <see
    /// cref="MouldSizesCutterRequestModel"/> object.</description></item> </list> Only files that have been
    /// successfully uploaded are included in this list.</param>
    /// <returns></returns>
    public async Task WriteFile(
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
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
    /// Asynchronously loads the file path for the monthly upload record.
    /// </summary>
    /// <remarks>This method checks for the existence of a JSON file named according to the current month and
    /// year in the upload directory. If the file does not exist, it creates a new empty JSON file.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file path of the monthly upload
    /// record, or <see langword="null"/> if the operation fails.</returns>
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
    /// Loads the list of uploaded files for the current month from the upload records.
    /// </summary>
    /// <remarks>This method checks for the existence of the upload records directory and creates it if it
    /// does not exist. It reads the upload records for the current month from a JSON file, deserializes them into a
    /// list of  <see cref="UploadJsonModel"/>, and returns this list. If the monthly record file does not exist, it
    /// creates  a new file with an empty list. If reading the file fails, a warning is logged, and an empty list is
    /// returned.</remarks>
    /// <returns>A list of <see cref="UploadJsonModel"/> representing the uploaded files for the current month, or an empty list
    /// if the records cannot be read.</returns>
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
    /// Writes files to a server and records the upload details.
    /// </summary>
    /// <remarks>This method processes each file individually to avoid complete failure in case of an error
    /// with a single file. It updates the pending upload state by removing successfully uploaded files.</remarks>
    /// <param name="uploadedFiles">A list to store details of successfully uploaded files. This list is updated with the file name and upload time
    /// for each successful upload.</param>
    /// <param name="responseInfos">A list of tuples containing information required for file writing and upload tracking. Each tuple includes file
    /// name, response models, and request model.</param>
    /// <returns></returns>
    private static async Task WriteFileAndRecord(
        List<UploadJsonModel>? uploadedFiles,
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
    {
        var pendingState = JsonSerializer.Deserialize<UploadState>(File.ReadAllText(UploadTrackerPaths.PendingPath));
        // 调用写入接口       
        // 所有文件分开写入,避免一次性全部失败
        (responseInfos ?? []).ForEach(async (responseInfo) =>
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
        });

        // 写回 pending.json
        await File.WriteAllTextAsync(UploadTrackerPaths.PendingPath, JsonSerializer.Serialize(pendingState, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Creates a JSON string representing the content required for the file writing interface.
    /// </summary>
    /// <param name="responseInfo">A tuple containing optional data used to construct the JSON content. The tuple elements include: <list
    /// type="bullet"> <item><description>A string representing the cutter specification.</description></item>
    /// <item><description>A <see cref="MouldSizesCutterResponseModel"/> containing cutter response
    /// data.</description></item> <item><description>A <see cref="MouldSizesListResponseModel"/> containing a list of
    /// mould sizes.</description></item> <item><description>A <see cref="FileStorageUploadParamResponseModel"/>
    /// containing file storage parameters.</description></item> <item><description>A <see
    /// cref="MouldSizesCutterRequestModel"/> for cutter request data.</description></item> </list></param>
    /// <returns>A JSON string formatted with indentation, representing the list of <see cref="MouldSizesCutterRequestModel"/>
    /// objects. Returns <see langword="null"/> if the input data is insufficient to construct the JSON content.</returns>
    private static string? CreateJsonContentForFileWritingInterface(
        (string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)? responseInfo)
    {
        // 构造写入接口需要的数据
        // 注意这个接口必须按照列表去输入
        List<MouldSizesCutterRequestModel> mouldSizesCutterRequestModelContentList = [];

#pragma warning disable CS8604 // 可能的 null 引用参数
        mouldSizesCutterRequestModelContentList.Add(new MouldSizesCutterRequestModel()
        {
            // 没有就是null
            containerNum = responseInfo?.Item2?.data?.FirstOrDefault()?.containerNum,
            // 这里放置规格,去掉结尾的字母,表示正反只会出现F和Z
            cutterBlankSpec = responseInfo?.Item1?.Split()?[1].TrimEnd('F', 'Z'),
            // 规格结尾带F的为2,带Z或者不带的都是1
            cutterType = (responseInfo?.Item1?.Split()?[1].ToArray().Last() == 'F') ? 2 : 1,
            // 不要使用默认值,有空直接用空
            fileId = long.TryParse(responseInfo?.Item4?.data?.FirstOrDefault()?.fileId, out _) ? long.Parse(responseInfo?.Item4?.data?.FirstOrDefault()?.fileId) : null,
            // 取数据时候尽可能用靠后的接口的响应,免得中间有修改忘记处理
            fileName = responseInfo?.Item4?.data?.FirstOrDefault()?.fileName,
            // 有空直接输出null
            fileUrl = responseInfo?.Item4?.data?.FirstOrDefault()?.fileUrl,
            // 刀模编号,如果没有就null
            mouldSizeCutterId = long.TryParse(responseInfo?.Item2?.data?.FirstOrDefault()?.mouldSizeCutterId, out _) ?

            long.Parse(responseInfo?.Item2?.data?.FirstOrDefault()?.mouldSizeCutterId) :
            null,
            // 刀模编号从刀模编号列表查询接口中获取
            mouldSizeId = long.Parse(responseInfo?.Item3?.data?.records?.First(record => record?.specification?.Split('.')?[0] == responseInfo?.Item1?.Split()?[1].TrimEnd('F', 'Z'))?.mouldSizeId ?? "0"),
            // 默认使用1
            seq = responseInfo?.Item2?.data?.FirstOrDefault()?.seq ?? 1
        });
#pragma warning restore CS8604 // 可能的 null 引用参数

        var jsonContent = JsonSerializer.Serialize(mouldSizesCutterRequestModelContentList, new JsonSerializerOptions { WriteIndented = true });
        log.Info($"{jsonContent}");

        return jsonContent;
    }

    /// <summary>
    /// Asynchronously writes JSON content to a server endpoint.
    /// </summary>
    /// <param name="jsonContent">The JSON content to be sent to the server. Can be <see langword="null"/>.</param>
    /// <param name="responseInfo">A tuple containing optional response information related to the request. Each item in the tuple can be <see
    /// langword="null"/>.</param>
    /// <returns></returns>
    /// <exception cref="Exception">Thrown if the server response indicates a failure, with details of the error and message.</exception>
    private static async Task WriteFileToServer(
        string? jsonContent,
        (string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)? responseInfo)
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

    private async void UpdateListBoxAndFile(
        List<UploadJsonModel>? uploadedFiles,
        string? monthlyRecord,
        List<(string?, MouldSizesCutterResponseModel?, MouldSizesListResponseModel?, FileStorageUploadParamResponseModel?, MouldSizesCutterRequestModel?)?>? responseInfos)
    {
        // 在列表框中显示已上传文件列表
        ListBoxUploaded?.Invoke(uploadedFiles);
        // 在列表框中删除已上传文件列表
        ListBoxPendingUpload?.Invoke(responseInfos);

        // 写入已上传文件
#pragma warning disable CS8604 // 可能的 null 引用参数
        await File.WriteAllTextAsync(monthlyRecord, JsonSerializer.Serialize(uploadedFiles, new JsonSerializerOptions { WriteIndented = true }));
#pragma warning restore CS8604 // 可能的 null 引用参数
        log.Info($"更新 uploaded.json 文件成功，已添加 {uploadedFiles?.Count} 个文件名");
    }
    #endregion
}
