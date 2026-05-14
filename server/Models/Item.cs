namespace Server.Models;

public sealed class Item
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ItemStatus Status { get; set; } = ItemStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
