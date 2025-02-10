using BookAI.Services;
using BookAI.Telegram;using EpubCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddServices(builder.Configuration);
builder.Services.AddLogging(b => b.AddConsole());


var host = builder.Build();

var scope = host.Services.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<EpubService>();
var file = File.OpenRead("/users/user/Downloads/Behave_The_Biology_of_Humans_at_Our_Best_and_Worst_by_Robert_M_Sapolsky.epub");

var bookStream = await service.ProcessBookAsync(file, CancellationToken.None);

using var fs = File.OpenWrite("/users/user/Desktop/fix.epub");
await bookStream.CopyToAsync(fs);