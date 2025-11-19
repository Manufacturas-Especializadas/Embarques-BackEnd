using System;
using System.Collections.Generic;

namespace Embarques.Models;

public partial class Roles
{
    public int Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<Users> Users { get; set; } = new List<Users>();
}