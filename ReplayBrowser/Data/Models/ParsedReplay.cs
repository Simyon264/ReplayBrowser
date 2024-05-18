using System.ComponentModel.DataAnnotations;

namespace ReplayBrowser.Data.Models;

public class ParsedReplay
{
    [Key]
    public string Name { get; set; }
}