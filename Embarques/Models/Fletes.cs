using System;
using System.Collections.Generic;

namespace Embarques.Models;

public partial class Fletes
{
    public int Id { get; set; }

    public int? IdSupplier { get; set; }

    public int? IdDestination { get; set; }

    public int? HighwayExpenseCost { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? CostOfStay { get; set; }

    public virtual Destination IdDestinationNavigation { get; set; }

    public virtual Suppliers IdSupplierNavigation { get; set; }
}