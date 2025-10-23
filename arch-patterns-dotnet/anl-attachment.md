# Platební aplikace (školící sample) - Analytická příloha

> Verze: 1.0 • Datum: 27. 9. 2025 • Autor: Mediso - Štěpán Zechner 

------------------------------------------------------------------------
## 1) Účel dokumentu

Tento dokument slouží jako analytická příloha k ukázkové platební
aplikaci. Cílem je popsat kontext, domény, procesy, požadavky, rozhraní
a akceptační kritéria tak, aby vývojový tým mohl konzistentně
implementovat řešení a aby materiál byl použitelný v rámci vzdělávacích
webinářů.

------------------------------------------------------------------------
## 2) Kontext a cíle

**Kontext:** Ukázkový projekt pro vzdělávání - demonstruje
Clean/Hexagonal architekturu, CQRS, Event Sourcing, resilience a
observability.

**Technologický rámec:** .NET 8, PostgreSQL + Marten, Wolverine
(SAGA/orchestrace), WolverineFx (CQRS), OpenTelemetry, Serilog, Polly.

**Hlavní cíle:** 
1. Ukázat dobře strukturovanou doménu plateb a souvisejících modulů
2. Zdokumentovat „happy path" i chybové větve s kompenzacemi
3. Předvést event‑sourced agregát Payment a projekce pro čtecí modely
4. Vytvořit jasné kontrakty API a eventů pro integrace
5. Stanovit měřitelné NFR a akceptační kritéria

------------------------------------------------------------------------
## 3) Stakeholdeři a persony

-   **Product Owner (PO):** definuje požadavky, prioritizuje backlog,
    schvaluje akceptační kritéria.
-   **Tech Lead / Architekt:** navrhuje architekturu, přezkoumává
    kvalitu a NFR.
-   **Vývojář (BE/FE):** implementace use‑casů a API.
-   **QA / Tester:** definuje scénáře, testuje funkční i nefunkční
    požadavky.
-   **Compliance Officer:** pravidla AML, limity, audit.
-   **Ops / DevOps:** CI/CD, provoz, monitoring.

**Hlavní persony (uživatelé systému):**
- **P1 - Zákazník:** zadává platby (web/mobile), sleduje stav.
- **P2 - Operátor podpory:** nahlíží na stav plateb, provádí storna/rušení (je‑li to možné).
- **P3 - Risk/Compliance:** sleduje AML flagy a rozhoduje o uvolnění/stopce.

------------------------------------------------------------------------
## 4) Domény a hranice systému

**Domény:**
- **Payments**: iniciace a životní cyklus platby (agregát Payment, události, stavový automat)
- **Accounts**: účty a zůstatky, rezervace prostředků
- **Ledger**: účetní zápisy (journal), párování debet/kredit
- **Compliance**: AML/Screening, pravidla a manuální schválení
- **Settlement**: vypořádání (vnitřní/externí), clearing
- **Notifications**: publikace stavů (e‑mail/webhook/SSE) a uživatelská oznámení

**Hranice systému:**
- **Externí brány/Bankovní API** (faktické vypořádání)
- **KYC/AML služby** (screening)
- **Notifikační kanály** (e‑mail/sms/webhook)

------------------------------------------------------------------------
## 5) Pojmy (slovníček stručně)

-   **Payment** - požadavek na převod finančních prostředků.
-   **Reservation (Hold)** - dočasné zablokování částky na účtu plátce.
-   **Settlement** - finální vypořádání transakce (zápis do ledgeru
    a/nebo externí clearing).
-   **Reversal/Compensation** - kompenzační krok při chybě.
-   **Read Model** - projekce pro rychlé čtení (např. PaymentDetails,
    AccountBalance).

------------------------------------------------------------------------
## 6) Use‑casy (funkční scénáře)

**UC‑1: Vytvoření platby**

- P1 zadá částku, měnu, účet plátce, účet příjemce, referenci.
- Systém validuje, uloží **PaymentRequested**, spustí AML screening.

**UC‑2: AML screening**

- Compliance modul spustí automatická pravidla; může vzniknout **PaymentFlagged**.
- Je‑li riziko nízké, pokračuje se k rezervaci prostředků; jinak čeká na manuální rozhodnutí.

**UC‑3: Rezervace prostředků**

- Accounts provede **ReserveFunds** (hold). Vznikne **FundsReserved** nebo **FundsReservationFailed**.

**UC‑4: Zaúčtování (Journal)**

- Ledger knihuje debit/kredit, vznikne **PaymentJournaled**.

**UC‑5: Vypořádání**

- Settlement provede vnitřní převod nebo volá externí bránu. Vznikne **PaymentSettled**.

**UC‑6: Notifikace**

- Notifications publikuje stav (SSE/webhook/e‑mail) – **PaymentNotified**.

