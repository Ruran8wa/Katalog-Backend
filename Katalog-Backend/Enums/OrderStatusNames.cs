namespace Katalog_Backend.Enums;

public static class OrderStatusNames
{
    // The domain name of a status (see GLOSSARY.md), used in messages and metric tags.
    // Failed is the stored status for what the domain calls Rejected.
    public static string ToDomainName(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "pending",
        OrderStatus.Confirmed => "confirmed",
        OrderStatus.Failed => "rejected",
        OrderStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
