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
using delosfera_server.Modules.Integrations.Mail;
using delosfera_server.Modules.Search.Models;
using delosfera_server.Modules.Integrations.Directory;

namespace delosfera_server.Data;

public class DelosferaDbContext : DbContext
{
    /// <summary>
    /// Кто сейчас работает — нужен журналу изменений настроек. Необязательная
    /// зависимость: контекст поднимают и вне запроса (миграции, фоновые службы),
    /// и требовать там текущего пользователя нечего.
    /// </summary>
    private readonly Common.Services.Authorization.ICurrentUserService? _currentUser;

    public DelosferaDbContext(DbContextOptions<DelosferaDbContext> options) : base(options) { }

    public DelosferaDbContext(
        DbContextOptions<DelosferaDbContext> options,
        Common.Services.Authorization.ICurrentUserService currentUser) : base(options) =>
        _currentUser = currentUser;

    /// <summary>Журнал изменений справочников и настроек — пишется самим контекстом.</summary>
    public DbSet<delosfera_server.Modules.Settings.Models.SettingsChange> SettingsChanges =>
        Set<delosfera_server.Modules.Settings.Models.SettingsChange>();

    public DbSet<TypeVnd> TypesVnd => Set<TypeVnd>(); // Справочник: типы ВНД
    public DbSet<SecurityLevel> SecurityLevels => Set<SecurityLevel>(); // Справочник: Уровни секретности
    public DbSet<ApprovalBody> ApprovalBodies => Set<ApprovalBody>(); // Справочник:  Органы утверждения
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>(); // Справочник: Структурные подразделения
    public DbSet<Keyword> Keywords => Set<Keyword>(); // Справочник: Ключевые слова
    public DbSet<Position> Positions => Set<Position>(); // Справочник: Должности
    public DbSet<OrganizationUnitHistory> OrganizationUnitHistory => Set<OrganizationUnitHistory>(); // Историчность оргструктуры (GEN-08)
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

    // --- Годовой план актуализации ВНД (PLN-01..07) ---
    public DbSet<ActualizationPlan> ActualizationPlans => Set<ActualizationPlan>();
    public DbSet<ActualizationPlanItem> ActualizationPlanItems => Set<ActualizationPlanItem>();
    public DbSet<PlanItemEvent> PlanItemEvents => Set<PlanItemEvent>();
    public DbSet<ActualizationSettings> ActualizationSettings => Set<ActualizationSettings>();
    public DbSet<VndApprovalStage> VndApprovalStages => Set<VndApprovalStage>();
    public DbSet<VndApprovalStageAttachment> VndApprovalStageAttachments => Set<VndApprovalStageAttachment>();
    public DbSet<VndRepeatCommentAttachment> VndRepeatCommentAttachments => Set<VndRepeatCommentAttachment>();

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

    // --- Администрируемость: типы документов, справочники, представления (GEN-06/07/10) ---
    public DbSet<DocumentTypeDefinition> DocumentTypeDefinitions => Set<DocumentTypeDefinition>();
    public DbSet<DocumentTypeField> DocumentTypeFields => Set<DocumentTypeField>();
    public DbSet<ListView> ListViews => Set<ListView>();
    public DbSet<CustomDictionary> CustomDictionaries => Set<CustomDictionary>();
    public DbSet<CustomDictionaryItem> CustomDictionaryItems => Set<CustomDictionaryItem>();
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
    public DbSet<SimpleSignatureRegulation> SimpleSignatureRegulations => Set<SimpleSignatureRegulation>();
    public DbSet<SimpleSignatureConsent> SimpleSignatureConsents => Set<SimpleSignatureConsent>();

    /// <summary>Кому из удостоверяющих центров доверяет банк — список ведёт администратор.</summary>
    public DbSet<TrustedCertificateAuthority> TrustedCertificateAuthorities => Set<TrustedCertificateAuthority>();

    /// <summary>Каким сертификатом подписывает каждый сотрудник.</summary>
    public DbSet<UserCertificate> UserCertificates => Set<UserCertificate>();