**UC‑7: Storno/Cancel**

- Dokud není Settlement zahájen, P2 může zrušit platbu – **PaymentCancelled**.

**UC‑8: Manuální uvolnění/stopka**

- P3 může platbu uvolnit nebo definitivně zamítnout – **PaymentReleased** / **PaymentDeclined**.

------------------------------------------------------------------------

## 7) Procesní toky

**Happy path:**

```
PaymentRequested → AMLPassed → FundsReserved → PaymentJournaled → PaymentSettled → PaymentNotified
```

**Výjimky a kompenzace (příklady):**

- **AMLFlagged:** čeká na rozhodnutí → Declined (→ uvolnit rezervace, pokud existují) / Released (pokračuj).
- **FundsReservationFailed:** PaymentDeclined + notifikace zákazníkovi.
- **SettlementFailed:** kompenzace (storno journalu, uvolnění rezervace) + PaymentFailed.

------------------------------------------------------------------------

## 8) Funkční požadavky (FR)

- **FR‑1:** Systém musí umožnit vytvořit platbu s povinnými údaji: amount, currency, payerAccountId, payeeAccountId, reference.
- **FR‑2:** Systém provede AML screening synchronně (rychlá pravidla) a/nebo asynchronně (hloubková kontrola).
- **FR‑3:** Při pozitivním AML výsledku musí být platba pozastavena do manuálního rozhodnutí.
- **FR‑4:** Rezervace prostředků musí být idempotentní (opakovaný požadavek nesmí vytvořit duplicitní hold).
- **FR‑5:** Ledger musí držet vyvážené zápisy (debet=kredit) a auditní stopu.
- **FR‑6:** Settlement podporuje vnitřní transfer i volání externí brány.
- **FR‑7:** Uživatel může dotazem získat stav platby a historii událostí.
- **FR‑8:** Notifikace o změně stavu musí být doručeny spolehlivě (retry s backoff).

------------------------------------------------------------------------

## 9) Nefunkční požadavky (NFR)

- **NFR‑1 Výkon:** 95. percentil latence vytvoření platby < 300 ms (bez externí brány). Průchodnost min. 100 RPS.
- **NFR‑2 Dostupnost:** 99,9 % měsíčně pro API čtení a zadání plateb.
- **NFR‑3 Odolnost:** idempotence, retry s exponenciálním backoff, circuit‑breaker při volání externích služeb.
- **NFR‑4 Auditovatelnost:** úplná sledovatelnost (tracing), nezměnitelné eventy, časová razítka.
- **NFR‑5 Pozorovatelnost:** tracing, metriky (p99 latence, chybovost), strukturované logy.

------------------------------------------------------------------------

## 10) Datový a doménový model (přehled)

**Agregát Payment (event‑sourced):**

- Klíčové atributy: PaymentId, Amount, Currency, PayerAccountId, PayeeAccountId, Reference, State, Flags.
- Události (viz §12): PaymentRequested, AMLPassed/Flagged, FundsReserved/ReservationFailed, PaymentJournaled, PaymentSettled, PaymentCancelled, PaymentDeclined, PaymentFailed.

**Projekce (read models):**

- **PaymentDetails:** { PaymentId, CreatedAt, Amount, Currency, Payer, Payee, State, LastEventAt, Flags }
- **AccountBalance:** { AccountId, Balance, Reserved }

**Accounts:** Account { AccountId, Owner, Currency, Balance, ReservedBalance }

**Ledger:** JournalEntry { EntryId, PaymentId, DebitAccountId, CreditAccountId, Amount, Currency, Timestamp }

------------------------------------------------------------------------

## 11) API kontrakty (REST, výňatek)

**POST /payments** – vytvoření platby

```json
{
  "amount": 1250.00,
  "currency": "CZK",
  "payerAccountId": "acc-001",
  "payeeAccountId": "acc-999",
  "reference": "INV-2025-0912"
}
```

**201 Created**

```json
{ "paymentId": "pay-123", "state": "Requested" }
```

**GET /payments/{id}** – detail

```json
{
  "paymentId": "pay-123",
  "state": "Settled",
  "amount": 1250.00,
  "currency": "CZK",
  "events": [ {"type":"PaymentRequested","at":"2025-09-27T08:15:23Z"}, {"type":"FundsReserved","at":"2025-09-27T08:15:25Z"}, {"type":"PaymentSettled","at":"2025-09-27]T08:15:30Z"} ]
}
```

**GET /accounts/{id}/balance** – zůstatek

```json
{ "accountId": "acc-001", "balance": 100000.00, "reserved": 2500.00 }
```

------------------------------------------------------------------------

## 12) Model událostí (Event Contracts)

