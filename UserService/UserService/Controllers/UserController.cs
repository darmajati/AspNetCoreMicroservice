using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using UserService.Data;
using UserService.Entities;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserServiceContext _context;
        private readonly IntegrationEventSenderService _integrationEventSenderService;

        public UserController(UserServiceContext context, IntegrationEventSenderService integrationEventSenderService)
        {
            _context = context;
            _integrationEventSenderService = integrationEventSenderService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await _context.User.ToListAsync();
        }

        private void PublishToMessageQueue(string integrationEvent, string eventData)
        {
            var factory = new ConnectionFactory();
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var body = Encoding.UTF8.GetBytes(eventData);
            channel.BasicPublish(exchange: "user",
                                             routingKey: integrationEvent,
                                             basicProperties: null,
                                             body: body);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            using var transaction = _context.Database.BeginTransaction();

            var existingUser = await _context.User.FindAsync(id);
            if (existingUser == null)
            {
                return NotFound();
            }

            existingUser.Name = user.Name;
            existingUser.Mail = user.Mail;
            existingUser.OtherData = user.OtherData;

            existingUser.Version++;

            await _context.SaveChangesAsync();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = existingUser.ID,
                newname = existingUser.Name,
                newVersion = existingUser.Version
            });

            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.update",
                    Data = integrationEventData,
                    Version = existingUser.Version
                });

            _context.SaveChanges();
            transaction.Commit();

            _integrationEventSenderService.StartPublishingOutstandingIntegrationEvents();

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            user.Version = 1;
            using var transaction = _context.Database.BeginTransaction();
            _context.User.Add(user);
            _context.SaveChanges();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                name = user.Name,
                version = user.Version
            });

            _context.IntegrationEventOutbox.Add(
                new IntegrationEvent()
                {
                    Event = "user.add",
                    Data = integrationEventData
                });

            _context.SaveChanges();
            transaction.Commit();

            _integrationEventSenderService.StartPublishingOutstandingIntegrationEvents();

            return CreatedAtAction("GetUser", new { id = user.ID }, user);
        }
    }
}
