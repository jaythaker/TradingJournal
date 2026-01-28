---
name: cli-commands
description: "Use the TradingJournal CLI tool (tj) for command-line operations"
auto-activates:
  - "cli command"
  - "tj command"
  - "command line"
  - "terminal import"
  - "shell script"
---

# Skill: CLI Commands

## When to Use This Skill

This skill activates when you need to:
- Use the `tj` command-line tool
- Import trades from terminal
- Automate TradingJournal operations
- Script trading journal workflows

## Prerequisites

- TradingJournal.Cli project built
- API running at http://localhost:5122
- Valid user account

## Step-by-Step Workflow

### Step 1: Build CLI Tool

```bash
cd TradingJournal.Cli
dotnet build
```

### Step 2: Set Environment Variables

```bash
# Set API URL
export TJ_API_URL=http://localhost:5122

# Option A: Login to get token
export TJ_TOKEN=$(tj login --email your@email.com --password yourpassword 2>/dev/null | grep -o 'eyJ[^"]*')

# Option B: Set token directly (from browser session)
export TJ_TOKEN="your-jwt-token-here"
```

### Step 3: Available Commands

#### Login
```bash
# Interactive login
tj login --email your@email.com --password yourpassword

# Returns JWT token on success
```

#### List Accounts
```bash
tj accounts

# Output:
# ID                                   | Name           | Currency
# abc123-def456-...                    | Fidelity IRA   | USD
```

#### Import Trades
```bash
# Import from CSV file
tj import trades --file ~/Downloads/fidelity-export.csv --account <account-id>

# Output:
# Imported: 15 trades
# Skipped: 3 duplicates
# Errors: 0
```

#### Import Dividends
```bash
tj import dividends --file ~/Downloads/dividends.csv --account <account-id>
```

#### View Summary
```bash
tj summary

# Output:
# Total P&L: $1,234.56
# Win Rate: 65.2%
# Total Trades: 45
```

#### View Summary by Account
```bash
tj summary --account <account-id>
```

## CLI Implementation

**Location:** `TradingJournal.Cli/Program.cs`

**Commands defined:**
```csharp
var rootCommand = new RootCommand("TradingJournal CLI");

// Login command
var loginCommand = new Command("login", "Login to get API token");
loginCommand.AddOption(new Option<string>("--email", "Email address"));
loginCommand.AddOption(new Option<string>("--password", "Password"));

// Import command with subcommands
var importCommand = new Command("import", "Import data");
var importTradesCommand = new Command("trades", "Import trades from CSV");
importTradesCommand.AddOption(new Option<string>("--file", "CSV file path"));
importTradesCommand.AddOption(new Option<string>("--account", "Account ID"));

// Accounts command
var accountsCommand = new Command("accounts", "List accounts");

// Summary command
var summaryCommand = new Command("summary", "Show trading summary");
```

## Automation Examples

### Daily Import Script

```bash
#!/bin/bash
# daily-import.sh

export TJ_API_URL=http://localhost:5122
export TJ_TOKEN="your-long-lived-token"

# Download latest from broker (example)
# curl -o /tmp/today.csv "https://broker.com/export?date=$(date +%Y-%m-%d)"

# Import trades
tj import trades --file /tmp/today.csv --account abc123

# Show updated summary
tj summary
```

### Batch Import

```bash
#!/bin/bash
# batch-import.sh

for file in ~/Downloads/fidelity-*.csv; do
    echo "Importing: $file"
    tj import trades --file "$file" --account abc123
done
```

## Common Issues and Solutions

**Issue:** `TJ_TOKEN` not set
**Solution:**
```bash
export TJ_TOKEN=$(tj login --email you@email.com --password pass123 | grep -o 'eyJ[^"]*')
```

**Issue:** API connection refused
**Solution:** Ensure API is running:
```bash
curl http://localhost:5122/api/health
```

**Issue:** Token expired
**Solution:** Re-login to get new token:
```bash
tj login --email you@email.com --password yourpass
```

**Issue:** Account not found
**Solution:** List accounts to get correct ID:
```bash
tj accounts
```

## Adding New CLI Command

1. Add command in `Program.cs`:
```csharp
var newCommand = new Command("newcmd", "Description");
newCommand.AddOption(new Option<string>("--param", "Parameter"));
newCommand.SetHandler(async (param) => {
    // Implementation
}, paramOption);
rootCommand.AddCommand(newCommand);
```

2. Rebuild:
```bash
dotnet build
```

## Success Criteria

- ✅ `tj --help` shows available commands
- ✅ `tj login` returns valid JWT token
- ✅ `tj accounts` lists user's accounts
- ✅ `tj import trades` imports CSV data
- ✅ `tj summary` shows trading statistics
