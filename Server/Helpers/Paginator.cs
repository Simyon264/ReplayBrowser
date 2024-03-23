namespace Server.Helpers;

/// <summary>
/// A class that helps with pagination.
/// </summary>
public class Paginator
{
    /// <summary>
    /// Returns the number of pages needed to display all items.
    /// </summary>
    public static int GetPageCount(int totalItems, int itemsPerPage)
    {
        return (int) Math.Ceiling((double) totalItems / itemsPerPage);
    }
    
    /// <summary>
    /// Returns the offset for a given page.
    /// </summary>
    public static int GetOffset(int page, int itemsPerPage)
    {
        return page * itemsPerPage;
    }
    
    /// <summary>
    /// Returns the page for a given offset.
    /// </summary>
    public static int GetPage(int offset, int itemsPerPage)
    {
        return offset / itemsPerPage;
    }
}