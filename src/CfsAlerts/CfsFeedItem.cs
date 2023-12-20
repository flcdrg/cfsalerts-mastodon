namespace CfsAlerts;

public record CfsFeedItem(string Id, string Title, string Description, string Link, DateTime PubDate)
{
    public virtual bool Equals(CfsFeedItem? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}