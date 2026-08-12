using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Meetings.Models;

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
    public DbSet<Substitution> Substitutions => Set<Substitution>(); // Замещение на период отсутствия (GEN-14)
    
    public DbSet<FileAttachment> FileAttachments { get; set; }
    public DbSet<VndRedaction> VndRedactions { get; set; }
    
    public DbSet<VndRedactionAttachment> VndRedactionAttachments { get; set; }
    
    public DbSet<VndActualizationRequest> VndActualizationRequests { get; set; }
    
    public DbSet<VndDocument> VndDocuments => Set<VndDocument>();
    public DbSet<VndLink> VndLinks => Set<VndLink>();
    
    public DbSet<VndApprovalProcess> VndApprovalProcesses => Set<VndApprovalProcess>();
    public DbSet<VndApprovalStage> VndApprovalStages => Set<VndApprovalStage>();
    
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    // --- Контур СЗ: справочники архивного хранения (SZ-07 / GEN-09) ---
    public DbSet<StorageTerm> StorageTerms => Set<StorageTerm>(); // Справочник: Сроки хранения
    public DbSet<NomenclatureCase> NomenclatureCases => Set<NomenclatureCase>(); // Справочник: Номенклатура дел

    // --- Фундамент документов (GEN-05/09/13): карточка, вложения, связи, нумераторы, аудит ---
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<Numerator> Numerators => Set<Numerator>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // --- Движок согласования (TID-01..14, SZ-01, PRC-08) ---
    public DbSet<RouteInstance> RouteInstances => Set<RouteInstance>();
    public DbSet<RouteStep> RouteSteps => Set<RouteStep>();
    public DbSet<RouteParticipant> RouteParticipants => Set<RouteParticipant>();
    public DbSet<Resolution> Resolutions => Set<Resolution>();
    public DbSet<Remark> Remarks => Set<Remark>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<RouteTemplate> RouteTemplates => Set<RouteTemplate>();
    public DbSet<RouteTemplateStep> RouteTemplateSteps => Set<RouteTemplateStep>();
    public DbSet<RouteTemplateParticipant> RouteTemplateParticipants => Set<RouteTemplateParticipant>();

    // --- ЭП (SIG-01..05) ---
    public DbSet<Signature> Signatures => Set<Signature>();

    // --- Закупки (контур 6 ТЗ): матрица полномочий и её параметры ---
    public DbSet<ProcurementMethod> ProcurementMethods => Set<ProcurementMethod>();
    public DbSet<AuthorityMatrixRule> AuthorityMatrixRules => Set<AuthorityMatrixRule>();
    public DbSet<ProcurementParameter> ProcurementParameters => Set<ProcurementParameter>();
    public DbSet<ProcurementRequest> ProcurementRequests => Set<ProcurementRequest>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<CommercialProposal> CommercialProposals => Set<CommercialProposal>();
    public DbSet<ProcurementProtocol> ProcurementProtocols => Set<ProcurementProtocol>();
    public DbSet<ProtocolRow> ProtocolRows => Set<ProtocolRow>();
    public DbSet<ProtocolSignature> ProtocolSignatures => Set<ProtocolSignature>();
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<CommissionMember> CommissionMembers => Set<CommissionMember>();
    public DbSet<TenderBid> TenderBids => Set<TenderBid>();
    public DbSet<ProcurementContract> ProcurementContracts => Set<ProcurementContract>();
    public DbSet<DeliveryAct> DeliveryActs => Set<DeliveryAct>();
    public DbSet<ProcurementPlan> ProcurementPlans => Set<ProcurementPlan>();
    public DbSet<ProcurementPlanItem> ProcurementPlanItems => Set<ProcurementPlanItem>();
    public DbSet<Guarantee> Guarantees => Set<Guarantee>();
    public DbSet<ProcurementClaim> ProcurementClaims => Set<ProcurementClaim>();

    // --- Служебные записки (контур 4 ТЗ) ---
    public DbSet<SzDocument> SzDocuments => Set<SzDocument>();
    public DbSet<SzKind> SzKinds => Set<SzKind>();
    public DbSet<SzHrKind> SzHrKinds => Set<SzHrKind>();
    public DbSet<SzAssignment> SzAssignments => Set<SzAssignment>();

    // --- Заседания Правления, КПА и комитетов (ТЗ «Исполнение решений КПА, Правления и Комитетов») ---
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<AgendaGuest> AgendaGuests => Set<AgendaGuest>();
    public DbSet<AgendaAssignment> AgendaAssignments => Set<AgendaAssignment>();
    public DbSet<AgendaFile> AgendaFiles => Set<AgendaFile>();


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