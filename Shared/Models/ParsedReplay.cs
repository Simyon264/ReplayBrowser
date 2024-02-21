using System.ComponentModel.DataAnnotations;

namespace Shared.Models;

public class ParsedReplay
{
    [Key]
    public string Name { get; set; }
}