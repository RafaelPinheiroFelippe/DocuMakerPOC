using DocuMakerPOC.TransactionScripts;
using Supabase;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Supabase
builder.Services.AddSingleton(_ => new Client(
    configuration["SupabaseUrl"],
    configuration["SupabaseKey"],
    new SupabaseOptions
    {
        AutoRefreshToken = true,
        AutoConnectRealtime = true,
        //SessionHandler = new SupabaseSessionHandler() <-- This must be implemented by the developer
    }));

//TransactionScripts
builder.Services.AddTransient<GenerateC4Script>();

//Redis
builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = "localhost:6379"; });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();