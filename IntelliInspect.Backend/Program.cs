using IntelliInspect.Backend.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 5L * 1024 * 1024 * 1024; // 5 GB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5L * 1024 * 1024 * 1024; // 5 GB
});


builder.Services.AddScoped<CsvParserService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowLocalhost4200",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowLocalhost4200");


app.UseAuthorization();
app.UseRouting();

app.MapControllers();

app.Run();
