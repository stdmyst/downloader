using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace downloader;

public class Loader : ILoader
{
    private const char NumberSeparator = '_';
    private const string ResourceExtension = ".ts";

    private int _retries = 0;
    private readonly HttpClient _client;
    private readonly ILogger<Loader> _logger;
    private readonly string _resourceName;
    private readonly int _finalPartNumber;
    private Uri _lastUri;
    private int _currentPartNumber;

    private bool IsLastPart => _currentPartNumber == _finalPartNumber;
    
    public Loader(HttpClient client, string resourceName, int finalPart, string initialUri, IServiceProvider services)
    {
        _client = client;
        _logger = services.GetService<ILogger<Loader>>() ?? new NullLogger<Loader>();
        _resourceName = resourceName;
        _finalPartNumber = finalPart;
        _lastUri = new Uri(initialUri);
        _currentPartNumber = GetPartNumberFromUri(_lastUri);
    }

    public async Task DownloadAsync(string folderToSave)
    {
        if (!Directory.Exists(folderToSave))
            Directory.CreateDirectory(folderToSave);
        
        var pathToFile = $"{folderToSave}/{_resourceName}{ResourceExtension}";
        
        await using FileStream destination = new FileStream(pathToFile, FileMode.OpenOrCreate);
        while (true)
        {
            try
            {
                await using var source = await _client.GetStreamAsync(_lastUri);
                await source.CopyToAsync(destination);
                
                _logger.LogInformation(@"Part {_currentPartNumber} was downloaded to ""{pathToFile}""", _currentPartNumber, pathToFile);

                if (IsLastPart)
                {
                    _logger.LogInformation(@"Resource ""{_resourceName}"" was downloaded to ""{pathToFile}""", _resourceName, pathToFile);
                    return;
                }
            
                SetNextPartOfResource();
                
                _retries = 0;
            }
            catch (HttpRequestException)
            {
                if (_retries >= 3)
                    throw;

                _retries++;
                
                await Task.Delay(3000);
            }
        }
    }

    private void SetNextPartOfResource()
    {
        var leftPart = _lastUri.AbsolutePath;
        var currentPart = leftPart.Substring(leftPart.LastIndexOf('/') + 1);
        
        _lastUri = GetIncrementedUri(currentPart);
    }

    private Uri GetIncrementedUri(string currentPart)
    {
        var nextPart = IncrementPartNumber(currentPart);
        var newUri = GetUriWithReplacedPart(_lastUri, currentPart, nextPart);
        
        return newUri;
    }

    private string IncrementPartNumber(string part)
    {
        var oldNumberString = ExtractNumberFromString(part);
        var numberOfSymbols = oldNumberString.Length;
        
        var currentNumber = ParseNumberFromString(oldNumberString);
        var nextNumber = currentNumber + 1;
        _currentPartNumber = nextNumber;
        
        var resultString = nextNumber.ToString().PadLeft(numberOfSymbols, '0');
        
        return part.Replace(oldNumberString + ResourceExtension, resultString + ResourceExtension);
    }

    private int GetPartNumberFromUri(Uri uri)
    {
        var path = uri.AbsolutePath;
        var partNumberString = GetLastPartOfPath(path);
        partNumberString = ExtractNumberFromString(partNumberString);
        
        var partNumber = ParseNumberFromString(partNumberString);

        return partNumber;
    }

    private string GetLastPartOfPath(string path)
        => path.Substring(path.LastIndexOf('/') + 1);

    private string ExtractNumberFromString(string stringPart) 
        => stringPart.Split('.').First().Split(NumberSeparator).Last();

    private int ParseNumberFromString(string stringNumber)
    {
        if (int.TryParse(stringNumber, out var number))
            return number;
        
        throw new ArgumentException("Invalid number was specified.");
    }
    
    private Uri GetUriWithReplacedPart(Uri uri, string oldPart, string newPart)
        => new(uri.ToString().Replace(oldPart, newPart));
}