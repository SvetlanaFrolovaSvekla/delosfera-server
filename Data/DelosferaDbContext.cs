using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Vnd.Models;

namespace delosfera_server.Data;

public class DelosferaDbContext : DbContext
{
    public DelosferaDbContext(DbContextOptions<DelosferaDbContext> options) : base(options) { }

    public DbSet<TypeVnd> TypesVnd => Set<TypeVnd>(); // Справочник: типы ВНД
    public DbSet<SecurityLevel> SecurityLevels => Set<SecurityLevel>(); // Справочник: Уровни секретности
    public DbSet<ApprovalBody> ApprovalBodies => Set<ApprovalBody>(); // Справочник:  Органы утверждения
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>(); // Справочник: Структурные подразделения
    public DbSet<Keyword> Keywords => Set<Keyword>(); // Справочник: Ключевые слова
    public DbSet<Position> Positions => Set<Position>(); // Справочник: Должности
    public DbSet<UserGroup> UserGroups => Set<UserGroup>(); // Справочник: Группы пользователей
    
    public DbSet<Rubric> Rubrics => Set<Rubric>(); // Справочник: Рубрикатор
    public DbSet<Role> Roles => Set<Role>(); // Роли пользователей
    public DbSet<User> Users => Set<User>(); // Пользователи
    public DbSet<Token> Tokens => Set<Token>(); // Токены
    
    
    public DbSet<VndDocument> VndDocuments => Set<VndDocument>();
    public DbSet<VndRedaction> VndRedactions => Set<VndRedaction>();
    public DbSet<VndLink> VndLinks => Set<VndLink>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DelosferaDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries<IAuditableEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    var now = DateTime.UtcNow;
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
    }
}