# Architektonické patterny v moderních aplikacích
[Confluence](https://confluence.mediso.cz/spaces/~szechner/pages/16944137/Architektonick%C3%A9+patterny+v+modern%C3%ADch+aplikac%C3%ADch)

## User Story
*"Uživatel chce poslat peníze ze svého účtu na účet kamaráda"*

## Architektura / patterny
- CQRS
- Mediator
- Event sourcing
- Resilience patterns

## Přílohy
- [Analytická příloha](./anl-attachment.md) - popis domén, procesů, požadavků, NFR, akceptačních kritérií
- [Warp](./WARP.md) - popis architektonických patternů, technologického stacku, CI/CD, deploymentu **[warp.dev](https://warp.dev)**

## Struktura projektu

### Řešení (Solution)
```
Mediso.PaymentSample.sln - Hlavní solution soubor s 8 projekty
```

### Zdrojové projekty (`src/`)

#### 🏛️ **Mediso.PaymentSample.SharedKernel**
- **Účel**: Sdílené abstrakce, base třídy a utility
- **Závislosti**: Žádné
- **Obsahuje**: DDD building blocks, common interfaces, výčty

#### 🎯 **Mediso.PaymentSample.Domain**  
- **Účel**: Doménová logika a business pravidla
- **Závislosti**: SharedKernel
- **Obsahuje**: 
  - Event-sourced Payment agregát
  - Domain events (PaymentRequested, FundsReserved, atd.)
  - Business výjimky a validace
  - Doménové služby

#### ⚡ **Mediso.PaymentSample.Application**
- **Účel**: Use cases a aplikační logika (CQRS)
- **Závislosti**: Domain
- **Technologie**: Wolverine pro CQRS
- **Obsahuje**:
  - Command handlers (CreatePaymentCommand, CancelPaymentCommand)
  - Query handlers (GetPaymentQuery, GetAccountBalanceQuery)
  - Read models / projekce (PaymentDetails, AccountBalance)
  - Application services

#### 🔧 **Mediso.PaymentSample.Infrastructure**
- **Účel**: Externí integrace a technická implementace
- **Závislosti**: Application
- **Technologie**: 
  - Marten (Event store + PostgreSQL)
  - WolverineFx (SAGA orchestrace)
  - Serilog (Strukturované logování)
  - OpenTelemetry (Distributed tracing)
- **Obsahuje**:
  - Event store implementace
  - Projekce do read modelů
  - Externí služby (AML, Settlement Gateway)
  - SAGA orchestrace pro payment workflow

#### 🌐 **Mediso.PaymentSample.Api**
- **Účel**: REST API endpoints
- **Závislosti**: Application, Infrastructure
- **Obsahuje**:
  - Controllers (`/payments`, `/accounts/{id}/balance`)
  - API middleware a konfigurace
  - OpenAPI/Swagger dokumentace

### Testovací projekty (`tests/`)

#### 🧪 **Mediso.PaymentSample.UnitTests**
- **Účel**: Unit testy pro Domain a Application vrstvu
- **Technologie**: NUnit + FakeItEasy
- **Testuje**: 
  - Payment agregát stavové automaty
  - Business pravidla a validace
  - Command/Query handlers

#### 🔗 **Mediso.PaymentSample.IntegrationTests**
- **Účel**: Integrační testy pro API a Infrastructure
- **Technologie**: NUnit + FakeItEasy
- **Testuje**:
  - End-to-end payment workflow
  - Marten event store integrace
  - Wolverine SAGA orchestrace
  - API kontrakty

### Nástroje (`tools/`)

#### 🗃️ **Mediso.PaymentSample.DataSeeder**
- **Účel**: Generování testovacích dat
- **Typ**: Console aplikace
- **Obsahuje**: Seed data pro účty, platby a test scénáře

### Architektonické vzory

#### Clean Architecture (Hexagonal)
```
Api → Application + Infrastructure
Infrastructure → Application  
Application → Domain
Domain → SharedKernel
```

#### CQRS + Event Sourcing
- **Commands**: Zapisují eventy do event store
- **Queries**: Čtou z materialized views (projekce)
- **Events**: Immutable, definované v Domain vrstvě

#### SAGA Pattern
- **Orchestrace**: Wolverine řídí multi-step procesy
- **Kompenzace**: Automatické rollback při selháních
- **State machine**: Payment prochází definovanými stavy

### Technologický stack
- **.NET 8** - Hlavní framework
- **PostgreSQL + Marten** - Event store a document DB
- **WolverineFx** - Message handling, SAGA, CQRS
- **Serilog** - Strukturované logování
- **OpenTelemetry** - Distributed tracing
- **NUnit + FakeItEasy** - Testování

### Spuštění
```bash
# Build celé solution
dotnet build

# Spuštění API
dotnet run --project src/Mediso.PaymentSample.Api

# Spuštění testů
dotnet test

# Generování test dat
dotnet run --project tools/Mediso.PaymentSample.DataSeeder
```
