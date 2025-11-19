using System;
using System.Collections.Generic;

namespace Embarques.Models;

public partial class Destination
{
    public int Id { get; set; }

    public string DestinationName { get; set; }

    public int? Cost { get; set; }

    public virtual ICollection<Fletes> Fletes { get; set; } = new List<Fletes>();
}