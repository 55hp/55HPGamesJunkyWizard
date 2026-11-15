# Junky Wizard — Prototipo v3.1: modalità a corsia singola

## Contesto
- Progetto: C:\Users\Franz\Unity Projects\55HPGamesJunkyWizard — repo 55hp/55HPGamesJunkyWizard.
- Lavora SOLO su `develop`. Commit piccoli, push alla fine. NESSUN merge.
- Prerequisito: la v3 (Docs/Prompts/prototype_v3_lettere_a_tempo.md) deve essere già implementata. Se non lo è, implementa prima la v3, poi questa.
- Questa spec AGGIUNGE una modalità; `GridV3` resta invariata.

## Nuova modalità `SingleLane`
- Nel menu di boot: due voci, "GridV3" e "Corsia singola" (tasti 1 / 2). Il toggle mezzo beat (H) vale per entrambe.
- Tutto come GridV3 (lettere a tempo, lettere maledette, limite 10 + esplosione, riconoscimento magie, potenza, manuale su Spazio con pagine ← → a tempo), TRANNE quanto segue.

### Campo
- Una sola colonna centrale × `gridRows` righe.

### Nemici
- Niente intenti: avanzano sempre dritti di una casella ogni battuta (4 beat), stessa animazione psichedelica.
- Spawn solo dal fondo. Casella occupata: il nemico resta fermo quella battuta.
- Colpo al mago come GridV3.

### Lancio
- **Invio**: lancia il buffer contro i nemici (giudicato Perfect/Good/Miss come le frecce in GridV3).
- **Shift destro**: lancia il buffer su sé stessi (giudicato allo stesso modo).
- Le frecce non lanciano (← → servono solo per le pagine del manuale aperto).
- Effetti:
  - pyro (Invio): uccide tutti i nemici nella corsia. Con Shift destro: fallisce.
  - pyro potenziata: identica a pyro in questa modalità.
  - heal / aegis (Shift destro): come GridV3. Con Invio: falliscono.
- Shift destro non deve inserire lettere nel buffer né alterare il riconoscimento delle maiuscole.

### HUD
- Promemoria comandi specifico della modalità. Nessuna icona di intento.

## Report finale (breve)
- Cosa hai fatto, scelte non specificate, problemi aperti, come testare.
