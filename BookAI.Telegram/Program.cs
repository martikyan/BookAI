using BookAI.Services;
using BookAI.Telegram;using EpubCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddServices(builder.Configuration);
builder.Services.AddLogging(b => b.AddConsole());


var host = builder.Build();

var scope = host.Services.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<EpubService>();
var file = File.OpenRead("/users/user/Downloads/Educated_The_Sunday_Times_and_New_York_Times_bestselling_memoir.epub");

var bookStream = await service.ProcessBookAsync(file, CancellationToken.None);

using var fs = File.OpenWrite("/users/user/Desktop/AI_Educated_The_Sunday_Times_and_New_York_Times_bestselling_memoir.epub");
await bookStream.CopyToAsync(fs);