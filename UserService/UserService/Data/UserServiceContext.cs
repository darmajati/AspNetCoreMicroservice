using Microsoft.EntityFrameworkCore;

namespace UserService.Data
{
    public class UserServiceContext : DbContext
    {
        public UserServiceContext(DbContextOptions<UserServiceContext> options)
            : base(options)
        {
        }

        public DbSet<UserService.Entities.User> User { get; set; }

        public DbSet<UserService.Entities.IntegrationEvent> IntegrationEventOutbox { get; set; }
    }
}
