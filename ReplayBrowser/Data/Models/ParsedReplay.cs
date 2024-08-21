using System.ComponentModel.DataAnnotations;

namespace ReplayBrowser.Data.Models;

public class ParsedReplay
{
    [Key]
    public required string Name { get; set; }
}