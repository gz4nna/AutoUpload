namespace AutoUpload.Models
{
    public class UploadState
    {
        public List<string> AllFilesInFolder { get; set; } = new();
        public List<string> FilesToUpload { get; set; } = new();
    }

}