- **PaymentRequested** { paymentId, amount, currency, payerAccountId, payeeAccountId, reference, requestedAt }
- **AMLPassed** { paymentId, checkedAt, ruleSetVersion }
- **PaymentFlagged** { paymentId, reason, severity, checkedAt }
- **FundsReserved** { paymentId, accountId, amount, reservedAt, reservationId }
- **FundsReservationFailed** { paymentId, accountId, reason, failedAt }
- **PaymentJournaled** { paymentId, entries:[{debitAccountId, creditAccountId, amount, currency}], journaledAt }
- **PaymentSettled** { paymentId, settledAt, channel, externalRef }
- **PaymentCancelled** { paymentId, cancelledAt, by }
- **PaymentDeclined** { paymentId, declinedAt, reason }
- **PaymentFailed** { paymentId, failedAt, reason }
- **PaymentNotified** { paymentId, channel, deliveredAt }

------------------------------------------------------------------------

## 13) Stavový automat platby (zjednodušeně)

```
Requested → (Flagged → Released | Declined) → AMLPassed → Reserved → Journaled → Settled → Notified
                                 ↘ ReservationFailed → Declined
                             ↘ SettlementFailed → Failed → (kompenzace)
```

------------------------------------------------------------------------

## 14) Validace & business pravidla (příklady)

- **Amount > 0**, měna v sadě podporovaných ISO 4217.
- **Payer ≠ Payee** (pokud není interní převod na vlastní sub‑účet).
- **Denní limit** na účet a souhrnný limit na zákazníka (AML/anti‑fraud pravidla).
- **Idempotence key** pro vytvoření platby (např. `Idempotency-Key` header) – ochrana proti duplicitám.

------------------------------------------------------------------------

## 15) Bezpečnost

- Audit logy všech rozhodnutí Compliance + přístupů k detailu platby.

------------------------------------------------------------------------

## 16) Observability

- **Tracing:** korelační `traceId` přes všechny služby (OpenTelemetry).
- **Metriky:** RPS, p95/p99 latence, chybovost, fronty retry.
- **Logy:** strukturované (Serilog), několik úrovní (Debug → Fatal), PII redakce.

------------------------------------------------------------------------

## 17) Integrace a rozhraní

- **Externí Settlement Gateway:** REST/HTTP s podpisem požadavků.
- **AML provider:** sync API pro rychlé pravidlo, async webhook pro hlubokou kontrolu.
- **Notifications:** e‑mail (SMTP/API), webhook (HMAC podpis), SSE pro UI.

------------------------------------------------------------------------

## 18) Testovací strategie

- **Unit:** stavové automaty agregátu Payment, validace pravidel.
- **Integrační:** Marten (event store + projekce), Wolverine (SAGA a outbox), idempotence rezervace.
- **Contract:** fake Settlement/AML, ověření kontraktů a chybových kódů.
- **E2E:** happy path + vybrané chybové scénáře, měření latence.

------------------------------------------------------------------------

## 19) Akceptační kritéria (výběr)

- **AC‑UC1:** Po `POST /payments` vrátit `201` s `paymentId`; `GET /payments/{id}` do 100 ms vrátí stav `Requested`.
- **AC‑UC3:** Rezervace je idempotentní – opakovaný požadavek nezmění `reserved` částku.
- **AC‑UC5:** Po úspěšném vypořádání je v Ledgeru párový zápis a stav `Settled`.
- **AC‑NFR:** p95 latence `POST /payments` (bez externích volání) < 300 ms při 100 RPS.

------------------------------------------------------------------------

## 20) Roadmapa inkrementů

1. **I1 – Základ Payments + API + projekce PaymentDetails**
2. **I2 – Accounts (balance, hold) + idempotence**
3. **I3 – Ledger (journal) + kompenzace**
4. **I4 – Settlement (interní) + Notifications**
5. **I5 – AML (sync/async) + manuální rozhodnutí**
6. **I6 – Observability + bezpečnost + hardening**
7. **I7 – Docker/K8s provoz, škálování**
8. **I8 – Blockchain audit (rozšíření)**
9. **I9 – AI asistence (testy, log analýza)**

------------------------------------------------------------------------

## 21) Rizika a mitigace

- **Externí brány nestabilní:** circuit‑breaker, fallback, dead‑letter + retry.
- **Konzistence read modelů:** projekční lag – informovat UI o „eventual consistency“.
- **AML false‑positives:** manuální review, auditovatelná rozhodnutí.
- **Locky/konkurence v rezervacích:** optimistické zámky + idempotence klíče.

------------------------------------------------------------------------

## 22) Slovníček pojmů (rozšířený)

- **SAGA:** orchestrace více kroků se stavem a kompenzacemi.
- **Idempotence:** opakované vykonání požadavku má stejný efekt jako jednorázové.
- **Outbox pattern:** spolehlivá publikace událostí po DB transakci.
- **Projection:** materializovaný čtecí model z eventů.

