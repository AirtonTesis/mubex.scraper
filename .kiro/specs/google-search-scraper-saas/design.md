# Design Document — SaaS de Extração de Dados do Google Search

## 1. Visão Geral da Arquitetura

O **SaaS de Extração de Dados do Google Search** implementa uma arquitetura distribuída com três camadas principais:

1. **Frontend (Angular)**: Interface do usuário com design Glassmorphism e sidebar expansível
2. **Backend API (.NET Core)**: Servidor de aplicação com CQRS, Vertical Slice Architecture e auto-validação de domínio
3. **Scraping Workers**: Componentes assíncronos para extração de dados utilizando Playwright/Selenium

A comunicação entre componentes segue padrões assíncronos e desacoplados, permitindo escalabilidade horizontal independente de cada camada.

### 1.1 Diagrama de Sistema High-Level

```
┌─────────────────────────────────────────────────────────────────┐
│                        Frontend (Angular)                       │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐   │
│  │  Dashboard   │  │ Search Lists │  │   Job Management   │   │
│  │   Module     │  │     CRUD     │  │      Module        │   │
│  └──────────────┘  └──────────────┘  └────────────────────┘   │
│         │                  │                    │               │
│         └──────────────────┴────────────────────┘               │
│                            │                                    │
│                     HTTPS/REST API                              │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                    Backend API (.NET Core)                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              API Layer (Controllers)                    │   │
│  │  - Authentication Endpoints (JWT)                       │   │
│  │  - Search List Endpoints (CRUD)                         │   │
│  │  - Job Management Endpoints (Queue Operations)          │   │
│  │  - Dashboard Query Endpoints                            │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │         Application Layer (CQRS with MediatR)           │   │
│  │  ┌──────────────────┐      ┌──────────────────┐        │   │
│  │  │    Commands      │      │     Queries      │        │   │
│  │  │ - CreateList     │      │ - GetDashboard   │        │   │
│  │  │ - UpdateList     │      │ - GetLists       │        │   │
│  │  │ - DeleteList     │      │ - GetJobHistory  │        │   │
│  │  │ - EnqueueJob     │      │ - GetJobStatus   │        │   │
│  │  └──────────────────┘      └──────────────────┘        │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              Domain Layer                               │   │
│  │  - BaseEntity (Id, CreatedAt, UpdatedAt)                │   │
│  │  - SearchList Entity (Map/Ensure validation)           │   │
│  │  - Job Entity (Status state machine)                    │   │
│  │  - User Entity                                          │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │         Infrastructure Layer                            │   │
│  │  - EF Core DbContext                                    │   │
│  │  - JWT Authentication Service                           │   │
│  │  - Queue Manager (Message Broker Integration)          │   │
│  └─────────────────────────────────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                      Message Queue
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                    Scraping Workers (.NET)                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │            Job Consumer Service                         │   │
│  │  - Polls queue for pending jobs                         │   │
│  │  - Updates job status via API callbacks                │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │         Scraping Engine (Playwright/Selenium)           │   │
│  │  - Browser automation with stealth techniques          │   │
│  │  - Proxy rotation                                       │   │
│  │  - User-Agent rotation                                  │   │
│  │  - CAPTCHA detection                                    │   │
│  │  - Retry mechanism                                      │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Princípios Arquiteturais

- **Separation of Concerns**: Camadas frontend, backend e workers são independentes
- **CQRS (Command Query Responsibility Segregation)**: Separação de operações de escrita e leitura
- **Vertical Slice Architecture**: Features organizadas como slices verticais completos
- **Domain-Driven Design**: Entidades de domínio auto-validadas com Map/Ensure
- **Asynchronous Processing**: Jobs de scraping processados fora do fluxo HTTP
- **Scalability**: Workers escaláveis horizontalmente de forma independente

---

## 2. Frontend Architecture (Angular)

### 2.1 Estrutura de Módulos

```
src/
├── app/
│   ├── core/
│   │   ├── auth/
│   │   │   ├── auth.service.ts
│   │   │   ├── auth.guard.ts
│   │   │   └── jwt.interceptor.ts
│   │   ├── api/
│   │   │   └── api.service.ts
│   │   └── i18n/
│   │       ├── translation.service.ts
│   │       └── validation-key.pipe.ts
│   ├── shared/
│   │   ├── components/
│   │   │   ├── sidebar/
│   │   │   │   ├── sidebar.component.ts
│   │   │   │   └── sidebar.component.scss (Glassmorphism)
│   │   │   └── error-display/
│   │   │       └── error-display.component.ts
│   │   └── models/
│   │       ├── search-list.model.ts
│   │       ├── job.model.ts
│   │       └── validation-error.model.ts
│   ├── features/
│   │   ├── dashboard/
│   │   │   ├── dashboard.module.ts
│   │   │   ├── dashboard.component.ts
│   │   │   ├── dashboard.service.ts
│   │   │   └── components/
│   │   │       ├── metric-card/
│   │   │       └── temporal-filter/
│   │   ├── search-lists/
│   │   │   ├── search-lists.module.ts
│   │   │   ├── list-view/
│   │   │   ├── create-edit/
│   │   │   └── search-lists.service.ts
│   │   └── jobs/
│   │       ├── jobs.module.ts
│   │       ├── job-queue/
│   │       ├── job-history/
│   │       └── jobs.service.ts
│   └── assets/
│       └── i18n/
│           └── pt-BR.json
```

### 2.2 Glassmorphism Design System

**Princípios Visuais:**
- Backgrounds translúcidos com `backdrop-filter: blur()`
- Bordas sutis com opacidade reduzida
- Sombras suaves para profundidade
- Paleta de cores com tons claros e pastéis

**CSS Base (SCSS):**

```scss
// glassmorphism-base.scss
@mixin glass-card {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 16px;
  box-shadow: 0 8px 32px 0 rgba(31, 38, 135, 0.15);
}

.glass-container {
  @include glass-card;
  padding: 24px;
  transition: all 0.3s ease;
  
  &:hover {
    background: rgba(255, 255, 255, 0.15);
    box-shadow: 0 12px 40px 0 rgba(31, 38, 135, 0.2);
  }
}
```

### 2.3 Sidebar Component (Hover-Expandable)

**Component Logic:**

```typescript
// sidebar.component.ts
@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.component.html',
  styleUrls: ['./sidebar.component.scss']
})
export class SidebarComponent {
  isExpanded = false;

  @HostListener('mouseenter')
  onMouseEnter(): void {
    this.isExpanded = true;
  }

