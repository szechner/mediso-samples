# Mediso Payment Sample DataSeeder

🌱 **PostgreSQL 18 + Marten Event Store Migration and Data Seeding Tool**

## Overview

The DataSeeder is a console application designed for database migration and realistic sample data generation for the Mediso Payment Sample application. It leverages **PostgreSQL 18** with enhanced monitoring capabilities and **Marten** as an event store.

## Features

### 🔧 Database Migration
- **PostgreSQL 18 Schema Initialization**: Creates database schema with Marten event store tables
- **Enhanced Monitoring**: Applies PostgreSQL 18 monitoring extensions (`pg_stat_statements`, `pgstattuple`, `pg_buffercache`)
- **Health Checks**: Comprehensive database health validation
- **Reset Capability**: Safe database reset with confirmation prompts

### 🌱 Realistic Data Seeding
- **Configurable Volume**: Customizable number of customers, merchants, and payments
- **Business Logic Simulation**: Realistic payment scenarios including:
  - **AML (Anti-Money Laundering) checks**: Simulated compliance validations
  - **Fund Reservations**: Realistic payment processing flows  
  - **Payment Settlements**: Completed payment scenarios
  - **Failures and Cancellations**: Error condition simulations
  - **Time-based Data**: Historical payment data across configurable time ranges

### 📊 Statistics and Monitoring
- **Seeding Statistics**: Detailed metrics on generated data
- **Database Health Reports**: PostgreSQL 18 monitoring insights
- **Activity Tracing**: OpenTelemetry-compatible tracing
- **Comprehensive Logging**: Structured logging with Serilog

## Quick Start

### Prerequisites
- **.NET 8.0** SDK or later
- **PostgreSQL 18** instance (can be started using `.\scripts\start-postgresql18.ps1`)
- **PowerShell** (for convenience scripts)

### Using PowerShell Script (Recommended)
```powershell
# Show help
.\scripts\run-dataseeder.ps1

# Initialize database with PostgreSQL 18 enhancements
.\scripts\run-dataseeder.ps1 migrate

# Generate sample data (basic)
.\scripts\run-dataseeder.ps1 seed

# Generate custom volume of data
.\scripts\run-dataseeder.ps1 seed -Payments 500 -Customers 100 -Merchants 50 -Seed 12345

# Check database health and statistics
.\scripts\run-dataseeder.ps1 status

# Reset database (WARNING: destructive)
.\scripts\run-dataseeder.ps1 reset -Confirm
```

### Using .NET CLI Directly
```bash
# Navigate to DataSeeder project
cd src/Mediso.PaymentSample.DataSeeder

# Build the project
dotnet build --configuration Release

# Run commands
dotnet run --configuration Release -- migrate
dotnet run --configuration Release -- seed --payments 100 --customers 50
dotnet run --configuration Release -- status
```

## Commands

### `migrate`
Initializes the database schema and applies PostgreSQL 18 enhancements.

**What it does:**
- Creates database if configured to do so
- Applies Marten event store schema
- Enables PostgreSQL 18 monitoring extensions
- Creates monitoring functions and views

### `seed`
Generates and inserts realistic sample data.

**Options:**
- `--payments, -p`: Number of payments to generate (default: 100)
- `--customers`: Number of customers to generate (default: 50) 
- `--merchants`: Number of merchants to generate (default: 20)
- `--timerange, -t`: Time range in months for historical data (default: 6)
- `--realistic, -r`: Enable realistic business scenarios (default: true)
- `--seed, -s`: Random seed for reproducible data generation

**Realistic Scenarios Include:**
- Payment processing with proper state transitions
- AML flag simulations on high-value transactions
- Fund reservation and settlement flows
- Payment failures and cancellations
- Merchant and customer relationship modeling

### `status`
Checks database health and provides seeding statistics.

**Reports:**
- PostgreSQL version and configuration
- Schema and table existence
- Monitoring extension status
- Event store statistics
- Data volume metrics

### `reset`
**⚠️ DESTRUCTIVE OPERATION** - Completely resets the database.

**Options:**
- `--confirm, -y`: Skip confirmation prompt

