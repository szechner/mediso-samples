# Auditní tok od Kafky k důkaznímu balíku

_(PaymentSample → Kafka → Merkle → Solana → Ověření → Export)_

## 1. Vznik auditní události (Kafka jako zdroj pravdy o událostech)

Každá významná událost v systému (např. platební operace) vytvoří **auditní událost**.  
Ta se odešle do Kafka topicu:

> **`payments.audit.v1`**

Auditní událost obsahuje:

-   **co se stalo** (typ události),

-   **kdy se to stalo** (čas),

-   **ke kterému případu to patří** – `correlationId`  
    _(to je „spisová značka“, která drží celý případ pohromadě)_,

-   **obsah události (payload)**,

-   a **hash obsahu (`payloadSha256`)**.


Kafka zde slouží jako:

-   **neměnný proud událostí** (log),

-   **oddělení aplikace od auditu**,

-   a záruka, že auditní události vznikají **v reálném čase**, ne dodatečně.


----------

## 2. Uložení do databáze (rychlá evidence, ne důkaz)

Auditní události se z Kafky ukládají do databáze (`audit_records`).

Důležité:

-   databáze **není zdroj důvěry**,

-   databáze je **pracovní evidence**:

  -   umožňuje dotazy,

  -   skládání případů,

  -   export.


Aby bylo možné později prokázat, že se **obsah nezměnil**, ukládá se:

-   payload (pro čitelnost),

-   **hash payloadu (`payloadSha256`)** – ten je klíčový.


----------

# Teď přichází kryptografie – ale lidsky

## 3. Co je hash (pro laiky)

Hash (např. SHA-256) je:

-   **digitální otisk dat**,

-   krátký řetězec znaků, který:

  -   **jednoznačně odpovídá obsahu**,

  -   při sebemenší změně obsahu se **zcela změní**.


Přirovnání:

> Jako otisk prstu dokumentu.  
> Změníš čárku → otisk je úplně jiný.

----------

## 4. Co je „leaf“ (list) – základní stavební kámen

U nás **leaf** reprezentuje **jednu auditní událost**.

Leaf **není payload**.  
Leaf je **hash složený z klíčových údajů**, které definují _co přesně se stalo_:

**Leaf = hash z:**

-   ID události,

-   `correlationId` (ke kterému případu patří),

-   času vzniku události,

-   **hashu payloadu** (ne samotného payloadu).


👉 Díky tomu:

-   není možné změnit payload, čas ani vazbu na případ **bez detekce**,

-   leaf je malý, anonymní a bezpečný.


----------

## 5. Proč se dělá batch (dávka)

Auditní události **neukotvujeme na blockchain po jedné**.  
To by bylo:

-   drahé,

-   pomalé,

-   zbytečné.


Místo toho:

-   v pravidelném **časovém intervalu** vezmeme více událostí,

-   a spojíme je do **jedné dávky (batch)**.


----------

## 6. Co je Merkle strom (vysvětleno bez matematiky)

Merkle strom je způsob, jak:

-   **sloučit mnoho otisků (leafů)** do **jednoho otisku**,

-   a přitom si zachovat možnost **později dokázat**, že tam konkrétní položka byla.


### Jak to funguje obrazně:

1.  Každá událost má svůj leaf (otisk).

2.  Dva leafy se spojí a zahashují → vznikne nový hash.

3.  Tyto nové hashe se zase párují a hashují.

4.  Pokračuje se, dokud nevznikne **jeden jediný hash**.


Ten finální hash se jmenuje:

> **Merkle root**

Merkle root je:

-   **otisk celé dávky**,

-   změna _jediné_ události změní celý root.


----------

## 7. Merkle proof – jak dokážeme, že tam něco bylo

Pro každou jednotlivou událost umíme vytvořit **Merkle důkaz**:

-   malý seznam hashů,

-   pomocí kterého lze:

  -   z daného leafu

  -   **znovu spočítat Merkle root**.


To znamená:

> „Tato konkrétní událost **prokazatelně patří** do dávky, jejíž otisk byl ukotven.“

A to:

-   **offline**,

-   bez databáze,

-   bez přístupu k systému.


----------

# Blockchain část – proč a jak

## 8. Proč blockchain nepoužíváme jako databázi

Blockchain:

-   je **pomalý**,

-   **drahý**,

-   **nevhodný pro data**.


My ho používáme jen jako:

> **nezávislou, veřejnou časovou pečeť**

----------

## 9. Co přesně zapisujeme na blockchain (Solana)

Na blockchain **nezapisujeme data událostí**.

Zapisujeme pouze **krátké memo**:

`mediso.audit.v1|
batch=<batchId>|
root=<merkleRoot>`

To znamená:

-   „V čase X existovala dávka Y s tímto přesným otiskem.“


Blockchain tím plní roli:

-   **digitálního notáře**,

-   který potvrdí:

  -   že něco existovalo,

  -   a kdy to existovalo.


----------

## 10. Co znamená „ověřeno na chainu“

Samostatný proces:

-   sleduje blockchain,

-   ověřuje, že transakce:

  -   skutečně existuje,

  -   je **finalizovaná**,

  -   obsahuje očekávané memo.


Jakmile je to splněno, do databáze se zapíše:

-   že dávka je **ověřená**,

-   včetně:

  -   bloku,

  -   času,

  -   veřejného klíče účtu, který zápis provedl.


API už pak **nemusí mluvit s blockchainem** – pracuje s ověřeným výsledkem.

----------

# Ověření případu (correlationId)

## 11. Jak ověřujeme celý případ

Pro daný `correlationId` systém:

1.  Najde všechny auditní události případu.

2.  Pro každou:

  -   znovu spočítá leaf z uložených dat,

  -   ověří, že sedí s tím, co je v dávce.

3.  Z Merkle důkazu znovu spočítá root.

4.  Zkontroluje, že tento root:

  -   byl skutečně ukotven na blockchainu,

  -   a je potvrzený (finalized).


----------

## 12. Výsledek ověření (lidsky pochopitelný)

Systém vrací jasný verdikt:

-   **VERIFIED**  
    Všechny události jsou prokazatelně nezměněné a ukotvené.

-   **INCONCLUSIVE**  
    Něco ještě čeká (např. čerstvá dávka, neověřený zápis).

-   **NOT_VERIFIED**  
    Byla zjištěna nesrovnalost – změna dat, porušení integrity.


----------

# Důkazní balík (pro OČTŘ / regulátora)

## 13. Co je důkazní balík

Důkazní balík je **ZIP soubor**, který obsahuje:

-   čitelný popis případu,

-   kryptografické důkazy,

-   a metadata k blockchainovému ukotvení.


Je:

-   **přenositelný**,

-   **ověřitelný offline**,

-   **nezávislý na systému**, který ho vytvořil.


Lze ho:

-   založit do spisu,

-   předat třetí straně,

-   znovu ověřit i za několik let.