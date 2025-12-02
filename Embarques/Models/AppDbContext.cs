using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Embarques.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Destination> Destination { get; set; }

    public virtual DbSet<Fletes> Fletes { get; set; }

    public virtual DbSet<Roles> Roles { get; set; }

    public virtual DbSet<Suppliers> Suppliers { get; set; }

    public virtual DbSet<Users> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Destination>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Destinat__3214EC071B88EF3A");

            entity.Property(e => e.Cost).HasColumnName("cost");
            entity.Property(e => e.DestinationName)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("destinationName");
        });

        modelBuilder.Entity<Fletes>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Fletes__3214EC07E39F0EC1");

            entity.Property(e => e.CostOfStay).HasColumnName("costOfStay");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("createdAt");
            entity.Property(e => e.HighwayExpenseCost).HasColumnName("highwayExpenseCost");
            entity.Property(e => e.RegistrationDate)
                .HasColumnType("datetime")
                .HasColumnName("registrationDate");

            entity.HasOne(d => d.IdDestinationNavigation).WithMany(p => p.Fletes)
                .HasForeignKey(d => d.IdDestination)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Fletes__IdDestin__534D60F1");

            entity.HasOne(d => d.IdSupplierNavigation).WithMany(p => p.Fletes)
                .HasForeignKey(d => d.IdSupplier)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Fletes__IdSuppli__52593CB8");
        });

        modelBuilder.Entity<Roles>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Roles__3214EC07E8D5A945");

            entity.HasIndex(e => e.Name, "UQ__Roles__72E12F1BC3F63F59").IsUnique();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(40)
                .IsUnicode(false)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Suppliers>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Supplier__3214EC0706E8EFCD");

            entity.Property(e => e.SupplierName)
                .HasMaxLength(200)
                .IsUnicode(false)
                .HasColumnName("supplierName");
        });

        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC076D88EF05");

            entity.HasIndex(e => new { e.Name, e.PayRollNumber }, "UQ_Users_Name_PayRollNumber").IsUnique();

            entity.HasIndex(e => e.PayRollNumber, "UQ__Users__9EAAFED54CCFC90F").IsUnique();

            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("name");
            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnName("passwordHash");
            entity.Property(e => e.PayRollNumber).HasColumnName("payRollNumber");
            entity.Property(e => e.RefreshToken)
                .HasMaxLength(256)
                .HasColumnName("refreshToken");
            entity.Property(e => e.RefreshTokenExpiryTime).HasColumnName("refreshTokenExpiryTime");
            entity.Property(e => e.RolId).HasColumnName("rolId");

            entity.HasOne(d => d.Rol).WithMany(p => p.Users)
                .HasForeignKey(d => d.RolId)
                .HasConstraintName("FK__Users__rolId__5AEE82B9");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}