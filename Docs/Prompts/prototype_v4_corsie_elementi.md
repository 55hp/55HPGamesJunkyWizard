# Junky Wizard — Prototipo v4: corsie 1–5, selezione a tempo, elementi, nuovo libro magie

## Contesto
- Progetto: C:\Users\Franz\Unity Projects\55HPGamesJunkyWizard — repo 55hp/55HPGamesJunkyWizard.
- Lavora SOLO su `develop`. Commit piccoli, push alla fine. NESSUN merge.
- Parti dallo stato attuale (589973d o successivi): GridV3Game, IntentGridField, TimedLetterBuffer, SpellRecognizer, BeatClock, boot.
- Grey-box. Se un dettaglio non è specificato, scegli la soluzione più semplice e annotala nel report.
- I punti [DEFAULT] sono provvisori: implementali come scritti, valori nel config.

## 0. Calibrazione
- `firstBeatOffset` = **0.142** (verificato a mano; l'asset sul repo ha ancora 0.272).

## 1. Modalità unica `Lanes1to5`
- Sostituisce GridV3 e SingleLane nel menu di boot (il codice vecchio può restare).
- `laneCount` da 1 a 5. Menu di boot: tasti 1–5 scelgono il numero di corsie. H = mezzo beat on/off (come ora).
- Campo: `laneCount` colonne × `gridRows` righe (8).
- Nemici:
  - 1 corsia: avanzano dritti, nessun intento.
  - 2–5 corsie: intenti come GridV3 (↑ ↗ ↖), mai fuori dalla griglia.
  - Velocità in config `enemyStep`: Bar (default) / Beat / HalfBeat.
  - Colpo al mago come ora.

## 2. Comandi
- Lettere: digitate a tempo come ora (normali / maledette, mezzo beat opzionale).
- **Lancio su corsia selezionata**: Invio oppure ↑.
- **Lancio su sé stessi**: Shift destro oppure ↓.
- **Selezione corsia** (2–5 corsie): ← → spostano la selezione di 1, SOLO se Perfect/Good; con Miss non si sposta. Non ciclica. All'avvio la selezione è la corsia centrale (con corsie pari: quella a sinistra del centro).
- La bacchetta (solo visual) punta sempre la corsia selezionata.
- Manuale: Spazio apre/chiude. Aperto: ← → girano pagina a tempo (non spostano la corsia). Lancio con Invio/↑/Shift/↓ ancora possibile.
- Giudizio Perfect/Good/Miss anche sui tasti di lancio (loggato).

## 3. Buffer
- Limite **9** lettere: alla **10ª** esplosione (uccide tutti, -1 vita, oscuramento 4 s, buffer svuotato) — come ora ma con limite 9.
- Lettere maledette: contano per il riconoscimento; contraccolpo visivo al lancio come ora.
- Dopo ogni lancio il buffer si svuota.

## 4. Libro delle magie
Riconoscimento: sottosequenza come ora; precisione = lettere magia / lettere buffer. Più magie riconosciute: vince la precisione più alta; a parità, la parola più lunga. "Doppia" = due sottosequenze disgiunte in ordine (vale solo per le magie base elementali).

| Parola | Tipo | Bersaglio | Effetto |
|---|---|---|---|
| `pyro` / `kryo` / `aero` | Base fuoco / ghiaccio / vento | Corsia | Colpisce il primo nemico (il più vicino) della corsia |
| `pyropyro` / `kryokryo` / `aeroaero` | Doppia | Corsia | Colpisce i primi 2 nemici della corsia |
| `pyrsos` / `krymos` / `anemos` | Fascio | Corsia | Colpisce tutti i nemici della corsia |
| `pyrkaia` / `kryeros` / `thyella` | Linea | Tutte le corsie (qualunque tasto di lancio su corsia) | Colpisce il primo nemico di ogni corsia |
| `iasis` | Cura | Sé | +1 vita (max 5) |
| `panakeia` | Cura totale | Sé | Vite al massimo |
| `aegis` | Scudo | Sé | +1 carica scudo |

- Magia elementale lanciata su sé stessi: fallisce. Magia di supporto lanciata su corsia: fallisce.
- [DEFAULT] La potenza (precisione × giudizio) non modifica più gli effetti in v4: va solo loggata.

## 5. Elementi e resistenze
- Ogni nemico ha una resistenza: nessuna / fuoco / ghiaccio / vento (pesi in config, default 40/20/20/20).
- Resistenza ASSOLUTA: un nemico resistente a un elemento non subisce nulla da nessuna magia di quell'elemento (base, doppia, fascio, linea). Il colpo su un resistente non conta come uccisione e non si "consuma" su di lui per base/doppia [DEFAULT: il colpo lo attraversa e prosegue al prossimo nemico valido].
- Visual [DEFAULT]: colore del corpo = resistenza (nessuna grigio, fuoco arancio, ghiaccio azzurro, vento verde). La vicinanza NON usa più il rosso del corpo: usa un contorno/alone rosso che cresce avvicinandosi.

## 6. Scudo a cariche
- Lo scudo è un contatore: si parte con **1** carica, massimo **5**.
- Ogni colpo nemico consuma 1 carica; senza cariche, -1 vita.
- Visual: feedback sul mago proporzionale alle cariche (es. anelli/sfere attorno al mago, uno per carica). Contatore anche nell'HUD.

## 7. Sblocco magie per livello [DEFAULT]
- In config, per ogni `laneCount` la lista delle magie sbloccate:
  - 1 corsia: base + doppie elementali, iasis, aegis.
  - 2 corsie: + fasci.
  - 3–5 corsie: + linee, panakeia.
- Una magia non sbloccata non viene riconosciuta (il lancio fallisce).
- Il manuale mostra solo le magie sbloccate (una per pagina: parola, effetto, bersaglio).

## 8. HUD
- Buffer (maledette in stile diverso) + contatore `x/9`, vite, cariche scudo, corsia selezionata, cerchio del beat, intenti.

## Report finale (breve)
- Cosa hai fatto, scelte non specificate, problemi aperti, come testare.
