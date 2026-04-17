using Blazored.LocalStorage;
using Blazored.Toast;
using Radzen;
using WyszukiwaczApp.Components;
using WyszukiwaczApp.Other;
using WyszukiwaczApp.Services;
using WyszukiwaczApp.Proxies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRadzenComponents();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();
builder.Services.AddBlazoredToast();
builder.Services.AddBlazoredLocalStorage();

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OfferService>();
builder.Services.AddScoped<NotificationServiceClient>();
builder.Services.AddScoped<LoginProxy>();
builder.Services.AddScoped<DataProxy>();
builder.Services.AddScoped<NotificationProxy>();
builder.Services.AddScoped<HistoryProxy>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
