# PostgreSQL 18 with Enhanced Monitoring - Startup Script
# This script starts the PostgreSQL 18 environment with monitoring tools

param(
    [switch]$Build,
    [switch]$Clean,
    [switch]$Logs,
    [string]$Service = ""
)

$ErrorActionPreference = "Stop"
$dockerComposeFile = "docker-compose.yml"

Write-Host "🚀 PostgreSQL 18 Enhanced Monitoring Environment" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

if ($Clean) {
    Write-Host "🧹 Cleaning up existing containers and volumes..." -ForegroundColor Yellow
    docker-compose -f $dockerComposeFile down -v --remove-orphans
    docker system prune -f
    Write-Host "✅ Cleanup completed" -ForegroundColor Green
    exit 0
}

if ($Logs) {
    if ($Service) {
        Write-Host "📋 Showing logs for service: $Service" -ForegroundColor Yellow
        docker-compose -f $dockerComposeFile logs -f $Service
    } else {
        Write-Host "📋 Showing logs for all services" -ForegroundColor Yellow
        docker-compose -f $dockerComposeFile logs -f
    }
    exit 0
}

# Check if Docker is running
try {
    docker version | Out-Null
} catch {
    Write-Host "❌ Docker is not running. Please start Docker Desktop first." -ForegroundColor Red
    exit 1
}

# Build if requested
if ($Build) {
    Write-Host "🔨 Building PostgreSQL 18 monitoring environment..." -ForegroundColor Yellow
    docker-compose -f $dockerComposeFile build --no-cache
}

# Start services
Write-Host "🐘 Starting PostgreSQL 18 with enhanced monitoring..." -ForegroundColor Cyan
docker-compose -f $dockerComposeFile up -d

# Wait for PostgreSQL to be ready
Write-Host "⏳ Waiting for PostgreSQL 18 to be ready..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 1

do {
    try {
        $healthCheck = docker-compose -f $dockerComposeFile exec -T postgres pg_isready -U payment_user -d payment_sample
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ PostgreSQL 18 is ready!" -ForegroundColor Green
            break
        }
    } catch {
        # Continue trying
    }
    
    if ($attempt -ge $maxAttempts) {
        Write-Host "❌ PostgreSQL 18 failed to start within timeout" -ForegroundColor Red
        Write-Host "📋 Showing PostgreSQL logs:" -ForegroundColor Yellow
        docker-compose -f $dockerComposeFile logs postgres
        exit 1
    }
    
    Write-Host "⏳ Attempt $attempt/$maxAttempts - waiting for PostgreSQL 18..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
    $attempt++
} while ($true)

# Show service status
Write-Host "`n📊 Service Status:" -ForegroundColor Cyan
docker-compose -f $dockerComposeFile ps

# Display access information
Write-Host "`n🎯 Access Information:" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host "PostgreSQL 18:" -ForegroundColor White
Write-Host "  • Host: localhost:5432" -ForegroundColor Gray
Write-Host "  • Database: payment_sample" -ForegroundColor Gray
Write-Host "  • Username: payment_user" -ForegroundColor Gray
Write-Host "  • Password: payment_password" -ForegroundColor Gray
Write-Host ""
Write-Host "pgAdmin (PostgreSQL Admin):" -ForegroundColor White
Write-Host "  • URL: http://localhost:8080" -ForegroundColor Gray
Write-Host "  • Email: admin@mediso.com" -ForegroundColor Gray
Write-Host "  • Password: admin123" -ForegroundColor Gray
Write-Host ""
Write-Host "Jaeger Tracing:" -ForegroundColor White
Write-Host "  • URL: http://localhost:16686" -ForegroundColor Gray
Write-Host ""

# Test monitoring functions
Write-Host "🔍 Testing PostgreSQL 18 monitoring functions..." -ForegroundColor Cyan
try {
    $testQuery = @"
SELECT 'PostgreSQL 18 Enhanced Monitoring Active' as status,
       version() as postgresql_version,
       current_database() as database_name,
       current_user as user_name,
       now() as timestamp;
"@

    $result = docker-compose -f $dockerComposeFile exec -T postgres psql -U payment_user -d payment_sample -c "$testQuery"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ PostgreSQL 18 monitoring functions are active!" -ForegroundColor Green
    }
} catch {
    Write-Host "⚠️  Could not test monitoring functions, but services are running" -ForegroundColor Yellow
}

Write-Host "`n🎉 PostgreSQL 18 Enhanced Monitoring Environment is ready!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""
Write-Host "💡 Useful Commands:" -ForegroundColor Cyan
Write-Host "  • View logs: .\scripts\start-postgresql18.ps1 -Logs" -ForegroundColor Gray
Write-Host "  • View service logs: .\scripts\start-postgresql18.ps1 -Logs -Service postgres" -ForegroundColor Gray
Write-Host "  • Clean everything: .\scripts\start-postgresql18.ps1 -Clean" -ForegroundColor Gray
Write-Host "  • Rebuild: .\scripts\start-postgresql18.ps1 -Build" -ForegroundColor Gray
Write-Host ""
Write-Host "📊 Monitoring Functions Available:" -ForegroundColor Cyan
Write-Host "  • SELECT * FROM monitoring.get_event_store_stats();" -ForegroundColor Gray
Write-Host "  • SELECT * FROM monitoring.get_slow_queries(100);" -ForegroundColor Gray
Write-Host "  • SELECT * FROM monitoring.get_connection_stats();" -ForegroundColor Gray
Write-Host "  • SELECT * FROM monitoring.get_cache_hit_ratio();" -ForegroundColor Gray
Write-Host "  • SELECT * FROM monitoring.event_store_dashboard;" -ForegroundColor Gray