## Configuration

### `appsettings.json`
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=mediso_payment_sample;Username=payment_user;Password=payment_pass;"
  },
  "DataSeederSettings": {
    "SampleDataSettings": {
      "PaymentCount": 100,
      "CustomerCount": 50,
      "MerchantCount": 20,
      "TimeRangeMonths": 6,
      "EnableRealisticScenarios": true
    },
    "MigrationSettings": {
      "AutoCreateDatabase": true,
      "AutoCreateSchema": true,
      "ResetDatabase": false,
      "MigrationTimeoutSeconds": 300
    }
  }
}
```

### Environment Variables
- `DATASEEDER_ConnectionStrings__Default`: Override connection string
- `ASPNETCORE_ENVIRONMENT`: Set environment (Development, Staging, Production)
- `DATASEEDER_Verbose`: Enable verbose logging

## Data Model

The DataSeeder generates realistic data following the payment domain model:

### Customers
- Personal information with realistic names and addresses
- KYC (Know Your Customer) status simulation
- Risk profiles for AML scenarios

### Merchants  
- Business information and categorization
- Payment processing capabilities
- Geographic distribution

### Payments
- Complete payment lifecycle events
- Realistic amounts and currencies  
- Proper state transitions (Initiated → Reserved → Settled/Failed/Cancelled)
- Time-distributed historical data

## Monitoring Integration

The DataSeeder integrates with the PostgreSQL 18 monitoring infrastructure:

### Available Monitoring Views
- `monitoring.event_store_stats`: Event store performance metrics
- `monitoring.slow_queries`: Query performance analysis  
- `monitoring.connection_stats`: Database connection metrics
- `monitoring.cache_hit_ratios`: Buffer cache effectiveness

### Functions
- `monitoring.get_table_sizes()`: Database table size analysis
- `monitoring.get_index_usage()`: Index utilization statistics

## Troubleshooting

### Common Issues

**Build Errors**
```bash
# Clean and restore packages
dotnet clean
dotnet restore
dotnet build
```

**Connection Issues**
- Ensure PostgreSQL 18 is running (`.\scripts\start-postgresql18.ps1`)
- Verify connection string in `appsettings.json`
- Check network connectivity and firewall settings

**Permission Issues**
- Ensure database user has schema creation permissions
- Verify monitoring extension installation privileges

### Logs
The DataSeeder creates structured logs in:
- **Console Output**: Real-time progress and status  
- **File Logs**: `logs/dataseeder-{date}.log`

## Development

### Project Structure
```
src/Mediso.PaymentSample.DataSeeder/
├── Program.cs                          # Console app entry point
├── Configuration/                      # Configuration models
├── Services/
│   ├── IMigrationService.cs           # Migration interface
│   ├── PostgreSql18MigrationService.cs # PostgreSQL 18 migration implementation
│   ├── ISampleDataService.cs          # Data generation interface
│   └── BogusSampleDataService.cs      # Bogus-based data generation
├── appsettings.json                   # Base configuration
├── appsettings.Development.json       # Development environment config
└── Scripts/                          # SQL enhancement scripts
```

### Technologies Used
- **.NET 8.0**: Modern C# runtime
- **System.CommandLine**: Command-line interface framework
- **Marten**: Event store and document database
- **Bogus**: Realistic fake data generation
- **Serilog**: Structured logging
- **Npgsql**: PostgreSQL data provider
- **Microsoft.Extensions.Hosting**: Dependency injection and configuration

### Contributing
When adding new features:
1. Follow existing patterns for command handlers
2. Add appropriate logging and activity tracing
3. Update configuration models as needed
4. Test with both PowerShell script and direct CLI usage
5. Update documentation

## Related Documentation

- [Infrastructure README](../Infrastructure/README.md): PostgreSQL 18 setup and configuration
- [Docker Compose Setup](../../docs/docker-setup.md): Container-based PostgreSQL 18 environment

---

**🎯 Next Steps**: After running DataSeeder, use the generated data to test the payment processing APIs and explore the PostgreSQL 18 monitoring capabilities through PgAdmin at http://localhost:8080.