    /// <summary>Служба меток времени и проверка отзыва — одна запись на систему.</summary>
    public DbSet<SigningSettings> SigningSettings => Set<SigningSettings>();

    /// <summary>Инструкции по работе с системой — тексты правит администратор (KB-01..03).</summary>
    public DbSet<delosfera_server.Modules.Help.Models.HelpArticle> HelpArticles =>
        Set<delosfera_server.Modules.Help.Models.HelpArticle>();

    /// <summary>Снимки экрана в статьях инструкции — связь для проверки доступа.</summary>
    public DbSet<delosfera_server.Modules.Help.Models.HelpArticleImage> HelpArticleImages =>
        Set<delosfera_server.Modules.Help.Models.HelpArticleImage>();

    // --- Ознакомление с документами (Б-19) ---
    public DbSet<AcknowledgementSheet> AcknowledgementSheets => Set<AcknowledgementSheet>();
    public DbSet<AcknowledgementEntry> AcknowledgementEntries => Set<AcknowledgementEntry>();

    /// <summary>
    /// Настройки почтовых уведомлений. В базе, а не в конфигурации сервера:
    /// выключить рассылку должен уметь администратор, не трогая сервер.
    /// </summary>
    public DbSet<delosfera_server.Modules.Integrations.Mail.MailSettings> MailSettings =>
        Set<delosfera_server.Modules.Integrations.Mail.MailSettings>();

    /// <summary>
    /// Представления журналов: наборы колонок в списках документов.
    /// Требование банка — настраивать их без программирования.
    /// </summary>
    public DbSet<delosfera_server.Modules.Documents.Models.JournalView> JournalViews =>
        Set<delosfera_server.Modules.Documents.Models.JournalView>();

    /// <summary>
    /// Оргструктура из портала: настройки связи и история проходов.
    /// Сама структура ложится в справочник подразделений, отдельной копии нет —
    /// иначе у банка стало бы два дерева, расходящихся со временем.
    /// </summary>
    public DbSet<delosfera_server.Modules.Integrations.OrgStructure.OrgStructureSettings> OrgStructureSettings =>
        Set<delosfera_server.Modules.Integrations.OrgStructure.OrgStructureSettings>();

    public DbSet<delosfera_server.Modules.Integrations.OrgStructure.OrgSyncRun> OrgSyncRuns =>
        Set<delosfera_server.Modules.Integrations.OrgStructure.OrgSyncRun>();

    /// <summary>
    /// Доверенности: кто, кому, на что и на какой срок. Отвечает на вопрос
    /// «вправе ли этот человек подписать вот это сегодня».
    /// </summary>
    public DbSet<delosfera_server.Modules.PowerOfAttorney.Models.PowerOfAttorney> PowersOfAttorney =>
        Set<delosfera_server.Modules.PowerOfAttorney.Models.PowerOfAttorney>();

    public DbSet<delosfera_server.Modules.PowerOfAttorney.Models.PoaFile> PoaFiles =>
        Set<delosfera_server.Modules.PowerOfAttorney.Models.PoaFile>();

    /// <summary>
    /// Регулярные обязательства: заседания комитетов, отчёты, пересмотр политик,
    /// график сдачи в НБКР. Регулятор мыслит периодичностью — здесь она и живёт.
    /// </summary>
    public DbSet<delosfera_server.Modules.Obligations.Models.RecurringObligation> RecurringObligations =>
        Set<delosfera_server.Modules.Obligations.Models.RecurringObligation>();

    public DbSet<delosfera_server.Modules.Obligations.Models.ObligationPeriod> ObligationPeriods =>
        Set<delosfera_server.Modules.Obligations.Models.ObligationPeriod>();

    /// <summary>
    /// Книга регистрации корреспонденции: входящие и исходящие письма, запросы
    /// регулятора, обращения клиентов, запросы по счетам.
    /// </summary>
    public DbSet<delosfera_server.Modules.Correspondence.Models.CorrespondenceLetter> CorrespondenceLetters =>
        Set<delosfera_server.Modules.Correspondence.Models.CorrespondenceLetter>();

