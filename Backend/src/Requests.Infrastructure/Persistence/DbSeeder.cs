using Requests.Domain.Entities;

namespace Requests.Infrastructure.Persistence;

public static class DbSeeder
{
    public static void Seed(RequestsDbContext db)
    {
        if (db.Requests.Any())
            return;

        var random = new Random(42);
        var statuses = Enum.GetValues<RequestStatus>();
        var types = Enum.GetValues<RequestType>();

        var requests = Enumerable.Range(1, 500)
            .Select(i => new Request
            {
                Id = i,
                RequestNumber = $"REQ-{i:000000}",
                CustomerId = (i % 100) + 1,
                OwnerId = (i % 5) + 1,
                AssignedToUserId = i % 7 == 0 ? null : ((i + 1) % 5) + 1,
                Status = statuses[random.Next(statuses.Length)],
                RequestType = types[random.Next(types.Length)],
                CreatedAt = DateTime.UtcNow.AddDays(-(i % 365)),
                UpdatedAt = DateTime.UtcNow.AddDays(-(i % 100))
            })
            .ToList();

        db.Requests.AddRange(requests);
        db.SaveChanges();
    }
}
