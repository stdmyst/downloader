using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace downloader;

public class Loader(
    string resourceName,
    string initialUri,
    char chunkNumberSeparator,
    IServiceProvider services,
    string resourceExtension = ".ts",
    bool withChunkNumberPadding = false)
    : ILoader
{
    private const ushort MaxRetries = 3;
    private const int DelayMs = 3000;
    
    private int _retries;
    private (DateTime Start, DateTime End) _totalRequestTime;
    private readonly HttpClient _client = services
        .GetRequiredService<IHttpClientFactory>()
        .CreateClient();
    
    private readonly ILogger<Loader> _logger = services.GetService<ILogger<Loader>>()
                                                   ?? new NullLogger<Loader>();
    private Uri _lastUri = new(initialUri);
    private int _currentPartNumber;
    private int _numberOfSymbols;
    
    public async Task DownloadAsync(string folderToSave)
    {
        _totalRequestTime.Start = DateTime.Now;
        
        CreateDirectoryIfNotExists(folderToSave);
        
        var pathToFile = $"{folderToSave}/{resourceName}{resourceExtension}";
        
        await using FileStream destination = new FileStream(pathToFile, FileMode.Create);
        for (; _retries < MaxRetries; _retries++)
        {
            try
            {
                await using var source = await _client.GetStreamAsync(_lastUri);
                await source.CopyToAsync(destination);
                
                _logger.LogInformation(@"Part {partNumber} was downloaded to ""{pathToFile}""",
                    _currentPartNumber, pathToFile);
                
                _lastUri = GetNextChunkUri(_lastUri);
                _retries = 0;
            }
            catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.NotFound)
            {
                LogEndOfOperation(pathToFile);
                return;
            }
            catch (HttpRequestException)
            {
                _retries++;
                await Task.Delay(DelayMs);
            }
        }
    }
    
    private Uri GetNextChunkUri(Uri uri)
    {
        var uriAbsolutePath = uri.AbsolutePath;
        
        var oldPart = GetLastPartOfPath(uriAbsolutePath);
        
        var newPart = IncrementPartNumber(oldPart);
        
        var newUri = GetUriWithReplacedPart(_lastUri, oldPart, newPart);

        return newUri;
    }
    
    private string GetLastPartOfPath(string path) 
        => path.Substring(path.LastIndexOf('/') + 1);
    
    private string IncrementPartNumber(string part)
    {
        var oldNumberString = GetChunkNumberFromString(part);

        if (_numberOfSymbols == 0)
            _numberOfSymbols = oldNumberString.Length;

        var currentNumber = ParseNumberFromString(oldNumberString);
        var nextNumber = currentNumber + 1;
        _currentPartNumber = nextNumber;

        var resultString = nextNumber.ToString();

        if (withChunkNumberPadding)
            resultString = resultString.PadLeft(_numberOfSymbols, '0');

        return part.Replace(oldNumberString + resourceExtension, resultString + resourceExtension);
    }
    
    private string GetChunkNumberFromString(string part) 
        => part.Split('.').First().Split(chunkNumberSeparator).Last();

    private int ParseNumberFromString(string stringNumber)
    {
        if (int.TryParse(stringNumber, out var number))
            return number;
        
        throw new ArgumentException("Invalid number was specified.");
    }
    
    private Uri GetUriWithReplacedPart(Uri uri, string oldPart, string newPart)
        => new(uri.ToString().Replace(oldPart, newPart));
    
    private void CreateDirectoryIfNotExists(string folderToSave)
    {
        if (!Directory.Exists(folderToSave))
            Directory.CreateDirectory(folderToSave);
    }

    private void LogEndOfOperation(string pathToFile)
    {
        _totalRequestTime.End = DateTime.Now;
                    
        var fileInfo = new FileInfo(pathToFile);
        var duration = _totalRequestTime.End - _totalRequestTime.Start;
                    
        _logger.LogInformation(
            @"Resource ""{resourceName}"" was downloaded to ""{pathToFile}"". Resource size = {size} bytes; Total request duration = {duration}",
            resourceName, fileInfo.FullName, fileInfo.Length, duration);
    }
}