    public DbSet<delosfera_server.Modules.Correspondence.Models.Correspondent> Correspondents =>
        Set<delosfera_server.Modules.Correspondence.Models.Correspondent>();

    public DbSet<delosfera_server.Modules.Correspondence.Models.LetterFile> LetterFiles =>
        Set<delosfera_server.Modules.Correspondence.Models.LetterFile>();

    /// <summary>
    /// Приказы по личному составу. Книга ведётся отдельно от приказов по основной
    /// деятельности: срок хранения у неё особый.
    /// </summary>
    public DbSet<delosfera_server.Modules.Hr.Models.HrOrder> HrOrders =>
        Set<delosfera_server.Modules.Hr.Models.HrOrder>();

    public DbSet<delosfera_server.Modules.Hr.Models.HrOrderEmployee> HrOrderEmployees =>
        Set<delosfera_server.Modules.Hr.Models.HrOrderEmployee>();

    /// <summary>Пожелания и замечания сотрудников с экранов системы — обкатка подразделениями.</summary>
    public DbSet<delosfera_server.Modules.Feedback.Models.FeedbackItem> FeedbackItems =>
        Set<delosfera_server.Modules.Feedback.Models.FeedbackItem>();

    /// <summary>
    /// Заходы на экраны. Растёт быстрее всех прочих таблиц, чистится фоновой службой
    /// PageVisitCleanupWorker по настройке Usage:RetentionDays.
    /// </summary>
    public DbSet<delosfera_server.Modules.Feedback.Models.PageVisit> PageVisits =>
        Set<delosfera_server.Modules.Feedback.Models.PageVisit>();

    // --- Закупки (контур 6 ТЗ): матрица полномочий и её параметры ---
    public DbSet<ProcurementMethod> ProcurementMethods => Set<ProcurementMethod>();
    public DbSet<AuthorityMatrixRule> AuthorityMatrixRules => Set<AuthorityMatrixRule>();
    public DbSet<ProcurementParameter> ProcurementParameters => Set<ProcurementParameter>();
    public DbSet<ProcurementRequest> ProcurementRequests => Set<ProcurementRequest>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<CommercialProposal> CommercialProposals => Set<CommercialProposal>();
    public DbSet<ProposalFile> ProposalFiles => Set<ProposalFile>();
    public DbSet<ProcurementProtocol> ProcurementProtocols => Set<ProcurementProtocol>();
    public DbSet<ProtocolRow> ProtocolRows => Set<ProtocolRow>();
    public DbSet<ProtocolSignature> ProtocolSignatures => Set<ProtocolSignature>();
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<CommissionMember> CommissionMembers => Set<CommissionMember>();
    public DbSet<TenderBid> TenderBids => Set<TenderBid>();
    public DbSet<CommissionVote> CommissionVotes => Set<CommissionVote>();
    public DbSet<TenderMeetingChange> TenderMeetingChanges => Set<TenderMeetingChange>();
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

    /// <summary>Сотрудники, которых касается кадровая записка: их может быть несколько.</summary>
    public DbSet<SzEmployee> SzEmployees => Set<SzEmployee>();
    public DbSet<SzAssignment> SzAssignments => Set<SzAssignment>();
    public DbSet<SzApprover> SzApprovers => Set<SzApprover>(); // согласующие, выбранные автором записки

    // --- Заседания Правления, КПА и комитетов (ТЗ «Исполнение решений КПА, Правления и Комитетов») ---
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<AgendaGuest> AgendaGuests => Set<AgendaGuest>();
    public DbSet<AgendaAssignment> AgendaAssignments => Set<AgendaAssignment>();
    public DbSet<AgendaFile> AgendaFiles => Set<AgendaFile>();

    // --- Интеграции (раздел 8 ТЗ) ---
    public DbSet<OutgoingEmail> OutgoingEmails => Set<OutgoingEmail>(); // очередь исходящих писем (INT-02)

