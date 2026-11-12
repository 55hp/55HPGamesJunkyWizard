# Junky Wizard — Prototipo v2.1: bacchetta sempre libera, pagine su/giù, pyro 3 s

## Contesto
- Progetto: C:\Users\Franz\Unity Projects\55HPGamesJunkyWizard — repo 55hp/55HPGamesJunkyWizard.
- Lavora SOLO su `develop`. Commit piccoli e descrittivi, push alla fine. NESSUN merge.
- Parti dalla v2 attuale (commit 859a32c o successivi). Modifiche mirate: non toccare altro.
- La modalità `Lanes` deve continuare a funzionare come prima (le modifiche sotto valgono per `Grid`, salvo dove indicato).

## 1. Bacchetta sempre libera (modalità Grid)
- Frecce SINISTRA/DESTRA tenute premute ruotano la bacchetta SEMPRE: anche con il manuale aperto e anche mentre il buffer di digitazione è aperto.
- In modalità Grid le frecce sinistra/destra NON hanno più nessun'altra funzione.

## 2. Pagine del manuale con frecce SU/GIÙ (modalità Grid)
- Con il manuale aperto: freccia SU = pagina precedente, freccia GIÙ = pagina successiva.
- Regola di timing invariata: la pagina gira solo con giudizio Perfect o Good; con Miss non gira. Usa il timestamp dell'evento convertito in dspTime, come per le frecce attuali.
- Pagine non cicliche, come ora.
- Modalità Lanes: invariata (frecce sinistra/destra = corsia o pagina come in v1).
- Aggiorna promemoria comandi e testi del manuale in base alla modalità.

## 3. Pyro: durata fissa 3 s (modalità Grid)
- Nuovo valore in config `pyroDurationSeconds` = 3 (sostituisce `pyroDurationBeats` per la modalità Grid; se `pyroDurationBeats` non è usato altrove, rimuovilo).
- La durata NON dipende più dal giudizio: sempre `pyroDurationSeconds`.
- Il giudizio di Invio scala SOLO la larghezza del getto (moltiplicatori Perfect/Good/Miss invariati).
- La durata conta dal momento in cui premi Invio (come ora).

## 4. Velocità bacchetta: nuovi default
- Aggiorna Assets/Config/PrototypeConfig.asset e i default nello script:
  - wandStartSpeed = 60
  - wandMaxSpeed = 200
  - wandTimeToMaxSpeed = 0.5

## Test
- Nessun test automatico. Test manuale in Play Mode su Prototype.unity, modalità Grid e Lanes.

## Report finale
- Cosa hai fatto.
- Scelte non specificate che hai preso.
- Problemi aperti.
- Come testare (passi in Play Mode).
