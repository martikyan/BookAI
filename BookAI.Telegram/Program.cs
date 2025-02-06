using BookAI.Services;
using BookAI.Telegram;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddServices(builder.Configuration);

var host = builder.Build();

var e = host.Services.GetRequiredService<EpubService>();
var file = File.OpenRead("/users/user/Downloads/The_Hitchhiker_39_s_Guide_to_the_G_-_Douglas_Adams_Non-Illustrated.epub");

await e.SimplifyAsync(file, CancellationToken.None);
host.Run();