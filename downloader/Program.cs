using downloader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddHttpClient();
var provider = services.BuildServiceProvider();

(string Name, int FinalPartNumber, string Uri) @params = (args[0],  int.Parse(args[1]), args[2]);

var loader = new Loader(@params.Name, @params.FinalPartNumber, @params.Uri, provider);
await loader.DownloadAsync("output");