# DataSeeder Runner Script
# Mediso Payment Sample - PostgreSQL 18 + Marten Event Store
param(
    [Parameter(Position = 0)]
    [ValidateSet("migrate", "seed", "status", "reset", "help", "")]
    [string]$Command = "help",
    
    [string]$Environment = "Development",
    [string]$ConnectionString,
    [switch]$VerboseLogging,
    
    # Seed command options
    [int]$Payments = 100,
    [int]$Customers = 50,
    [int]$Merchants = 20,
    [int]$TimeRange = 6,
    [switch]$NoRealistic,
    [int]$Seed,
    
    # Reset command options
    [switch]$Confirm
)

# Colors and formatting
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Show-Help {
    Write-ColorOutput "🌱 Mediso Payment Sample Data Seeder" $InfoColor
    Write-ColorOutput "PostgreSQL 18 + Marten Event Store Migration and Seeding Tool" $InfoColor
    Write-Host ""
    
    Write-ColorOutput "USAGE:" $InfoColor
    Write-Host "  .\run-dataseeder.ps1 <command> [options]"
    Write-Host ""
    
    Write-ColorOutput "COMMANDS:" $InfoColor
    Write-Host "  migrate     🔧 Initialize database schema and apply PostgreSQL 18 enhancements"
    Write-Host "  seed        🌱 Generate and seed sample data"
    Write-Host "  status      📊 Check database health and seeding statistics"
    Write-Host "  reset       🗑️  Reset database (WARNING: destructive operation)"
    Write-Host "  help        ❓ Show this help message"
    Write-Host ""
    
    Write-ColorOutput "GLOBAL OPTIONS:" $InfoColor
    Write-Host "  -Environment    Environment (Development, Staging, Production) [default: Development]"
    Write-Host "  -ConnectionString    Override connection string from configuration"
    Write-Host "  -VerboseLogging Enable verbose logging"
    Write-Host ""
    
    Write-ColorOutput "SEED OPTIONS:" $InfoColor
    Write-Host "  -Payments       Number of payments to generate [default: 100]"
    Write-Host "  -Customers      Number of customers to generate [default: 50]"
    Write-Host "  -Merchants      Number of merchants to generate [default: 20]"
    Write-Host "  -TimeRange      Time range in months for historical data [default: 6]"
    Write-Host "  -NoRealistic    Disable realistic business scenarios"
    Write-Host "  -Seed           Random seed for reproducible data generation"
    Write-Host ""
    
    Write-ColorOutput "RESET OPTIONS:" $InfoColor
    Write-Host "  -Confirm        Confirm database reset without prompt"
    Write-Host ""
    
    Write-ColorOutput "EXAMPLES:" $InfoColor
    Write-Host "  # Initialize database"
    Write-Host "  .\run-dataseeder.ps1 migrate"
    Write-Host ""
    Write-Host "  # Seed sample data (basic)"
    Write-Host "  .\run-dataseeder.ps1 seed"
    Write-Host ""
    Write-Host "  # Seed with custom options"
    Write-Host "  .\run-dataseeder.ps1 seed -Payments 500 -Customers 100 -Merchants 50 -Seed 12345"
    Write-Host ""
    Write-Host "  # Check status"
    Write-Host "  .\run-dataseeder.ps1 status"
    Write-Host ""
    Write-Host "  # Reset database (with confirmation)"
    Write-Host "  .\run-dataseeder.ps1 reset -Confirm"
    Write-Host ""
    Write-Host "  # Use different environment"
    Write-Host "  .\run-dataseeder.ps1 migrate -Environment Staging -VerboseLogging"
}

function Build-DataSeeder {
    Write-ColorOutput "🔨 Building DataSeeder..." $InfoColor
    
    $projectPath = "F:\ProjektyMediso\mediso-samples\arch-patterns-dotnet\src\Mediso.PaymentSample.DataSeeder"
    
    try {
        Push-Location $projectPath
        $buildResult = dotnet build --configuration Release --no-restore 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "❌ Build failed!" $ErrorColor
            Write-Host $buildResult
            return $false
        }
        
        Write-ColorOutput "✅ Build successful!" $SuccessColor
        return $true
    }
    catch {
        Write-ColorOutput "❌ Build error: $_" $ErrorColor
        return $false
    }
    finally {
        Pop-Location
    }
}

function Invoke-DataSeeder {
    param([string[]]$Arguments)
    
    $projectPath = "F:\ProjektyMediso\mediso-samples\arch-patterns-dotnet\src\Mediso.PaymentSample.DataSeeder"
    
    try {
        Push-Location $projectPath
        
        Write-ColorOutput "🚀 Running DataSeeder with arguments: $($Arguments -join ' ')" $InfoColor
        Write-Host ""
        
        # Run the application
        & dotnet run --configuration Release --no-build -- @Arguments
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-ColorOutput "✅ DataSeeder completed successfully!" $SuccessColor
        } else {
            Write-Host ""
            Write-ColorOutput "❌ DataSeeder failed with exit code $LASTEXITCODE" $ErrorColor
        }
        
        return $LASTEXITCODE
    }
    catch {
        Write-ColorOutput "❌ Execution error: $_" $ErrorColor
        return 1
    }
    finally {
        Pop-Location
    }
}

# Main execution
try {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $rootDir = Split-Path -Parent $scriptDir
    
    # Change to project root directory
    Push-Location $rootDir
    
    # Handle help or empty command
    if ($Command -eq "help" -or $Command -eq "") {
        Show-Help
        exit 0
    }
    
    # Build the project first
    if (-not (Build-DataSeeder)) {
        exit 1
    }
    
    # Build command arguments
    $args = @($Command)
    
    # Add global options
    if ($Environment -ne "Development") {
        $args += @("--environment", $Environment)
    }
    
    if ($ConnectionString) {
        $args += @("--connection", $ConnectionString)
    }
    
    if ($VerboseLogging) {
        $args += "--verbose"
    }
    
    # Add command-specific options
    switch ($Command) {
        "seed" {
            if ($Payments -ne 100) {
                $args += @("--payments", $Payments)
            }
            if ($Customers -ne 50) {
                $args += @("--customers", $Customers)
            }
            if ($Merchants -ne 20) {
                $args += @("--merchants", $Merchants)
            }
            if ($TimeRange -ne 6) {
                $args += @("--timerange", $TimeRange)
            }
            if ($NoRealistic) {
                $args += @("--realistic", "false")
            }
            if ($Seed) {
                $args += @("--seed", $Seed)
            }
        }
        "reset" {
            if ($Confirm) {
                $args += "--confirm"
            }
        }
    }
    
    # Execute the DataSeeder
    $exitCode = Invoke-DataSeeder $args
    exit $exitCode
}
catch {
    Write-ColorOutput "❌ Script error: $_" $ErrorColor
    exit 1
}
finally {
    Pop-Location
}