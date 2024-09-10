using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Linq;
using PostService.Data;
using PostService.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PostService", Version = "v1" });
});

builder.Services.AddDbContext<PostServiceContext>(options =>
    options.UseSqlite("Data Source=post.db"));

var app = builder.Build();

// Ensure the database is created.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PostServiceContext>();
    dbContext.Database.EnsureCreated();
}

// Start listening for integration events.
Task.Run(() => ListenForIntegrationEvents());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

void ListenForIntegrationEvents()
{
    var factory = new ConnectionFactory();
    var connection = factory.CreateConnection();
    var channel = connection.CreateModel();
    var consumer = new EventingBasicConsumer(channel);

    consumer.Received += (model, ea) =>
    {
        var contextOptions = new DbContextOptionsBuilder<PostServiceContext>()
            .UseSqlite(@"Data Source=post.db")
            .Options;
        using var dbContext = new PostServiceContext(contextOptions);

        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [x] Received {0}", message);
        var data = JObject.Parse(message);
        var type = ea.RoutingKey;

        try
        {
            if (type == "user.add")
            {
                if (dbContext.User.Any(a => a.ID == data["id"].Value<int>()))
                {
                    Console.WriteLine("Ignoring old/duplicate entity");
                }
                else
                {
                    dbContext.User.Add(new User()
                    {
                        ID = data["id"].Value<int>(),
                        Name = data["name"].Value<string>(),
                        Version = data["version"].Value<int>()
                    });
                    dbContext.SaveChanges();
                }
            }
            else if (type == "user.update")
            {
                int newVersion = data["version"].Value<int>();
                var user = dbContext.User.First(a => a.ID == data["id"].Value<int>());
                if (user.Version >= newVersion)
                {
                    Console.WriteLine("Ignoring old/duplicate entity");
                }
                else
                {
                    user.Name = data["newname"].Value<string>();
                    user.Version = newVersion;
                    dbContext.SaveChanges();
                }
            }
            channel.BasicAck(ea.DeliveryTag, false); // Acknowledge message
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error processing message: " + ex.Message);
        }
    };

    channel.BasicConsume(queue: "user.postservice",
                         autoAck: false, // Disable automatic acknowledgments
                         consumer: consumer);

    // Keep the listener running.
    Console.WriteLine("Listening for integration events...");
    Console.ReadLine();
}
