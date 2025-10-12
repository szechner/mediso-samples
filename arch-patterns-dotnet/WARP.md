# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

This is a **Mediso educational sample project** demonstrating modern architectural patterns in .NET applications. The project implements a **payment processing system** showcasing:

- Clean/Hexagonal Architecture
- CQRS (Command Query Responsibility Segregation)  
- Event Sourcing
- SAGA pattern with orchestration
- Resilience patterns
- Observability patterns

**Current Status**: ✅ **Solution and base projects created** - Clean Architecture foundation is implemented with all projects, dependencies, and NuGet packages configured. Ready for domain implementation.

## Architecture & Domains

The system is designed around **6 core domains**:

1. **Payments** - Event-sourced Payment aggregate with state machine
2. **Accounts** - Account balances and fund reservations
3. **Ledger** - Double-entry bookkeeping with journal entries
4. **Compliance** - AML/KYC screening and manual approval workflows
5. **Settlement** - Internal transfers and external gateway integration
6. **Notifications** - Multi-channel notifications (email/webhook/SSE)

## Technology Stack (Implemented)

- ✅ **.NET 8** - Primary framework
- ✅ **PostgreSQL + Marten 8.11.0** - Event store and projections
- ✅ **WolverineFx 4.12.2** - SAGA orchestration, message handling, CQRS
- ✅ **OpenTelemetry.Extensions.Hosting 1.12.0** - Distributed tracing
- ✅ **Serilog.AspNetCore 9.0.0** - Structured logging
- ✅ **Polly (via Marten)** - Resilience patterns (circuit breaker, retry)
- ✅ **NUnit + FakeItEasy 8.3.0** - Testing framework

## Key Architectural Patterns

### Event Sourcing & CQRS
- **Payment aggregate** is event-sourced with immutable events
- **Read models** (projections) for queries: PaymentDetails, AccountBalance
- **Command/Query separation** with different data models

### SAGA Pattern
Payment processing follows a multi-step orchestrated flow:
```
PaymentRequested → AMLPassed → FundsReserved → PaymentJournaled → PaymentSettled → PaymentNotified
```

### Compensation Patterns
Each step has defined compensations for failure scenarios:
- **AMLFlagged** → Manual review or decline
- **FundsReservationFailed** → PaymentDeclined
- **SettlementFailed** → Rollback journal + release reservation

## API Design

RESTful APIs following the contracts defined in `anl-attachment.md`:

- `POST /payments` - Create payment (returns 201 with paymentId)
- `GET /payments/{id}` - Get payment details with event history  
- `GET /accounts/{id}/balance` - Get account balance and reserved amounts

## Development Commands (Available Now)

✅ **Solution is buildable and ready for development:**

```powershell
# Build entire solution
dotnet build

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test projects
dotnet test ./tests/Mediso.PaymentSample.UnitTests/
dotnet test ./tests/Mediso.PaymentSample.IntegrationTests/

# Run application locally
dotnet run --project ./src/Mediso.PaymentSample.Api/

# Run with specific environment
dotnet run --project ./src/Mediso.PaymentSample.Api/ --environment Development

# Generate test data
dotnet run --project ./tools/Mediso.PaymentSample.DataSeeder/

# List all projects in solution
dotnet sln list

# Restore NuGet packages
dotnet restore
```

## Testing Strategy

The project follows a comprehensive testing approach:

- **Unit Tests** - Payment aggregate state machine, business rules validation
- **Integration Tests** - Marten event store, Wolverine SAGA orchestration, idempotency
- **Contract Tests** - External service mocks (Settlement/AML providers)  
- **End-to-End Tests** - Full payment flow including error scenarios

**Testing Framework**: NUnit + FakeItEasy (as per project rules)

## Performance Requirements

- **Latency**: P95 < 300ms for payment creation (excluding external calls)
- **Throughput**: Minimum 100 RPS
- **Availability**: 99.9% monthly uptime

## Key Business Rules

- **Idempotency**: All operations must be idempotent using idempotency keys
- **Double-entry bookkeeping**: All ledger entries must balance (debit = credit)
- **AML compliance**: Payments flagged for risk require manual approval
- **Fund reservation**: Must prevent overdrafts with atomic reservation logic

## Development Increments

The project follows an incremental development approach:

1. **I1** - Core Payments domain + API + projections
2. **I2** - Accounts with balance management + idempotency  
3. **I3** - Ledger with journal entries + compensations
4. **I4** - Settlement (internal) + Notifications
5. **I5** - AML screening (sync/async) + manual workflows
6. **I6** - Observability + security hardening
7. **I7** - Containerization + Kubernetes deployment

## External Integrations

- **Settlement Gateway** - REST/HTTP with request signing for external bank transfers
- **AML Provider** - Synchronous API for quick rules, webhook for deep analysis
- **Notification Channels** - SMTP/API for email, HMAC-signed webhooks, SSE for real-time UI

## Observability

- **Distributed Tracing**: OpenTelemetry with correlation IDs across all services
- **Metrics**: RPS, P95/P99 latencies, error rates, queue depths
- **Structured Logs**: Serilog with PII redaction, multiple log levels
- **Business Events**: All domain events logged for audit trail

## Project Structure (Implemented)

### Solution & Projects
```
Mediso.PaymentSample.sln - Main solution with 8 projects

src/
├── Mediso.PaymentSample.SharedKernel/     - DDD building blocks
├── Mediso.PaymentSample.Domain/           - Business logic & aggregates  
├── Mediso.PaymentSample.Application/      - CQRS handlers & use cases
├── Mediso.PaymentSample.Infrastructure/   - Data access & external services
└── Mediso.PaymentSample.Api/              - REST API controllers

tests/
├── Mediso.PaymentSample.UnitTests/        - Domain & application tests
└── Mediso.PaymentSample.IntegrationTests/ - API & infrastructure tests

tools/
└── Mediso.PaymentSample.DataSeeder/       - Test data generation
```

### Clean Architecture Dependencies
```
Api → Application + Infrastructure
Infrastructure → Application
Application → Domain  
Domain → SharedKernel
Tests → Source Projects
```

## Important Files

- ✅ `README.md` - Project overview with detailed structure (updated)
- ✅ `anl-attachment.md` - **Comprehensive analytical documentation** with detailed domain analysis, API contracts, event models, and acceptance criteria
- ✅ `WARP.md` - This file with development guidance
- ✅ `.gitignore` - Comprehensive ignore rules for .NET + JetBrains IDEs
- ✅ `.csproj` files - **All project definitions created** with proper references
- ✅ `Mediso.PaymentSample.sln` - Solution file with 8 projects
- 🔄 `appsettings.json` - Configuration (to be implemented with domain logic)

## Rules and Conventions

- Use **NUnit and FakeItEasy** for testing (per project rules)
- Follow **Clean Architecture** principles with clear layer separation
- Implement **event-sourced aggregates** for core business entities
- Ensure **idempotency** for all commands
- Use **structured logging** with correlation IDs
- Apply **resilience patterns** for external service calls