namespace Requests.Application.Requests;

public sealed record RequestDto(
    int Id,
    string RequestNumber,
    int CustomerId,
    int OwnerId,
    int? AssignedToUserId,
    EnumRef Status,
    EnumRef RequestType,
    DateTime CreatedAt);
