using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using UserService;
using UserService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UserService", Version = "v1" });
});

// Configure Entity Framework to use SQLite
builder.Services.AddDbContext<UserServiceContext>(options =>
    options.UseSqlite("Data Source=user.db"));

// Add IntegrationEventSenderService as a Singleton and HostedService
builder.Services.AddSingleton<IntegrationEventSenderService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<IntegrationEventSenderService>());
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