    /// <summary>Связь со службой каталогов: адрес, учётная запись, расписание (INT-01).</summary>
    public DbSet<DirectorySettings> DirectorySettings => Set<DirectorySettings>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>(); // сохранённые фильтры поиска (GEN-04)


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DelosferaDbContext).Assembly);

        // Поисковые векторы (GEN-04) — тип Postgres, и другой провайдер их не понимает.
        // Тесты работают на in-memory базе, поэтому вне Postgres колонки исключаются
        // из модели: иначе весь тестовый набор падает на типе, который к проверяемой
        // логике отношения не имеет.
        if (!Database.IsNpgsql())
        {
            modelBuilder.Entity<Document>().Ignore(x => x.SearchVector);
            modelBuilder.Entity<SzDocument>().Ignore(x => x.SearchVector);
            modelBuilder.Entity<ProcurementRequest>().Ignore(x => x.SearchVector);
            modelBuilder.Entity<AgendaItem>().Ignore(x => x.SearchVector);

            // Дополнительные поля записки хранятся как jsonb — тот же случай.
            modelBuilder.Entity<SzDocument>().Ignore(x => x.ExtraFields);
        }
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();

        var pending = CollectSettingsChanges();
        var saved = base.SaveChanges();

        return saved + WriteSettingsChanges(pending, () => base.SaveChanges());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();

        var pending = CollectSettingsChanges();
        var saved = await base.SaveChangesAsync(cancellationToken);

        if (pending.Count == 0) return saved;

        delosfera_server.Modules.Settings.Services.SettingsChangeCollector.FillIds(pending);
        SettingsChanges.AddRange(pending.Select(p => p.Change));

        return saved + await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Готовит записи журнала до сохранения — после него старые значения полей
    /// уже недоступны.
    /// </summary>
    private List<(delosfera_server.Modules.Settings.Models.SettingsChange Change,
                  Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry)> CollectSettingsChanges()
    {
        if (_currentUser is null) return [];

        // Вне запроса текущего пользователя нет, и обращение к нему бросает
        // исключение: фоновые службы — чистка журнала посещений, закрытие
        // истёкших доверенностей, календарь обязательств — работают без
        // HttpContext и упали бы на первом же сохранении.
        //
        // Их правки в журнал настроек и не нужны: журнал ведут ради ответа на
        // вопрос «кто поменял», а у службы ответа нет.
        int userId;
        try
        {
            userId = _currentUser.UserId;
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        var pending = delosfera_server.Modules.Settings.Services.SettingsChangeCollector.Collect(
            ChangeTracker, userId == 0 ? null : userId);

        // ФИО берём из уже отслеживаемых сущностей, если человек там есть.
        // Отдельного запроса не делаем: журнал не повод ходить в базу при
        // каждом сохранении справочника — недостающее имя подставит выдача.
        if (pending.Count > 0 && userId != 0)
        {
            var name = ChangeTracker.Entries<Modules.Users.Models.User>()
                .FirstOrDefault(e => e.Entity.Id == userId)?.Entity.FullName;

            if (name is not null)
                foreach (var (change, _) in pending) change.UserName = name;
        }

        return pending;
    }

    private int WriteSettingsChanges(
        List<(delosfera_server.Modules.Settings.Models.SettingsChange Change,
              Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry)> pending,
        Func<int> save)
    {
        if (pending.Count == 0) return 0;

        delosfera_server.Modules.Settings.Services.SettingsChangeCollector.FillIds(pending);
        SettingsChanges.AddRange(pending.Select(p => p.Change));

        return save();
    }

    private void ApplyAuditInfo()
    {
        // Токен версии карточки обновляем при каждом сохранении: по нему EF отличит
        // «сохраняю то, что видел» от «сохраняю поверх чужой правки» (GEN-05).
        foreach (var document in ChangeTracker.Entries<Document>())
        {
            if (document.State is EntityState.Added or EntityState.Modified)
                document.Entity.ConcurrencyToken = Guid.NewGuid();
        }

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