  @HostListener('mouseleave')
  onMouseLeave(): void {
    this.isExpanded = false;
  }
}
```

**Component Template:**

```html
<!-- sidebar.component.html -->
<aside class="sidebar glass-container" [class.expanded]="isExpanded">
  <nav class="sidebar-nav">
    <a routerLink="/dashboard" class="nav-item">
      <i class="icon">📊</i>
      <span class="label" *ngIf="isExpanded">Dashboard</span>
    </a>
    <a routerLink="/search-lists" class="nav-item">
      <i class="icon">📋</i>
      <span class="label" *ngIf="isExpanded">Listas</span>
    </a>
    <a routerLink="/jobs" class="nav-item">
      <i class="icon">⚙️</i>
      <span class="label" *ngIf="isExpanded">Jobs</span>
    </a>
  </nav>
</aside>
```

**Component Styles:**

```scss
// sidebar.component.scss
.sidebar {
  position: fixed;
  left: 0;
  top: 0;
  height: 100vh;
  width: 64px;
  transition: width 0.3s ease;
  z-index: 1000;
  overflow: hidden;

  &.expanded {
    width: 240px;
  }

  .nav-item {
    display: flex;
    align-items: center;
    padding: 16px;
    gap: 12px;
    transition: background 0.2s ease;

    &:hover {
      background: rgba(255, 255, 255, 0.1);
    }

    .icon {
      font-size: 24px;
      min-width: 32px;
    }

    .label {
      white-space: nowrap;
      opacity: 0;
      animation: fadeIn 0.3s ease forwards;
    }
  }
}

@keyframes fadeIn {
  to { opacity: 1; }
}
```

### 2.4 Validation Key Translation Service

```typescript
// translation.service.ts
@Injectable({ providedIn: 'root' })
export class TranslationService {
  private translations: Record<string, string> = {};

  constructor(private http: HttpClient) {
    this.loadTranslations('pt-BR');
  }

  private loadTranslations(locale: string): void {
    this.http.get<Record<string, string>>(`/assets/i18n/${locale}.json`)
      .subscribe(data => this.translations = data);
  }

  translate(key: string): string {
    return this.translations[key] || key;
  }
}

// validation-key.pipe.ts
@Pipe({ name: 'validationKey' })
export class ValidationKeyPipe implements PipeTransform {
  constructor(private translationService: TranslationService) {}

  transform(key: string): string {
    return this.translationService.translate(key);
  }
}
```

**Translation File Example:**

```json
// pt-BR.json
{
  "validation.search_list.name_required": "O nome da lista é obrigatório",
  "validation.search_list.name_max_length": "O nome deve ter no máximo 100 caracteres",
  "validation.search_list.keywords_required": "Pelo menos uma palavra-chave é obrigatória",
  "validation.job.search_list_required": "Uma lista de busca deve ser selecionada",
  "auth.invalid_credentials": "Credenciais inválidas"
}
```

### 2.5 Dashboard Module with Temporal Filters

```typescript
// dashboard.component.ts
export interface DashboardMetrics {
  totalSearches: number;
  successRate: number;
  failureRate: number;
  activeExecutions: number;
}

export enum TemporalFilter {
  Day = 'day',
  Week = 'week',
  Month = 'month',
  Custom = 'custom'
}

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  metrics: DashboardMetrics | null = null;
  selectedFilter: TemporalFilter = TemporalFilter.Day;
  customStartDate: Date | null = null;
  customEndDate: Date | null = null;

  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.loadMetrics();
  }

  onFilterChange(filter: TemporalFilter): void {
    this.selectedFilter = filter;
    if (filter !== TemporalFilter.Custom) {
      this.loadMetrics();
    }
  }

  onCustomRangeApply(start: Date, end: Date): void {
    this.customStartDate = start;
    this.customEndDate = end;
    this.loadMetrics();
  }

  private loadMetrics(): void {
    let startDate: Date;
    let endDate: Date = new Date();

    switch (this.selectedFilter) {
      case TemporalFilter.Day:
        startDate = subDays(endDate, 1);
        break;
      case TemporalFilter.Week:
        startDate = subDays(endDate, 7);
        break;
      case TemporalFilter.Month:
        startDate = subDays(endDate, 30);
        break;
      case TemporalFilter.Custom:
        startDate = this.customStartDate!;
        endDate = this.customEndDate!;
        break;
    }

    this.dashboardService
      .getMetrics(startDate, endDate)
      .subscribe(metrics => this.metrics = metrics);
  }
}
```

---

## 3. Backend Architecture (.NET Core)

### 3.1 Vertical Slice Organization

```
src/
├── WebApi/
│   ├── Program.cs
│   ├── appsettings.json
│   └── Features/
│       ├── Authentication/
│       │   ├── AuthController.cs
│       │   ├── Login/
│       │   │   ├── LoginCommand.cs
│       │   │   ├── LoginCommandHandler.cs
│       │   │   └── LoginCommandValidator.cs
│       │   └── JwtService.cs
│       ├── SearchLists/
│       │   ├── SearchListsController.cs
│       │   ├── Create/
│       │   │   ├── CreateSearchListCommand.cs
│       │   │   ├── CreateSearchListHandler.cs
│       │   │   └── CreateSearchListValidator.cs
│       │   ├── Update/
│       │   │   ├── UpdateSearchListCommand.cs
│       │   │   └── UpdateSearchListHandler.cs
│       │   ├── Delete/
│       │   │   ├── DeleteSearchListCommand.cs
│       │   │   └── DeleteSearchListHandler.cs
│       │   └── GetAll/
│       │       ├── GetAllSearchListsQuery.cs
│       │       └── GetAllSearchListsHandler.cs
│       ├── Jobs/
│       │   ├── JobsController.cs
│       │   ├── Enqueue/
│       │   │   ├── EnqueueJobCommand.cs
│       │   │   └── EnqueueJobHandler.cs
│       │   ├── Pause/
│       │   │   ├── PauseJobCommand.cs
│       │   │   └── PauseJobHandler.cs
│       │   ├── Activate/
│       │   │   ├── ActivateJobCommand.cs
│       │   │   └── ActivateJobHandler.cs
│       │   └── GetHistory/
│       │       ├── GetJobHistoryQuery.cs
│       │       └── GetJobHistoryHandler.cs
│       └── Dashboard/
│           ├── DashboardController.cs
│           └── GetMetrics/
│               ├── GetDashboardMetricsQuery.cs
│               └── GetDashboardMetricsHandler.cs
├── Domain/
│   ├── Entities/
│   │   ├── BaseEntity.cs
│   │   ├── SearchList.cs
│   │   ├── Job.cs
│   │   └── User.cs
│   ├── ValueObjects/
│   │   ├── JobStatus.cs
│   │   └── ValidationKey.cs
│   └── Validation/
│       ├── IMapEnsure.cs
│       └── ValidationResult.cs
└── Infrastructure/
    ├── Persistence/
    │   ├── ApplicationDbContext.cs
    │   └── Configurations/
    │       ├── SearchListConfiguration.cs
    │       └── JobConfiguration.cs
    ├── Messaging/
    │   ├── IQueueManager.cs
    │   └── QueueManager.cs (RabbitMQ/Azure Service Bus)
    └── Authentication/
        └── JwtTokenService.cs
