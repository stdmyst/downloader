using System.Text.Json;
using downloader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddHttpClient();
var provider = services.BuildServiceProvider();

var paramsPath = args[0];
var folderToSave = args[1];
var @params = JsonSerializer.Deserialize<List<DownloadParams>>(File.ReadAllText(paramsPath));

if (@params == null)
    return;

foreach (var param in @params)
{
    var loader = new Loader(param.Name, param.Uri, param.ChunkNumberSeparator, provider);
    await loader.DownloadAsync(folderToSave);
}