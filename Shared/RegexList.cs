using System.Text.RegularExpressions;

namespace Shared;

public static class RegexList
{
    public static Regex ReplayRegex = new(@"^[a-zA-Z0-9-]+-(\d{4}_\d{2}_\d{2}-\d{2}_\d{2})-round_\d+\.zip$");
    
    public static Regex ServerNameRegex = new(@"(^[a-zA-Z0-9-]+)-\d{4}_\d{2}_\d{2}-\d{2}_\d{2}-round_\d+\.zip$");
}