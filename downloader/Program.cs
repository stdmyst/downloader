using Microsoft.Extensions.Logging;
using downloader;
using Microsoft.Extensions.DependencyInjection;

var client = new HttpClient();
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
var provider = services.BuildServiceProvider();

(string Name, int FinalPartNumber, string Uri) @params = (args[0],  int.Parse(args[1]), args[2]);

var loader = new Loader(client, @params.Name, @params.FinalPartNumber, @params.Uri,  provider);
await loader.DownloadAsync("output");