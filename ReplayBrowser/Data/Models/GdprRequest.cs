using System.ComponentModel.DataAnnotations;

namespace ReplayBrowser.Data.Models;

public class GdprRequest
{
    [Key]
    public Guid Guid { get; set; }
}