# Název
Vytvořit endpoint pro validaci čísla účtu + případné formátování podle BBAN a IBAN.

## Kontext
IBAN formátování zatím nemáme. Validaci čísla účtu už v systému máme, ale není veřejně vystavená přes API.

## Akceptační kritéria
- vznikne veřejný endpoint pro validaci čísla účtu
- endpoint vrátí výsledek validace
- pokud to dává smysl, vrátí i normalizovaný BBAN a IBAN
- řešení bude zdokumentované ve Swagger/OpenAPI
- bude znovu použita existující validační logika, pokud už v systému je