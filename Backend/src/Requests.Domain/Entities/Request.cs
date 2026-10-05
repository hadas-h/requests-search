namespace Requests.Domain.Entities;

public class Request
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int OwnerId { get; set; }
    public int? AssignedToUserId { get; set; }
    public RequestStatus Status { get; set; }
    public RequestType RequestType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
