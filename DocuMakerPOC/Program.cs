using System.Reflection;
using DocuMakerPOC.TransactionScripts;
using Firebase.Database;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

//TransactionScripts
builder.Services.AddTransient<GenerateDocsFromVideoScript>();

//Firebase
builder.Services.AddSingleton<FirebaseClient>(_ =>
    new FirebaseClient(
        builder.Configuration["Firebase:Path"],
        new FirebaseOptions
        {
            AuthTokenAsyncFactory = () => Task.FromResult(builder.Configuration["Firebase:Auth"]) 
        }));

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