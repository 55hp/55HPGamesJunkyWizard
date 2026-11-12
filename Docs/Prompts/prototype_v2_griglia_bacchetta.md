# Junky Wizard — Prototipo v2: griglia psichedelica + bacchetta

## Contesto
- Progetto: C:\Users\Franz\Unity Projects\55HPGamesJunkyWizard — repo 55hp/55HPGamesJunkyWizard.
- Lavora SOLO su `develop`. Commit piccoli e descrittivi, push alla fine. NESSUN merge.
- Parti dal prototipo v1 già presente (BeatClock, DspTimeMapper, SpellTyper, PrototypeWorld, PrototypeHud, PrototypeGame, PrototypeConfig). Riusa ciò che serve; non rompere il funzionamento attuale.
- Obiettivo v2: rendere il gameplay meno "rhythm game discreto" e più "rail shooter": logica scandita dal beat, resa visiva e mira continue. Resta grey-box: niente arte, niente feature non elencate. Se un dettaglio non è specificato, scegli la soluzione più semplice e annotala nel report.

## Modalità commutabile
- Aggiungi al config un enum `GameplayMode { Lanes, Grid }`.
  - `Lanes` = comportamento v1 attuale, invariato (corsie, nemici fermi, colpo dopo 4 beat, frecce = corsia).
  - `Grid` = tutto ciò che segue.
- Default: `Grid`. Il cambio di modalità avviene al restart del round (non serve a caldo).

## Griglia (modalità Grid)
- Griglia logica: 3 colonne (x = -3 / 0 / 3, riusa laneX) × `gridRows` righe di profondità (default 12, range consentito 8–20). Spaziatura righe `rowSpacing` (default 2 m).
- Riga 0 = riga del mago. Il mago sta al centro della riga 0.
- Nessuna griglia visibile obbligatoria; se utile al debug, linee guida molto tenui disattivabili da config.

## Nemici (modalità Grid)
- Cubi grigi, 1 HP. Hitbox generosa: raggio `enemyHitRadius` (default 1.0 m).
- Spawn, allineato al beat, scegliendo a caso tra:
  - **Fondo**: riga `gridRows`, colonna casuale.
  - **Laterale**: fuori griglia a sinistra o destra (posizione visiva x = ±6), riga casuale tra 4 e `gridRows`; al primo beat entra nella colonna esterna di quel lato.
  - Peso fondo/laterale in config (default 50/50).
- Frequenza di spawn: stessa rampa della v1 (da 1 ogni 16 beat a 1 ogni 4 beat in 180 s).
- Movimento: **esattamente una casella per beat**, logica discreta. Ogni nemico ha uno schema assegnato allo spawn (pesi in config):
  - **Dritto**: y-1.
  - **Zig-zag**: y-1 e x ±1 alternato (resta dentro le 3 colonne; se esce, inverte).
  - **Laterale**: alterna un passo di lato (x ±1, rimbalza ai bordi) e un passo avanti (y-1).
- Due nemici non possono occupare la stessa casella: se la destinazione è occupata, il nemico resta fermo quel beat.
- **Attacco**: quando un nemico entra nella riga 0 (qualsiasi colonna) colpisce il mago sul beat d'arrivo: se aegis è attivo lo consuma, altrimenti -1 vita. Poi il nemico sparisce.
- Nessun avviso a tempo fisso: la minaccia si legge dalla distanza in righe. Il nemico diventa progressivamente più rosso man mano che si avvicina (da grigio a riga `gridRows` a rosso pieno a riga 1).

## Interpolazione "psichedelica"
- Logica: posizione = casella. Visuale: tra il beat z e z+1 il cubo va da casella A a casella B usando la frazione di beat visiva (VisualBeatPosition).
- Curva: `AnimationCurve moveCurve` in config, default con leggero overshoot (sfora la destinazione e rientra).
- Più un'oscillazione laterale sinusoidale lungo il tragitto: ampiezza `wobbleAmplitude` (default 0.4 m) e frequenza `wobbleFrequency` in config, fase casuale per nemico.
- La hitbox segue la posizione VISIVA (continua), non la casella.

## Bacchetta (modalità Grid)
- Il mago ha un braccio destro: pivot alla spalla destra, la punta della bacchetta ruota solo sul piano orizzontale (yaw).
- Arco consentito: da -60° a +60° rispetto all'avanti (`wandMaxAngle`).
- Input: freccia sinistra/destra tenute premute ruotano la bacchetta (solo con manuale CHIUSO). Nessun requisito di timing.
- Velocità angolare con accelerazione esponenziale cappata:
  - `speed = min(wandMaxSpeed, wandStartSpeed * exp(wandAccel * holdTime))`
  - Default: wandStartSpeed 15°/s, wandMaxSpeed 180°/s, wandAccel calcolato perché la velocità massima si raggiunga in ~0.6 s (esponi il tempo-al-massimo in config e ricava wandAccel).
  - Rilascio = la bacchetta si ferma subito e mantiene la direzione. Cambio di verso = holdTime ripartito da 0.
- Feedback grey-box: braccio/bacchetta visibili + una linea sottile a terra che indica la direzione di mira.

## Magie in modalità Grid
- Digitazione invariata (Spazio → lettere → Invio, stesse regole di annullamento e giudizio).
- **pyro**: getto di fiamma lungo la direzione della bacchetta, dalla punta fino a coprire tutta la profondità della griglia.
  - Durata base 2 beat, larghezza base `pyroWidth` (default 1.5 m).
  - Il giudizio di Invio scala durata E larghezza: Perfect ×1, Good ×0.6, Miss ×0.3 (moltiplicatori in config).
  - Mentre il getto è attivo, le frecce continuano a ruotare la bacchetta e il getto la segue (si "spazzola" il campo).
  - Uccide ogni nemico la cui hitbox interseca il getto.
  - Si può digitare la magia successiva mentre il getto è attivo.
  - Visual grey-box: un parallelepipedo/cilindro semitrasparente arancione.
- **heal** e **aegis**: invariati rispetto alla v1, non si mirano.

## Manuale
- Invariato. Con manuale APERTO le frecce girano pagina (a tempo, come v1) e la bacchetta resta ferma.

## Config
- Tutti i nuovi valori nel PrototypeConfig esistente: gameplayMode, gridRows, rowSpacing, pesi spawn fondo/laterale, range righe spawn laterale, pesi schemi di movimento, enemyHitRadius, moveCurve, wobbleAmplitude, wobbleFrequency, wandMaxAngle, wandStartSpeed, wandMaxSpeed, tempo-al-massimo, pyroWidth, durata pyro in beat, moltiplicatori di giudizio.
- Aggiorna l'asset Assets/Config/PrototypeConfig.asset con i default indicati.

## Test
- Nessun test automatico. Test manuale in Play Mode su Prototype.unity, in entrambe le modalità.

## Report finale
- Cosa hai fatto.
- Scelte non specificate che hai preso.
- Problemi aperti.
- Come testare (passi in Play Mode, per entrambe le modalità).
