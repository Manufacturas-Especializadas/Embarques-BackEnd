using System;
using System.Collections.Generic;

namespace Embarques.Models;

public partial class Suppliers
{
    public int Id { get; set; }

    public string SupplierName { get; set; }

    public virtual ICollection<Fletes> Fletes { get; set; } = new List<Fletes>();
}