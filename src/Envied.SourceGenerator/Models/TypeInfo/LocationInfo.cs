using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

internal record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{

    public static LocationInfo Empty => new(string.Empty, TextSpan.FromBounds(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)));
    public Location ToLocation()
        => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(SyntaxNode node)
        => From(node.GetLocation());

    public static LocationInfo? From(Location location)
    {
        if (location.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}