```

### 3.2 Domain Layer - BaseEntity with Auto-Validation

```csharp
// BaseEntity.cs
public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }

    protected BaseEntity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}

// IMapEnsure.cs
public interface IMapEnsure<T>
{
    static abstract ValidationResult Map(T value);
    static abstract ValidationResult Ensure(T value);
}

// ValidationResult.cs
public class ValidationResult
{
    public bool IsValid { get; }
    public List<ValidationKey> Errors { get; }

    private ValidationResult(bool isValid, List<ValidationKey> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public static ValidationResult Success() => 
        new ValidationResult(true, new List<ValidationKey>());

    public static ValidationResult Failure(params ValidationKey[] errors) => 
        new ValidationResult(false, errors.ToList());

    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var allErrors = results.SelectMany(r => r.Errors).ToList();
        return allErrors.Any() 
            ? new ValidationResult(false, allErrors)
            : Success();
    }
}

// ValidationKey.cs
public record ValidationKey
{
    public string Key { get; init; }

    public ValidationKey(string entityName, string fieldName, string errorType)
    {
        Key = $"validation.{entityName}.{fieldName}_{errorType}";
    }

    public static ValidationKey Required(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "required");

    public static ValidationKey MaxLength(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "max_length");

    public static ValidationKey MinLength(string entityName, string fieldName) =>
        new ValidationKey(entityName, fieldName, "min_length");
}
```

### 3.3 Domain Entity - SearchList with Map/Ensure

```csharp
// SearchList.cs
public class SearchList : BaseEntity, IMapEnsure<SearchList>
{
    private const int MaxNameLength = 100;
    private const int MinNameLength = 3;

    public string Name { get; private set; }
    public List<string> Keywords { get; private set; }
    public List<string> Domains { get; private set; }
    public Guid UserId { get; private set; }

    // Private constructor for EF Core
    private SearchList() 
    {
        Keywords = new List<string>();
        Domains = new List<string>();
    }

    // Factory method with validation
    public static Result<SearchList> Create(
        string name, 
        List<string> keywords, 
        List<string> domains,
        Guid userId)
    {
        var searchList = new SearchList
        {
            Name = name?.Trim(),
            Keywords = keywords ?? new List<string>(),
            Domains = domains ?? new List<string>(),
            UserId = userId
        };

        var mapResult = Map(searchList);
        if (!mapResult.IsValid)
            return Result<SearchList>.Failure(mapResult.Errors);

        var ensureResult = Ensure(searchList);
        if (!ensureResult.IsValid)
            return Result<SearchList>.Failure(ensureResult.Errors);

        return Result<SearchList>.Success(searchList);
    }

    public static ValidationResult Map(SearchList value)
    {
        var errors = new List<ValidationKey>();

        // Basic structural validation
        if (string.IsNullOrWhiteSpace(value.Name))
            errors.Add(ValidationKey.Required("search_list", "name"));

        if (value.Name?.Length > MaxNameLength)
            errors.Add(ValidationKey.MaxLength("search_list", "name"));

        if (value.Keywords == null || !value.Keywords.Any())
            errors.Add(ValidationKey.Required("search_list", "keywords"));

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    public static ValidationResult Ensure(SearchList value)
    {
        var errors = new List<ValidationKey>();

        // Business rules and invariants
        if (value.Name?.Length < MinNameLength)
            errors.Add(ValidationKey.MinLength("search_list", "name"));

        if (value.Keywords.Any(k => string.IsNullOrWhiteSpace(k)))
            errors.Add(new ValidationKey("search_list", "keywords", "contains_empty"));

        if (value.UserId == Guid.Empty)
            errors.Add(new ValidationKey("search_list", "user_id", "invalid"));

        return errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : ValidationResult.Success();
    }

    public void Update(string name, List<string> keywords, List<string> domains)
    {
        Name = name?.Trim();
        Keywords = keywords ?? new List<string>();
        Domains = domains ?? new List<string>();
        Touch();
    }
}

// Result.cs (Helper for validation results)
public class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public List<ValidationKey> Errors { get; }

