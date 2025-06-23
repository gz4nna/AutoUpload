using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoUpload.Models.ResponseModels;

public class MouldSizesCutterPostResponseModel
{
    public int containerNum { get; set; } = 0;
    public string cutterBlankSpec { get; set; } = string.Empty;
    public int cutterType { get; set; } = 0;
    public long fileId { get; set; } = 0;
    public string fileName { get; set; } = string.Empty;
    public string fileUrl { get; set; } = string.Empty;
    public long mouldSizeCutterId { get; set; } = 0;
    public long mouldSizeId { get; set; } = 0;
    public int seq { get; set; } = 0;

}
