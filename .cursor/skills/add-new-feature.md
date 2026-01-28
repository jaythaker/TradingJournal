---
name: add-new-feature
description: "Add a new feature to TradingJournal following project patterns"
auto-activates:
  - "add new feature"
  - "implement feature"
  - "create endpoint"
  - "add api"
  - "new page"
---

# Skill: Add New Feature

## When to Use This Skill

This skill activates when you need to:
- Add a new API endpoint
- Create a new web page/view
- Add a new entity/model
- Implement end-to-end feature

## Prerequisites

- Understand the feature requirements
- Identify affected layers (API, Web, Database)

## Step-by-Step Workflow

### Step 1: Design the Feature

**Questions to answer:**
- What data needs to be stored? (Model)
- What operations are needed? (Service)
- What API endpoints? (Controller)
- What UI pages? (Views)

### Step 2: Create/Update Model

**In `TradingJournal.Api/Models/`:**

```csharp
[Table("new_entities")]
public class NewEntity
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [Column("userId")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
```

### Step 3: Update DbContext

**In `TradingJournal.Api/Data/ApplicationDbContext.cs`:**

```csharp
public DbSet<NewEntity> NewEntities { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Add cascade delete
    modelBuilder.Entity<NewEntity>()
        .HasOne(e => e.User)
        .WithMany()
        .OnDelete(DeleteBehavior.Cascade);
}
```

### Step 4: Create Migration

```bash
cd TradingJournal.Api
dotnet ef migrations add AddNewEntity
dotnet ef database update
```

### Step 5: Create Service Interface & Implementation

**`TradingJournal.Api/Services/INewEntityService.cs`:**

```csharp
public interface INewEntityService
{
    Task<IEnumerable<NewEntity>> GetByUserIdAsync(string userId);
    Task<NewEntity?> GetByIdAsync(string id, string userId);
    Task<NewEntity> CreateAsync(CreateNewEntityRequest request, string userId);
    Task<NewEntity> UpdateAsync(string id, UpdateNewEntityRequest request, string userId);
    Task DeleteAsync(string id, string userId);
}

public class CreateNewEntityRequest
{
    public string Name { get; set; } = string.Empty;
}
```

**`TradingJournal.Api/Services/NewEntityService.cs`:**

```csharp
public class NewEntityService : INewEntityService
{
    private readonly ApplicationDbContext _context;
    
    public NewEntityService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<IEnumerable<NewEntity>> GetByUserIdAsync(string userId)
    {
        return await _context.NewEntities
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }
    
    // ... other methods
}
```

### Step 6: Register Service in DI

**In `TradingJournal.Api/Program.cs`:**

```csharp
builder.Services.AddScoped<INewEntityService, NewEntityService>();
```

### Step 7: Create API Controller

**`TradingJournal.Api/Controllers/NewEntitiesController.cs`:**

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NewEntitiesController : ControllerBase
{
    private readonly INewEntityService _service;
    
    public NewEntitiesController(INewEntityService service)
    {
        _service = service;
    }
    
    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value!;
    
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entities = await _service.GetByUserIdAsync(GetUserId());
        return Ok(entities);
    }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNewEntityRequest request)
    {
        var entity = await _service.CreateAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }
    
    // ... other actions
}
```

### Step 8: Add Web DTO

**In `TradingJournal.Web/Models/Dtos.cs`:**

```csharp
public class NewEntityDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

### Step 9: Create Web Controller

**`TradingJournal.Web/Controllers/NewEntitiesController.cs`:**

```csharp
public class NewEntitiesController : Controller
{
    private readonly ApiClient _apiClient;
    
    public NewEntitiesController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }
    
    public async Task<IActionResult> Index()
    {
        var entities = await _apiClient.GetAsync<List<NewEntityDto>>("/api/newentities");
        return View(entities);
    }
    
    // ... other actions
}
```

### Step 10: Create Razor Views

**`TradingJournal.Web/Views/NewEntities/Index.cshtml`:**

```html
@model List<NewEntityDto>
@{
    ViewData["Title"] = "New Entities";
}

<h1><i class="bi bi-collection"></i> New Entities</h1>

<div class="card">
    <div class="card-body">
        <table class="table table-striped">
            <thead>
                <tr>
                    <th>Name</th>
                    <th>Created</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var entity in Model)
                {
                    <tr>
                        <td>@entity.Name</td>
                        <td>@entity.CreatedAt.ToString("g")</td>
                        <td>
                            <a asp-action="Edit" asp-route-id="@entity.Id" class="btn btn-sm btn-primary">Edit</a>
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
</div>
```

### Step 11: Add Navigation

**In `TradingJournal.Web/Views/Shared/_Layout.cshtml`:**

```html
<li class="nav-item">
    <a class="nav-link" asp-controller="NewEntities" asp-action="Index">
        <i class="bi bi-collection"></i> New Entities
    </a>
</li>
```

## Success Criteria

- ✅ Database table created via migration
- ✅ API endpoints return correct data
- ✅ Web pages display and edit data
- ✅ User isolation enforced (userId filtering)
- ✅ Navigation link visible when logged in