    private Result(bool isSuccess, T value, List<ValidationKey> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public static Result<T> Success(T value) => 
        new Result<T>(true, value, new List<ValidationKey>());

    public static Result<T> Failure(List<ValidationKey> errors) => 
        new Result<T>(false, default, errors);
}
```

### 3.4 Domain Entity - Job with Status State Machine

```csharp
// JobStatus.cs
public enum JobStatus
{
    Pending,
    Active,
    Paused,
    Completed,
    Failed
}

// Job.cs
public class Job : BaseEntity
{
    public Guid SearchListId { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<JobHistoryEntry> History { get; private set; }

    // Navigation property
    public SearchList SearchList { get; private set; }

    private Job()
    {
        History = new List<JobHistoryEntry>();
    }

    public static Job Create(Guid searchListId)
    {
        var job = new Job
        {
            SearchListId = searchListId,
            Status = JobStatus.Pending
        };
        
        job.AddHistoryEntry(JobStatus.Pending);
        return job;
    }

    public void Start()
    {
        if (Status != JobStatus.Pending && Status != JobStatus.Paused)
            throw new InvalidOperationException(
                $"Cannot start job in {Status} status");

        Status = JobStatus.Active;
        StartedAt = DateTime.UtcNow;
        AddHistoryEntry(JobStatus.Active);
        Touch();
    }

    public void Pause()
    {
        if (Status != JobStatus.Active)
            throw new InvalidOperationException(
                $"Cannot pause job in {Status} status");

        Status = JobStatus.Paused;
        AddHistoryEntry(JobStatus.Paused);
        Touch();
    }

    public void Resume()
    {
        if (Status != JobStatus.Paused)
            throw new InvalidOperationException(
                $"Cannot resume job in {Status} status");

        Status = JobStatus.Active;
        AddHistoryEntry(JobStatus.Active);
        Touch();
    }

    public void Complete()
    {
        if (Status != JobStatus.Active)
            throw new InvalidOperationException(
                $"Cannot complete job in {Status} status");

        Status = JobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddHistoryEntry(JobStatus.Completed);
        Touch();
    }

    public void Fail(string errorMessage)
    {
        Status = JobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        AddHistoryEntry(JobStatus.Failed);
        Touch();
    }

    public void IncrementRetry()
    {
        RetryCount++;
        Touch();
    }

    private void AddHistoryEntry(JobStatus status)
    {
        History.Add(new JobHistoryEntry
        {
            Id = Guid.NewGuid(),
            JobId = Id,
            Status = status,
            Timestamp = DateTime.UtcNow
        });
    }
}

// JobHistoryEntry.cs
public class JobHistoryEntry
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public JobStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### 3.5 CQRS Implementation with MediatR

**Command Example:**

```csharp
// CreateSearchListCommand.cs
public record CreateSearchListCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; }
    public List<string> Keywords { get; init; }
    public List<string> Domains { get; init; }
    public Guid UserId { get; init; }
}

// CreateSearchListHandler.cs
public class CreateSearchListHandler 
    : IRequestHandler<CreateSearchListCommand, Result<Guid>>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CreateSearchListHandler> _logger;

    public CreateSearchListHandler(
        ApplicationDbContext context,
        ILogger<CreateSearchListHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        CreateSearchListCommand request, 
        CancellationToken cancellationToken)
    {
        // Create entity with validation
        var result = SearchList.Create(
            request.Name,
            request.Keywords,
            request.Domains,
            request.UserId
        );

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Validation failed for SearchList creation: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Key)));
            return Result<Guid>.Failure(result.Errors);
        }

        // Persist entity
        _context.SearchLists.Add(result.Value);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "SearchList created successfully: {Id}", 
            result.Value.Id);

        return Result<Guid>.Success(result.Value.Id);
    }
}
```

**Query Example:**

```csharp
// GetDashboardMetricsQuery.cs
public record GetDashboardMetricsQuery : IRequest<DashboardMetricsDto>
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

// DashboardMetricsDto.cs
public record DashboardMetricsDto
{
    public int TotalSearches { get; init; }
    public decimal SuccessRate { get; init; }
    public decimal FailureRate { get; init; }
    public int ActiveExecutions { get; init; }
}

// GetDashboardMetricsHandler.cs
public class GetDashboardMetricsHandler 
    : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    private readonly ApplicationDbContext _context;

    public GetDashboardMetricsHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardMetricsDto> Handle(
        GetDashboardMetricsQuery request, 
        CancellationToken cancellationToken)
    {
        // Optimized read query without tracking
        var jobs = await _context.Jobs
            .AsNoTracking()
            .Where(j => j.CreatedAt >= request.StartDate 
                     && j.CreatedAt <= request.EndDate)
            .ToListAsync(cancellationToken);

        var totalSearches = jobs.Count;
        var completed = jobs.Count(j => j.Status == JobStatus.Completed);
        var failed = jobs.Count(j => j.Status == JobStatus.Failed);
        var active = jobs.Count(j => j.Status == JobStatus.Active);

        var successRate = totalSearches > 0 
            ? (decimal)completed / totalSearches * 100 
            : 0;
        var failureRate = totalSearches > 0 
            ? (decimal)failed / totalSearches * 100 
            : 0;

        return new DashboardMetricsDto
        {
            TotalSearches = totalSearches,
            SuccessRate = Math.Round(successRate, 2),
            FailureRate = Math.Round(failureRate, 2),
            ActiveExecutions = active
        };
    }
}
```

### 3.6 API Controller with Vertical Slice

```csharp
// SearchListsController.cs
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchListsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchListsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSearchListCommand command)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var commandWithUser = command with { UserId = userId };

        var result = await _mediator.Send(commandWithUser);

        if (!result.IsSuccess)
        {
            var problemDetails = new ValidationProblemDetails
            {
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest
            };

            foreach (var error in result.Errors)
            {
                problemDetails.Errors.Add(error.Key, new[] { error.Key });
            }

            return BadRequest(problemDetails);
        }

        return CreatedAtAction(
            nameof(GetById), 
            new { id = result.Value }, 
            result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SearchListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var query = new GetAllSearchListsQuery
        {
            UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, 
        [FromBody] UpdateSearchListCommand command)
    {
        var commandWithId = command with { Id = id };
        var result = await _mediator.Send(commandWithId);

        return result.IsSuccess ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteSearchListCommand { Id = id };
        var result = await _mediator.Send(command);

        return result ? NoContent() : NotFound();
    }
}
```

### 3.7 JWT Authentication Service

```csharp
// JwtTokenService.cs
public interface IJwtTokenService
{
    string GenerateToken(User user);
    ClaimsPrincipal? ValidateToken(string token);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        var secret = _configuration["Jwt:Secret"];
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            _key, 
            SecurityAlgorithms.HmacSha256);

        var expirationMinutes = int.Parse(
            _configuration["Jwt:ExpirationMinutes"]);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidAudience = _configuration["Jwt:Audience"],
            IssuerSigningKey = _key,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(
                token, 
                validationParameters, 
                out var validatedToken);
            
            return validatedToken is JwtSecurityToken jwtToken
                && jwtToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256, 
                    StringComparison.InvariantCultureIgnoreCase)
                ? principal
                : null;
        }
        catch
        {
            return null;
        }
    }
}
```

### 3.8 Queue Manager Interface

```csharp
// IQueueManager.cs
public interface IQueueManager
{
    Task EnqueueJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<Job?> DequeueJobAsync(CancellationToken cancellationToken = default);
    Task UpdateJobStatusAsync(Guid jobId, JobStatus status, 
        CancellationToken cancellationToken = default);
}

// QueueManager.cs (RabbitMQ implementation example)
public class RabbitMqQueueManager : IQueueManager
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RabbitMqQueueManager> _logger;
    private const string QueueName = "scraping-jobs";

    public RabbitMqQueueManager(
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<RabbitMqQueueManager> logger)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMq:Host"],
            Port = int.Parse(configuration["RabbitMq:Port"]),
            UserName = configuration["RabbitMq:Username"],
            Password = configuration["RabbitMq:Password"]
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _context = context;
        _logger = logger;
    }

    public async Task EnqueueJobAsync(
        Guid jobId, 
        CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Serialize(new { JobId = jobId });
        var body = Encoding.UTF8.GetBytes(message);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(
            exchange: "",
            routingKey: QueueName,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Job {JobId} enqueued successfully", jobId);
        await Task.CompletedTask;
    }

    public async Task<Job?> DequeueJobAsync(
        CancellationToken cancellationToken = default)
    {
        var result = _channel.BasicGet(QueueName, autoAck: false);
        
        if (result == null)
            return null;

        var message = Encoding.UTF8.GetString(result.Body.ToArray());
        var jobData = JsonSerializer.Deserialize<JobMessage>(message);

        var job = await _context.Jobs
            .Include(j => j.SearchList)
            .FirstOrDefaultAsync(
                j => j.Id == jobData.JobId, 
                cancellationToken);

        if (job != null)
        {
            _channel.BasicAck(result.DeliveryTag, multiple: false);
            _logger.LogInformation("Job {JobId} dequeued successfully", jobData.JobId);
        }
        else
        {
            _channel.BasicNack(result.DeliveryTag, multiple: false, requeue: false);
            _logger.LogWarning("Job {JobId} not found in database", jobData.JobId);
        }

        return job;
    }

    public async Task UpdateJobStatusAsync(
        Guid jobId, 
        JobStatus status, 
        CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs.FindAsync(
            new object[] { jobId }, 
            cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found for status update", jobId);
            return;
        }

        switch (status)
        {
            case JobStatus.Active:
                job.Start();
                break;
            case JobStatus.Paused:
                job.Pause();
                break;
            case JobStatus.Completed:
                job.Complete();
                break;
            case JobStatus.Failed:
                job.Fail("Status updated to failed");
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Job {JobId} status updated to {Status}", 
            jobId, 
            status);
    }
}

private record JobMessage(Guid JobId);
```

---

## 4. Scraping Workers Architecture

### 4.1 Worker Service Structure

```csharp
// ScrapingWorkerService.cs
public class ScrapingWorkerService : BackgroundService
{
    private readonly IQueueManager _queueManager;
    private readonly IScrapingEngine _scrapingEngine;
    private readonly ILogger<ScrapingWorkerService> _logger;

    public ScrapingWorkerService(
        IQueueManager queueManager,
        IScrapingEngine scrapingEngine,
        ILogger<ScrapingWorkerService> logger)
    {
        _queueManager = queueManager;
        _scrapingEngine = scrapingEngine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scraping Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queueManager.DequeueJobAsync(stoppingToken);

                if (job == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Processing job {JobId}", job.Id);

                await _queueManager.UpdateJobStatusAsync(
                    job.Id, 
                    JobStatus.Active, 
                    stoppingToken);

                var result = await _scrapingEngine.ExecuteAsync(
                    job, 
                    stoppingToken);

                if (result.IsSuccess)
                {
                    await _queueManager.UpdateJobStatusAsync(
                        job.Id, 
                        JobStatus.Completed, 
                        stoppingToken);
                    
                    _logger.LogInformation(
                        "Job {JobId} completed successfully", 
                        job.Id);
                }
                else
                {
                    await _queueManager.UpdateJobStatusAsync(
                        job.Id, 
                        JobStatus.Failed, 
                        stoppingToken);
                    
                    _logger.LogError(
                        "Job {JobId} failed: {Error}", 
                        job.Id, 
                        result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Scraping Worker stopped");
    }
}
```

### 4.2 Scraping Engine with Playwright

```csharp
// IScrapingEngine.cs
public interface IScrapingEngine
{
    Task<ScrapingResult> ExecuteAsync(Job job, CancellationToken cancellationToken);
}

public record ScrapingResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SearchResultData> Data { get; init; } = new();
}

public record SearchResultData
{
    public string Keyword { get; init; }
    public string Domain { get; init; }
    public int Position { get; init; }
    public string Url { get; init; }
}

// PlaywrightScrapingEngine.cs
public class PlaywrightScrapingEngine : IScrapingEngine
{
    private readonly IProxyRotationService _proxyService;
    private readonly IUserAgentRotationService _userAgentService;
    private readonly ICaptchaDetectionService _captchaDetection;
    private readonly ILogger<PlaywrightScrapingEngine> _logger;
    private const int MaxRetries = 3;

    public PlaywrightScrapingEngine(
        IProxyRotationService proxyService,
        IUserAgentRotationService userAgentService,
        ICaptchaDetectionService captchaDetection,
        ILogger<PlaywrightScrapingEngine> logger)
    {
        _proxyService = proxyService;
        _userAgentService = userAgentService;
        _captchaDetection = captchaDetection;
        _logger = logger;
    }

    public async Task<ScrapingResult> ExecuteAsync(
        Job job, 
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResultData>();
        string? lastError = null;

        foreach (var keyword in job.SearchList.Keywords)
        {
            var retryCount = 0;
            bool success = false;

            while (retryCount < MaxRetries && !success)
            {
                try
                {
                    var keywordResults = await ScrapeKeywordAsync(
                        keyword, 
                        job.SearchList.Domains,
                        cancellationToken);

                    results.AddRange(keywordResults);
                    success = true;
                }
                catch (CaptchaDetectedException ex)
                {
                    retryCount++;
                    lastError = ex.Message;
                    
                    _logger.LogWarning(
                        "CAPTCHA detected for keyword {Keyword}, retry {Retry}/{Max}",
                        keyword, retryCount, MaxRetries);

                    if (retryCount < MaxRetries)
                    {
                        // Change proxy before retry
                        _proxyService.RotateProxy();
                        await Task.Delay(
                            TimeSpan.FromSeconds(Random.Shared.Next(5, 15)), 
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    retryCount++;
                    lastError = ex.Message;
                    
                    _logger.LogError(
                        ex, 
                        "Error scraping keyword {Keyword}, retry {Retry}/{Max}",
                        keyword, retryCount, MaxRetries);

                    if (retryCount < MaxRetries)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(Random.Shared.Next(3, 8)), 
                            cancellationToken);
                    }
                }
            }

            if (!success)
            {
                _logger.LogError(
                    "Failed to scrape keyword {Keyword} after {Retries} attempts",
                    keyword, MaxRetries);
                
                return new ScrapingResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed after {MaxRetries} retries: {lastError}"
                };
            }
        }

        return new ScrapingResult
        {
            IsSuccess = true,
            Data = results
        };
    }

    private async Task<List<SearchResultData>> ScrapeKeywordAsync(
        string keyword,
        List<string> targetDomains,
        CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        
        var proxy = _proxyService.GetCurrentProxy();
        var userAgent = _userAgentService.GetRandomUserAgent();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Proxy = new Proxy
            {
                Server = proxy.Server,
                Username = proxy.Username,
                Password = proxy.Password
            }
        };

        await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);
        
        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = userAgent,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "pt-BR",
            TimezoneId = "America/Sao_Paulo"
        };

        var context = await browser.NewContextAsync(contextOptions);

        // Stealth techniques
        await context.AddInitScriptAsync(@"
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });
            
            window.chrome = {
                runtime: {}
            };
        ");

        var page = await context.NewPageAsync();

        // Emulate human behavior
        await page.Mouse.MoveAsync(
            Random.Shared.Next(100, 500),
            Random.Shared.Next(100, 500));

        var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(keyword)}";
        await page.GotoAsync(searchUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        // Random delay to simulate human reading
        await Task.Delay(
            TimeSpan.FromMilliseconds(Random.Shared.Next(1000, 3000)), 
            cancellationToken);

        // Check for CAPTCHA
        var hasCaptcha = await _captchaDetection.DetectAsync(page);
        if (hasCaptcha)
        {
            throw new CaptchaDetectedException(
                "CAPTCHA detected on Google search page");
        }

        // Extract search results
        var results = new List<SearchResultData>();
        var searchResults = await page.QuerySelectorAllAsync("div.g");

        for (int i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            var linkElement = await result.QuerySelectorAsync("a");
            
            if (linkElement == null)
                continue;

            var url = await linkElement.GetAttributeAsync("href");
            
            if (string.IsNullOrEmpty(url))
                continue;

            var uri = new Uri(url);
            var domain = uri.Host.Replace("www.", "");

            if (targetDomains.Any(d => domain.Contains(d, 
                StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(new SearchResultData
                {
                    Keyword = keyword,
                    Domain = domain,
                    Position = i + 1,
                    Url = url
                });
            }
        }

        return results;
    }
}

public class CaptchaDetectedException : Exception
{
    public CaptchaDetectedException(string message) : base(message) { }
}
```

### 4.3 Proxy Rotation Service

```csharp
// IProxyRotationService.cs
public interface IProxyRotationService
{
    ProxyConfig GetCurrentProxy();
    void RotateProxy();
}

public record ProxyConfig
{
    public string Server { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
}

// ProxyRotationService.cs
public class ProxyRotationService : IProxyRotationService
{
    private readonly List<ProxyConfig> _proxies;
    private int _currentIndex;
    private readonly object _lock = new();

    public ProxyRotationService(IConfiguration configuration)
    {
        _proxies = configuration
            .GetSection("Proxies")
            .Get<List<ProxyConfig>>() ?? new List<ProxyConfig>();
        
        _currentIndex = 0;
    }

    public ProxyConfig GetCurrentProxy()
    {
        lock (_lock)
        {
            if (!_proxies.Any())
                throw new InvalidOperationException("No proxies configured");

            return _proxies[_currentIndex];
        }
    }

    public void RotateProxy()
    {
        lock (_lock)
        {
            _currentIndex = (_currentIndex + 1) % _proxies.Count;
        }
    }
}
```

### 4.4 User-Agent Rotation Service

```csharp
// IUserAgentRotationService.cs
public interface IUserAgentRotationService
{
    string GetRandomUserAgent();
}

// UserAgentRotationService.cs
public class UserAgentRotationService : IUserAgentRotationService
{
    private readonly List<string> _userAgents = new()
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0"
    };

    public string GetRandomUserAgent()
    {
        var index = Random.Shared.Next(_userAgents.Count);
        return _userAgents[index];
    }
}
```

### 4.5 CAPTCHA Detection Service

```csharp
// ICaptchaDetectionService.cs
public interface ICaptchaDetectionService
{
    Task<bool> DetectAsync(IPage page);
}

// CaptchaDetectionService.cs
public class CaptchaDetectionService : ICaptchaDetectionService
{
    private readonly ILogger<CaptchaDetectionService> _logger;

    public CaptchaDetectionService(ILogger<CaptchaDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> DetectAsync(IPage page)
    {
        try
        {
            // Check for reCAPTCHA iframe
            var recaptchaFrame = await page.QuerySelectorAsync("iframe[src*='recaptcha']");
            if (recaptchaFrame != null)
            {
                _logger.LogWarning("reCAPTCHA iframe detected");
                return true;
            }

            // Check for reCAPTCHA div
            var recaptchaDiv = await page.QuerySelectorAsync("div.g-recaptcha");
            if (recaptchaDiv != null)
            {
                _logger.LogWarning("reCAPTCHA div detected");
                return true;
            }

            // Check for "unusual traffic" message
            var pageContent = await page.ContentAsync();
            if (pageContent.Contains("unusual traffic", StringComparison.OrdinalIgnoreCase) ||
                pageContent.Contains("automated requests", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unusual traffic message detected");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting CAPTCHA");
            return false;
        }
    }
}
```

---

## 5. Data Models

### 5.1 Database Schema (EF Core)

```csharp
// ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<SearchList> SearchLists { get; set; }
    public DbSet<Job> Jobs { get; set; }
    public DbSet<JobHistoryEntry> JobHistoryEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new SearchListConfiguration());
        modelBuilder.ApplyConfiguration(new JobConfiguration());
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // Auto-update timestamps
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || 
                       e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

// SearchListConfiguration.cs
public class SearchListConfiguration : IEntityTypeConfiguration<SearchList>
{
    public void Configure(EntityTypeBuilder<SearchList> builder)
    {
        builder.ToTable("SearchLists");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Keywords)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null))
            .HasColumnType("jsonb");

        builder.Property(s => s.Domains)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null))
            .HasColumnType("jsonb");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.CreatedAt);
    }
}

// JobConfiguration.cs
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(j => j.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne(j => j.SearchList)
            .WithMany()
            .HasForeignKey(j => j.SearchListId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(j => j.History)
            .WithOne()
            .HasForeignKey(h => h.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.CreatedAt);
    }
}
```

### 5.2 ER Diagram

```
┌─────────────────────┐
│       Users         │
│─────────────────────│
│ Id (PK)             │
│ Email               │
│ PasswordHash        │
│ CreatedAt           │
│ UpdatedAt           │
└─────────────────────┘
           │
           │ 1:N
           │
┌─────────────────────┐
│    SearchLists      │
│─────────────────────│
│ Id (PK)             │
│ Name                │
│ Keywords (JSON)     │
│ Domains (JSON)      │
│ UserId (FK)         │
│ CreatedAt           │
│ UpdatedAt           │
└─────────────────────┘
           │
           │ 1:N
           │
┌─────────────────────┐
│        Jobs         │
│─────────────────────│
│ Id (PK)             │
│ SearchListId (FK)   │
│ Status              │
│ StartedAt           │
│ CompletedAt         │
│ RetryCount          │
│ ErrorMessage        │
│ CreatedAt           │
│ UpdatedAt           │
└─────────────────────┘
           │
           │ 1:N
           │
┌─────────────────────┐
│  JobHistoryEntry    │
│─────────────────────│
│ Id (PK)             │
│ JobId (FK)          │
│ Status              │
│ Timestamp           │
└─────────────────────┘
```

---

## 6. Error Handling and Logging

### 6.1 Global Exception Handler

```csharp
// GlobalExceptionHandler.cs
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;
        
        _logger.LogError(
            exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}",
            correlationId,
            httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request",
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = 
            StatusCodes.Status500InternalServerError;
        
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails, 
            cancellationToken);

        return true;
    }
}
```

### 6.2 Structured Logging Configuration

```csharp
// Program.cs logging setup
builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    config.AddConsole();
    config.AddDebug();
    
    if (builder.Environment.IsProduction())
    {
        // Add Serilog or Application Insights
        config.AddSerilog(new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
            .Enrich.WithProperty("Application", "GoogleSearchScraper")
            .Enrich.WithCorrelationId()
            .CreateLogger());
    }
});
```

### 6.3 Correlation ID Middleware

```csharp
// CorrelationIdMiddleware.cs
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader]
            .FirstOrDefault() ?? Guid.NewGuid().ToString();

        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

---

## 7. Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property Reflection

After analyzing all 70 acceptance criteria, the following redundancies were identified and resolved:

**Eliminated Redundancies:**
- **Timestamp properties** (12.3, 12.4, 12.5, 12.6): Consolidated into two comprehensive properties covering creation and update behaviors
- **Job state transitions** (4.5, 4.7): Combined into a single state machine property
- **Validation format properties** (8.4, 8.5): Merged into one comprehensive validation key format property
- **HTTP status code properties** (13.3, 13.4): Combined into comprehensive error response property
- **Retry mechanism properties** (6.3, 6.4, 6.5, 6.6): Consolidated into two properties covering retry limits and proxy rotation

**Retained Properties:**
Each remaining property provides unique validation value and tests distinct system behaviors.

### Property 1: JWT Token Generation with Expiration

*For any* valid user credentials, when authentication succeeds, the generated JWT token SHALL contain a valid expiration claim set to a future timestamp.

**Validates: Requirements 1.2**

### Property 2: Invalid Credentials Return Validation Keys

*For any* invalid credential combination, the authentication response SHALL return a structured Validation_Key following the format `validation.{entity}.{field}_{error}`.

**Validates: Requirements 1.3**

### Property 3: Token Validation Enforces Signature and Expiration

*For any* JWT token with invalid signature or expired timestamp, the validation process SHALL reject the token and return failure.

**Validates: Requirements 1.5**

### Property 4: Sidebar Content Adjustment on Expansion

*For any* UI state, when the sidebar expands from collapsed to expanded state, the main content area position SHALL adjust to accommodate the new sidebar width.

**Validates: Requirements 2.4**

### Property 5: SearchList Validation via Map and Ensure

*For any* SearchList data submitted for creation, the system SHALL invoke both Map and Ensure validation functions before persistence.

**Validates: Requirements 3.2**

### Property 6: Validation Failures Return Structured Keys

*For any* invalid SearchList data that fails validation, the system SHALL return a collection of Validation_Key identifiers matching the pattern `validation.search_list.{field}_{error}`.

**Validates: Requirements 3.3, 8.4**

### Property 7: SearchList Persistence Round-Trip

*For any* valid SearchList, persisting via CQRS command then querying back SHALL produce an equivalent SearchList with matching name, keywords, and domains.

**Validates: Requirements 3.4**

### Property 8: SearchList Deletion Removes Entity

*For any* existing SearchList, after successful deletion, subsequent queries SHALL NOT return the deleted entity.

**Validates: Requirements 3.8**

### Property 9: Job Enqueue is Non-Blocking

*For any* job creation request, the HTTP response SHALL return immediately without waiting for job processing completion.

**Validates: Requirements 4.2**

### Property 10: Job State Transitions Follow State Machine

*For any* job, state transitions SHALL only occur through valid paths: Pending→Active, Active→Paused, Paused→Active, Active→Completed, Active→Failed.

**Validates: Requirements 4.5, 4.7**

### Property 11: Job History Chronological Ordering

*For any* job execution history, the displayed list SHALL be ordered chronologically by timestamp in ascending order.

**Validates: Requirements 4.10**

### Property 12: Scraping Emulation Includes Randomization

*For any* scraping task execution, the automation SHALL include random delays and cursor movements to emulate human behavior.

**Validates: Requirements 5.3**

### Property 13: User-Agent Rotation Between Requests

*For any* sequence of scraping requests, consecutive requests SHALL use different User-Agent headers.

**Validates: Requirements 5.5**

### Property 14: Proxy Rotation Between Requests

*For any* sequence of scraping requests, consecutive requests SHALL use different proxy configurations.

**Validates: Requirements 5.6**

### Property 15: Retry Mechanism Respects Maximum Attempts

*For any* failing scraping task, the retry mechanism SHALL attempt exactly 3 retries before marking the job as failed.

**Validates: Requirements 6.3, 6.5**

### Property 16: Retry Uses Different Proxy

*For any* retry attempt after CAPTCHA detection or failure, the system SHALL select a different proxy from the available pool than the previous attempt.

**Validates: Requirements 6.4**

### Property 17: Dashboard Metrics Calculation Accuracy

*For any* set of job execution data within a time range, the calculated metrics (total searches, success rate, failure rate) SHALL accurately reflect the data statistics.

**Validates: Requirements 7.1**

### Property 18: Custom Date Range Filtering

*For any* valid custom date range with start and end dates, the dashboard SHALL display metrics containing only jobs created within that inclusive range.

**Validates: Requirements 7.7**

### Property 19: Entity Validation Invocation on Create/Update

*For any* domain entity being created or updated, the system SHALL invoke both Map and Ensure validation functions before persistence.

**Validates: Requirements 8.3**

### Property 20: Validation Key Format Compliance

*For any* validation error generated by the system, the Validation_Key SHALL match the naming pattern `validation.{entity_name}.{field_name}_{error_type}`.

**Validates: Requirements 8.5**

### Property 21: Validation Key Translation to PT-BR

*For any* Validation_Key received from the backend, the frontend SHALL provide a corresponding PT-BR translation string.

**Validates: Requirements 8.6, 11.3**

### Property 22: Read Queries Have No Side Effects

*For any* CQRS query handler execution, no database modifications SHALL occur (verified by query using AsNoTracking).

**Validates: Requirements 9.5**

### Property 23: Distributed Job Processing Without Duplication

*For any* job in the queue with multiple worker instances active, each job SHALL be processed by exactly one worker without duplication.

**Validates: Requirements 10.4**

### Property 24: Entity Creation Sets Timestamps

*For any* new entity creation, both CreatedAt and UpdatedAt properties SHALL be set to the current UTC timestamp at the moment of creation.

**Validates: Requirements 12.3, 12.4**

### Property 25: Entity Update Modifies UpdatedAt Only

*For any* existing entity update operation, the UpdatedAt property SHALL be modified to current UTC timestamp while CreatedAt SHALL remain unchanged.

**Validates: Requirements 12.5, 12.6**

### Property 26: Exception Logging Includes Context

*For any* unhandled exception, the system SHALL log error details including stack trace, correlation ID, and request context.

**Validates: Requirements 13.1**

### Property 27: Failed Job Logging Includes Details

*For any* scraping job that fails after all retry attempts, the system SHALL log detailed error information including proxy used and failure reason.

**Validates: Requirements 13.2**

### Property 28: Error Responses Use Appropriate HTTP Status Codes

*For any* error condition, the system SHALL return HTTP responses with semantically correct status codes (400 for validation, 401 for authentication, 403 for authorization, 500 for server errors).

**Validates: Requirements 13.3, 13.4, 13.5, 13.6**

### Property 29: Correlation ID Propagation Across Components

*For any* request flowing through multiple system components (API → Queue → Worker), the correlation ID SHALL be preserved and included in all log entries.

**Validates: Requirements 13.7**

---

## 8. Technology Stack Summary

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Frontend** | Angular 17+ | SPA framework |
| | TypeScript | Type-safe development |
| | RxJS | Reactive programming |
| | SCSS | Glassmorphism styling |
| **Backend API** | .NET 8 | Web API framework |
| | MediatR | CQRS implementation |
| | EF Core | ORM and data access |
| | FluentValidation | Input validation |
| | JWT Bearer | Authentication |
| **Workers** | .NET 8 Background Service | Job processing |
| | Playwright | Browser automation |
| | Selenium | Fallback automation |
| **Data** | PostgreSQL | Primary database |
| | RabbitMQ / Azure Service Bus | Message queue |
| **Infrastructure** | Docker | Containerization |
| | Redis (optional) | Caching |
| | Serilog | Structured logging |
| | Application Insights | Monitoring |

---

## 9. Deployment Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                      Load Balancer                           │
└────────────────────────┬─────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
┌────────▼────────┐            ┌────────▼────────┐
│   Web Server 1  │            │   Web Server 2  │
│   (Angular)     │            │   (Angular)     │
└─────────────────┘            └─────────────────┘
         │                               │
         └───────────────┬───────────────┘
                         │
                         │ HTTPS/REST
                         │
┌────────────────────────▼─────────────────────────────────────┐
│               API Gateway / Reverse Proxy                    │
└────────────────────────┬─────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         │                               │
┌────────▼────────┐            ┌────────▼────────┐
│  API Instance 1 │            │  API Instance 2 │
│  (.NET Core)    │            │  (.NET Core)    │
└────────┬────────┘            └────────┬────────┘
         │                               │
         └───────────────┬───────────────┘
                         │
         ┌───────────────┼───────────────┐
         │               │               │
┌────────▼────────┐ ┌───▼───┐  ┌───────▼────────┐
│   PostgreSQL    │ │ Redis │  │   RabbitMQ     │
│    (Primary)    │ │(Cache)│  │  (Message      │
│                 │ │       │  │   Queue)       │
└─────────────────┘ └───────┘  └───────┬────────┘
                                        │
                         ┌──────────────┴──────────────┐
                         │                             │
                ┌────────▼────────┐          ┌────────▼────────┐
                │  Worker Pool 1  │          │  Worker Pool 2  │
                │  (Scraping)     │          │  (Scraping)     │
                └─────────────────┘          └─────────────────┘
```

---

## 10. Security Considerations

### 10.1 Authentication & Authorization
- JWT tokens with short expiration (15-30 minutes)
- Refresh token mechanism for seamless renewal
- Role-based access control (RBAC) for admin features
- HTTPS-only communication

### 10.2 Data Protection
- Password hashing using bcrypt or Argon2
- Sensitive configuration in environment variables
- Database connection strings encrypted
- API keys and secrets in Azure Key Vault or similar

### 10.3 Rate Limiting
- API rate limiting per user/IP
- Scraping rate limiting to avoid detection
- Queue throttling to prevent resource exhaustion

### 10.4 Input Validation
- All user inputs validated at API boundary
- SQL injection prevention via parameterized queries (EF Core)
- XSS prevention in Angular templates (built-in sanitization)

---

## 11. Performance Optimization

### 11.1 Backend Optimizations
- **EF Core AsNoTracking**: Read queries without change tracking
- **Database Indexing**: Indexes on UserId, CreatedAt, Status
- **Connection Pooling**: Reuse database connections
- **Caching**: Redis for frequently accessed data (dashboard metrics)

### 11.2 Frontend Optimizations
- **Lazy Loading**: Feature modules loaded on demand
- **OnPush Change Detection**: Reduce Angular change detection cycles
- **Virtual Scrolling**: Efficient rendering of large lists
- **CDN**: Static assets served from CDN

### 11.3 Worker Optimizations
- **Parallel Processing**: Multiple workers processing jobs concurrently
- **Browser Context Reuse**: Reuse Playwright contexts when possible
- **Connection Pooling**: Reuse proxy connections

---

## 12. Monitoring and Observability

### 12.1 Logging Strategy
- **Structured Logging**: JSON-formatted logs with correlation IDs
- **Log Levels**: Debug, Info, Warning, Error, Critical
- **Log Aggregation**: Centralized logging (ELK Stack or Application Insights)

### 12.2 Metrics
- Request latency (API endpoints)
- Job processing time
- Success/failure rates
- Queue depth
- Active worker count

### 12.3 Alerting
- Failed job threshold exceeded
- Queue depth critical
- API error rate spike
- Worker health check failures

---

Este documento de design técnico fornece uma visão completa da arquitetura, componentes, interfaces e estratégias de implementação para o **SaaS de Extração de Dados do Google Search**, incluindo diagramas high-level, pseudocódigo low-level e propriedades de correção testáveis.
