# Junky Wizard — Prototipo v2.2: colpo senza beat extra, scena di boot, modalità mouse

## Contesto
- Progetto: C:\Users\Franz\Unity Projects\55HPGamesJunkyWizard — repo 55hp/55HPGamesJunkyWizard.
- Lavora SOLO su `develop`. Commit piccoli e descrittivi, push alla fine. NESSUN merge.
- Parti dalla v2.1 attuale (commit 77fadfe o successivi). Modifiche mirate: non toccare altro.
- Resta grey-box: niente arte, niente feature non elencate. Se un dettaglio non è specificato, scegli la soluzione più semplice e annotala nel report.

## 1. Colpo dei nemici senza beat extra (modalità Grid, tutte le varianti)
- Problema attuale: il nemico che entra nella riga 0 colpisce un beat dopo. A schermo sembra che il mostro arrivi prima alle spalle del mago e poi gli venga addosso. Incoerente: va tolto.
- Nuova regola: il nemico colpisce nel momento in cui, A SCHERMO, raggiunge la riga del mago. Nessun beat aggiuntivo.
- Nell'ultimo tratto verso la riga 0 il nemico NON deve mai superare visivamente il mago (niente overshoot oltre la riga 0, niente passaggio dietro le spalle).
- Il nemico resta vulnerabile (pyro) per tutto il tragitto fino al momento del colpo.
- Spiega nel report come hai allineato logica e visuale.

## 2. Terza modalità: bacchetta controllata dal mouse
- Estendi `GameplayMode` con un terzo valore, es. `GridMouse`. È identica a `Grid` tranne il controllo della bacchetta.
- Il cursore del mouse è visibile come mirino semplice a schermo.
- La bacchetta punta verso il punto del terreno sotto il cursore (raycast dal mouse sul piano del terreno), solo rotazione orizzontale (yaw), limitata a ±wandMaxAngle.
- Controllo diretto: nessuna accelerazione, nessuno smoothing.
- In `GridMouse` le frecce sinistra/destra non muovono la bacchetta. Frecce su/giù per le pagine del manuale invariate. Digitazione, pyro, heal, aegis invariati.

## 3. Scena di boot con menu
- Nuova scena `Assets/Scenes/Boot.unity`, prima nelle Build Settings, seguita da `Prototype.unity` (togli SampleScene dalle Build Settings, non cancellarla).
- Menu minimo (UI semplice, grey-box) con tre pulsanti:
  - "Lanes"
  - "Grid — frecce"
  - "Grid — mouse"
- Selezionabili con mouse e anche con tastiera (1 / 2 / 3).
- La scelta avvia `Prototype.unity` nella modalità selezionata. Passa la modalità in modo semplice (es. classe statica di sessione); NON modificare l'asset di config a runtime.
- Se `Prototype.unity` viene avviata direttamente dall'editor (senza passare dal boot), usa `gameplayMode` del config come oggi.
- In `Prototype.unity`: Esc torna al menu di boot.

## Test
- Nessun test automatico. Test manuale in Play Mode partendo da Boot.unity, tutte e tre le modalità.

## Report finale
- Cosa hai fatto.
- Scelte non specificate che hai preso.
- Problemi aperti.
- Come testare (passi in Play Mode).
