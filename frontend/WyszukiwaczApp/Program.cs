using Blazored.LocalStorage;
using WyszukiwaczApp;
using WyszukiwaczApp.Proxies;
using Radzen;
using WyszukiwaczApp.Components;
using WyszukiwaczApp.Other;
using WyszukiwaczApp.Services;
using WyszukiwaczApp.Proxies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new ApiConfig
{
    BaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5012/api/"
});

builder.Services.AddRadzenComponents();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddTransient<AuthTokenHandler>();
builder.Services.AddHttpClient(string.Empty)
    .AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SubscriptionService>();
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

app.MapFallback(context =>
{
    context.Response.Redirect("/404");
    return Task.CompletedTask;
});

app.Run();
