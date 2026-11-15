# Junky Wizard — Prototipo v3: lettere a tempo, lancio con le frecce, intenti dei nemici

## Contesto
- Progetto: C:\Users\Franz\Unity Projects\55HPGamesJunkyWizard — repo 55hp/55HPGamesJunkyWizard.
- Lavora SOLO su `develop`. Commit piccoli e descrittivi, push alla fine. NESSUN merge.
- Parti dalla v2.2 (commit aad8ec2 o successivi). Riusa BeatClock, DspTimeMapper, griglia, interpolazione "psichedelica", HUD, manuale, boot.
- Resta grey-box. Se un dettaglio non è specificato, scegli la soluzione più semplice e annotala nel report.
- I punti marcati [DEFAULT] sono scelte provvisorie: implementale come scritte, esponendo i valori nel config.

## 0. Modalità
- Nuova modalità unica di gioco: `GridV3`. È l'unica nel menu di boot.
- `Lanes`, `Grid`, `GridMouse`: rimuovile dal menu. Il codice può restare, non va cancellato in questo giro.
- Bacchetta e getto continuo NON esistono in GridV3.

## 1. Griglia e nemici
- Griglia: 3 colonne × `gridRows` righe. [DEFAULT] gridRows = 8 (range 8–20).
- I nemici si muovono di UNA casella ogni BATTUTA (4 beat), sul primo beat della battuta. Animazione dello spostamento: dura 1 beat, con la curva psichedelica esistente (overshoot + oscillazione), tranne l'ultimo tratto verso la riga 0 che resta dritto (come v2.2).
- **Intenti** (stile Slay the Spire): ogni nemico mostra sopra la testa un'icona con la PROSSIMA mossa, visibile per tutta la battuta prima che avvenga:
  - avanti di 1 (↑)
  - avanti di 1 e a destra (↗)
  - avanti di 1 e a sinistra (↖)
- L'intento si sceglie a caso tra quelli validi (un nemico sul bordo non può uscire dalla griglia). Pesi in config.
- Casella di destinazione occupata: il nemico resta fermo quella battuta e sceglie un nuovo intento.
- Spawn: dal fondo o dai lati come v2 (pesi in config), sempre sul primo beat di una battuta. Frequenza: rampa in battute, [DEFAULT] da 1 ogni 4 battute a 1 ogni battuta in 180 s.
- Colpo: quando un nemico raggiunge visivamente la riga 0 (regola v2.2): aegis attivo lo assorbe, altrimenti -1 vita.
- Nemici: 1 HP, colorazione rossa crescente con la vicinanza (come v2).

## 2. Digitazione libera a tempo
- Niente più Spazio per aprire la parola: si digita liberamente, le lettere entrano in un buffer visibile.
- Ogni lettera viene giudicata sul timestamp dell'evento (convertito in dspTime come oggi):
  - entro ±goodWindow (90 ms) da un beat → **lettera normale**;
  - fuori → **lettera maledetta**.
- Opzione `allowHalfBeatLetters` (config + toggle nel menu di boot, tasto H): se attiva, valgono anche i mezzi beat.
- Le lettere maledette CONTANO come lettere normali per riconoscere la magia. A schermo usano uno stile diverso (grey-box: colore viola + corsivo o font diverso).
- Nessun Backspace in questo giro.
- **Limite 10 lettere**: all'11ª lettera il buffer esplode:
  - uccide TUTTI i nemici in campo;
  - il mago perde 1 vita;
  - la scena si oscura: overlay nero all'80% che sfuma in `explosionDarkSeconds` [DEFAULT] 4 s;
  - il buffer si svuota.

## 3. Lancio con le frecce
- Con il manuale chiuso, una freccia lancia il contenuto del buffer:
  - ← colonna sinistra, ↑ colonna centrale, → colonna destra, ↓ sé stessi.
- La freccia viene giudicata (Perfect / Good / Miss, finestre attuali): moltiplicatore 1 / 0.6 / 0.3.
- Dopo il lancio il buffer si svuota sempre.

### Riconoscimento della magia
- Magie: `pyro`, `heal`, `aegis`.
- Una magia è riconosciuta se le sue lettere compaiono nel buffer nell'ordine giusto, anche con lettere in mezzo o intorno (sottosequenza). Esempi validi per pyro: `pyro`, `spyro`, `pyyro`.
- **Potenziata**: se la magia compare DUE volte come sottosequenze disgiunte e in ordine (es. `pyropyro`).
- Precisione = lettere della magia / lettere nel buffer (es. `pyro` = 1.0, `spyro` = 0.8).
- Potenza = precisione × moltiplicatore della freccia.
- Più magie riconosciute: vince quella con precisione maggiore; a parità, ordine pyro > heal > aegis.
- Nessuna magia riconosciuta: fallisce, non succede nulla.

### Effetti
- **pyro** (← ↑ →): uccide tutti i nemici nella colonna scelta, qualunque potenza [DEFAULT]. Con ↓: fallisce.
- **pyro potenziata** (← ↑ →): uccide tutti i nemici in tutte e 3 le colonne.
- **heal** (solo ↓): +max(1, round(2 × potenza)) vite, max 5. Potenziata: vite al massimo.
- **aegis** (solo ↓): assorbe il prossimo colpo; durata max(4, round(16 × potenza)) beat. Potenziata: assorbe 2 colpi.
- heal / aegis con ← ↑ →: falliscono.

### Lettere maledette: contraccolpo
- Al momento del lancio, ogni lettera maledetta presente nel buffer aggiunge un disturbo visivo di `cursedEffectSeconds` [DEFAULT] 3 s. I disturbi si sommano (più lettere = più intenso e/o più lungo).
- Grey-box: overlay a schermo con rumore/tremolio + leggera oscillazione della camera. La magia funziona comunque.

## 4. Manuale
- **Spazio** apre e chiude il manuale (Tab non serve più).
- Con il manuale aperto: ← → girano pagina SOLO se a tempo (Perfect/Good). ↑ ↓ non fanno nulla.
- Con il manuale aperto le frecce NON lanciano. La digitazione resta possibile.
- Una pagina per magia: parola, effetto, frecce valide.

## 5. HUD
- Buffer con stile diverso per le lettere maledette + contatore `x/10`.
- Cerchio del beat, vite, intenti sopra i nemici.
- Promemoria comandi aggiornato.

## Config
- Tutti i valori nuovi in PrototypeConfig: gridRows, pesi intenti, rampa spawn in battute, allowHalfBeatLetters, maxLetters (10), explosionDarkSeconds, cursedEffectSeconds, formule heal/aegis.

## Test
- Nessun test automatico. Test manuale da Boot.unity.

## Report finale (breve)
- Cosa hai fatto.
- Scelte non specificate che hai preso.
- Problemi aperti.
- Come testare.
