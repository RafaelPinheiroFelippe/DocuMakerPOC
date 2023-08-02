using DocuMakerPOC.TransactionScripts;
using Firebase.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//TransactionScripts
builder.Services.AddTransient<GenerateC4Script>();

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