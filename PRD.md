# PRD — EFInsight : EF Core Query Logger

## 1. Nom du produit
EFInsight — Librairie pour détecter, logger et analyser les requêtes EF Core lentes ou problématiques.

## 2. Objectif
Permettre aux développeurs EF Core de :
- Identifier les requêtes lentes ou inefficaces
- Surveiller les commandes SQL générées par EF Core
- Recevoir des alertes ou logs sur les performances des requêtes
- Agir avant que des problèmes de performance n’apparaissent en production

## 3. Problème
- Les requêtes EF Core complexes ou mal configurées entraînent :
  - Des N+1 queries invisibles
  - Des SELECT inutiles sur de grandes tables
  - Des requêtes lentes non détectées
- Les outils actuels (profilers SQL, logs manuels) :
  - Sont difficiles à intégrer dans le pipeline
  - Ne permettent pas d’alerter directement dans l’application
  - Peuvent être trop lourds pour les environnements prod

## 4. Objectifs du produit
- Logger automatiquement toutes les requêtes EF Core
- Détecter les requêtes dont le temps dépasse un seuil configurable
- Fournir des logs structurés (SQL, durée, paramètres)
- Permettre un filtrage par DbContext, table ou pattern
- Supporter les environnements synchrones et asynchrones
- Minimaliste, facile à intégrer (`AddInterceptors`)

## 5. Non-objectifs
- Pas de modification de données
- Pas de cache ou projection automatique
- Pas de monitoring multi-base externe
- Pas de refactoring de requêtes

## 6. Public cible
- Développeurs backend .NET / EF Core
- Equipes travaillant sur performance SQL
- Développeurs d’API et services à fort trafic

## 7. Fonctionnalités principales

| Feature | Description | Exemple / Usage |
|---------|-------------|----------------|
| Threshold warning | Détecte requêtes > seuil | `SlowQueryLogger(100ms)` |
| Logs structurés | SQL, durée, paramètres | `ILogger.LogWarning` avec JSON |
| Sync & Async support | Logger compatible `ToList()`, `ToListAsync()` | `ReaderExecutedAsync` + `ReaderExecuted` |
| DbContext scoped | Peut filtrer par contexte | `AddInterceptor(new SlowQueryLogger(..., contextType))` |
| Optional callback | Action personnalisée sur requête lente | `OnSlowQuery = (cmd, duration) => ...` |
| Include stack trace | Optionnel pour debugging | `CaptureStackTrace = true` |
| Aggregate stats | Durée moyenne, max, nb queries | Future enhancement |

## 8. API publique (C#)

```csharp
// Construction
var logger = new SlowQueryLogger(
    thresholdMs: 100,
    logger: loggerFactory.CreateLogger<SlowQueryLogger>(),
    captureStackTrace: true
);

// Optionnel callback
logger.OnSlowQuery = (command, duration) => { ... };

// Ajout au DbContext
optionsBuilder.AddInterceptors(logger);

// Exemple direct
var users = await db.Users.ToListAsync();
```

## 9. Configuration
- Threshold : Durée maximale en ms avant log  
- Logger : ILogger pour logs structurés  
- CaptureStackTrace : bool  
- ContextFilter : DbContext type filter (optionnel)  
- Callback : Action<DbCommand, TimeSpan> pour comportement personnalisé

## 10. Comportement attendu
- Détecte les requêtes lentes synchrones et asynchrones
- Ne modifie pas le pipeline EF Core
- Ne génère pas d’exception par défaut
- Logs formatés et lisibles
- Optionnel : capture stack trace ou nom DbContext

## 11. Diagnostics / Messages

| Code | Description |
|------|-------------|
| EFQL001 | Requête lente détectée (> threshold) |
| EFQL002 | Paramètres trop volumineux (optionnel) |
| EFQL003 | Requête filtrée ignorée par contexte |

## 12. Performance
- Impact minimal (<1ms par requête)
- Compatible production
- Pas de allocations inutiles
- Aucun impact sur EF Core SQL translation

## 13. Compatibilité
- .NET 10+
- EF Core 8+
- Sync & Async
- ILogger compatible (Microsoft.Extensions.Logging)
- Thread-safe

## 14. Tests & Validation
- Tests unitaires pour commandes sync et async
- Tests de seuil (threshold) et logs
- Tests de filtrage DbContext
- Benchmark sur 10k requêtes simulées

## 15. Documentation
- README avec installation et usage  
- Exemples pour `AddInterceptors`  
- Exemple de callback custom  
- Best practices pour thresholds  

## 16. Succes Metrics
- Requêtes lentes détectées en dev et prod
- Adoption simple : 1 ligne pour activer
- Logs lisibles et exploitables
- Aucune régression sur pipeline EF
