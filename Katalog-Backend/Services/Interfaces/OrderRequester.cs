namespace Katalog_Backend.Services.Interfaces;

// Who is asking: customers only see their own orders, admins see all.
public sealed record OrderRequester(string UserId, bool IsAdmin);
