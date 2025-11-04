namespace downloader;

public interface ILoader
{
    Task DownloadAsync(string folderToSave);
}