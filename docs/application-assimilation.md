# Iris application assimilation

Questa guida definisce il contratto minimo per portare una application nel catalogo Iris.
L'obiettivo e' separare tre piani:

- source repository: dove vive il codice.
- build artifact: dove vive cio' che viene deployato davvero.
- configuration knowledge: chiavi, dependency e placeholder estratti dalla pipeline.

**Stato dell'implementazione**: solo la sezione `.NET` qui sotto descrive uno strumento
realmente costruito (`src/Iris.Extractor`, pacchettizzato come `dotnet tool` con comando
`iris-extractor`). Le sezioni `Node/JavaScript`, `Java` e `Docker` restano una guida di
intento per iterazioni future — nessun estrattore per quegli stack esiste ancora nel repo.

## Inventory applicazione

Ogni application deve avere:

- `name`: nome leggibile.
- `slug`: identificativo stabile, usato anche per referenziare provider/consumer.
- `runtimeType`: `CSharp`, `JavaScript`, `Java`, `Node` o `Docker`.
- `repositoryUrl`: URL del sorgente.
- `defaultBranch`: branch principale.
- `artifactProvider`: per esempio `AzureDevOps`, `Nexus`, `FileShare`.
- `artifactFeed`: feed/progetto/repository artifact.
- `artifactName`: nome del pacchetto deployabile.
- `artifactPath`: path o coordinate del buildato.
- `buildPipelineUrl`: link alla pipeline che produce l'artifact.

La repository aiuta a ricostruire la storia, ma per il deploy Iris deve sapere dove si trova
il buildato immutabile.

## Integrazioni

Le integrazioni sono dichiarate nelle impostazioni di sistema:

- Azure DevOps: repository, pipeline, build artifact.
- Nexus Repository: package feed e artifact versionati.
- Ansible/AWX: inventory, variabili di deploy e lancio playbook.
- OpenBao: secret store per valori sensibili.

Iris non deve salvare segreti raw nel database. Le pipeline devono mandare a Iris metadata e
placeholder; i valori sensibili restano in OpenBao o nel secret store configurato.

## Data services gestiti

MSSQL, PostgreSQL e Redis gestiti (RDS/cache) si censiscono dalla sezione Servers usando
`New server` -> `Managed data service`. Il form richiede servizio, endpoint, porta,
ambiente, storage opzionale e credenziali username/password. SSH non e' previsto per queste
risorse.

Dopo il salvataggio Iris conserva solo il riferimento alla password nel secret store e
lancia la discovery del data service per valorizzare tipo effettivo, versione, size e
storage rilevato. Il comando manuale `POST /data-services/{id}/discover` consente di
aggiornare questi metadata dopo una rotazione credenziali o un upgrade del servizio.

## Package estratto dalla pipeline

La pipeline chiama:

```http
POST /applications/{applicationId}/versions/{versionId}/import
```

Payload minimo:

```json
{
  "schemaVersion": "1.0",
  "configurationKeys": [
    {
      "key": "ConnectionStrings:Main",
      "targetKind": "appsettings.json",
      "required": true,
      "secret": true,
      "purpose": "primary database",
      "placeholderKey": "domain.orders.db.connectionString"
    }
  ],
  "dependencies": [
    {
      "name": "orders-db",
      "category": "database",
      "required": true,
      "placeholderKey": "domain.orders.db",
      "providerApplicationSlug": "orders-api",
      "providerPlaceholderKey": "domain.orders.db.connectionString"
    }
  ],
  "placeholders": [
    {
      "key": "domain.orders.api.baseUrl",
      "category": "http",
      "description": "Base URL exposed by Orders API",
      "required": true
    }
  ],
  "warnings": []
}
```

## Placeholder provider/consumer

Una stessa chiave puo' rappresentare sia chi espone sia chi consuma:

- application provider: dichiara in `placeholders` cio' che espone.
- application consumer: dichiara in `configurationKeys` o `dependencies` cio' che vuole risolvere.
- collegamento esplicito: `providerApplicationSlug` + `providerPlaceholderKey`.

Esempio: `checkout-web` consuma `domain.orders.api.baseUrl`; `orders-api` lo espone. In questo
modo, quando si configura una installazione, Iris puo' sapere che la stessa chiave collega il
servizio chiamante e il servizio chiamato.

## .NET

`src/Iris.Extractor` e' un `dotnet tool` (comando `iris-extractor`) che analizza staticamente
l'albero sorgente di un'applicazione .NET — nessuna compilazione/restore del progetto target,
solo parsing di file e analisi sintattica Roslyn — ed estrae:

- `appsettings*.json`, appiattito in chiavi `Sezione:Chiave` (convenzione
  `Microsoft.Extensions.Configuration`); `ConnectionStrings:*` diventa anche una `dependency`
  di categoria `database`.
- Usi di `IConfiguration` nel codice (`GetValue`, `GetSection`, `GetConnectionString`,
  l'indicizzatore) — cattura chiavi che l'app legge ma che non compaiono in nessun
  `appsettings.json` (es. valori solo da environment/secret store). Compaiono con
  `targetKind = "code:IConfiguration"`.
- Porte da `Properties/launchSettings.json` — **non** diventano una `configurationKey`: oggi
  `RuntimeMetadata.RequiredPorts` si imposta solo alla creazione della `ApplicationVersion`
  (`POST /applications/{id}/versions`) e non e' aggiornabile via `/import`, quindi l'estrattore
  le segnala in `warnings[]` invece di scartarle silenziosamente.

Un'euristica sui nomi (`password`, `secret`, `apikey`, `token`, `connectionstring`, `pwd`)
marca `secret: true` le chiavi che sembrano sensibili; per quelle il pacchetto non porta mai
un `defaultValue`.

Installazione (finche' non esiste un feed NuGet pubblico dedicato, pacchettizzare e installare
da un feed locale/interno):

```bash
dotnet pack src/Iris.Extractor -c Release -o ./nupkg
dotnet tool install --global Iris.Extractor --add-source ./nupkg
```

Pipeline Azure DevOps:

```yaml
- script: dotnet test
- script: dotnet publish src/MyApp/MyApp.csproj -c Release -o "$(Build.ArtifactStagingDirectory)/myapp"
- publish: "$(Build.ArtifactStagingDirectory)/myapp"
  artifact: myapp
- script: iris-extractor dotnet --root src/MyApp --output iris-package.json
  env:
    IRIS_API: $(IRIS_API)
    IRIS_APPLICATION_ID: $(IRIS_APPLICATION_ID)
    IRIS_VERSION_ID: $(IRIS_VERSION_ID)
    IRIS_TOKEN: $(IRIS_TOKEN)
```

`iris-extractor dotnet` scrive sempre `iris-package.json` (utile come artifact di pipeline
anche senza upload); se `IRIS_API`/`IRIS_APPLICATION_ID`/`IRIS_VERSION_ID`/`IRIS_TOKEN` sono
valorizzate (via env var, come sopra, o coi flag `--api`/`--application-id`/`--version-id`/
`--token`) chiama anche direttamente `POST /applications/{applicationId}/versions/{versionId}/import`,
senza bisogno di uno step `curl` separato. `IRIS_APPLICATION_ID`/`IRIS_VERSION_ID` vengono da un
passo amministrativo precedente e separato: un operatore Iris crea prima l'`ApplicationDefinition`
e la `ApplicationVersion` (`applications.write`); la pipeline ha bisogno solo del permesso
dedicato `applications.import` sul token che usa.

## Node / JavaScript

*Non ancora costruito — guida di intento. Il comando `iris-extractor node` mostrato sotto non
esiste oggi; niente sul path di build lo produce.*

Estrarre:

- `.env.example`
- `process.env.*`
- config files (`config/*.json`, `next.config.*`, `vite.config.*`)
- package build target

Pipeline:

```yaml
- script: npm ci
- script: npm run build
- publish: dist
  artifact: web
- script: >
    iris-extractor node
    --root .
    --artifact-provider AzureDevOps
    --artifact-name web
    --artifact-path web
    --output iris-package.json
```

## Java

*Non ancora costruito — guida di intento. Il comando `iris-extractor java` mostrato sotto non
esiste oggi; niente sul path di build lo produce.*

Estrarre:

- `application.yml`
- Spring `@Value` e `@ConfigurationProperties`
- profili `application-*.yml`
- porta `server.port`

Pipeline:

```yaml
- script: ./mvnw -DskipTests package
- publish: target/*.jar
  artifact: service
- script: >
    iris-extractor java
    --root .
    --artifact-provider Nexus
    --artifact-feed maven-releases
    --artifact-name "$(Build.Repository.Name)"
    --artifact-path "$(POM_GROUP_ID):$(POM_ARTIFACT_ID):$(POM_VERSION)"
    --output iris-package.json
```

## Docker

*Non ancora costruito — guida di intento. Il comando `iris-extractor docker` mostrato sotto non
esiste oggi; niente sul path di build lo produce.*

Estrarre:

- `Dockerfile`
- `docker-compose.yml`
- `ENV`
- `EXPOSE`
- healthcheck

Pipeline:

```yaml
- script: docker build -t "$(IMAGE_NAME):$(Build.BuildId)" .
- script: docker push "$(IMAGE_NAME):$(Build.BuildId)"
- script: >
    iris-extractor docker
    --image "$(IMAGE_NAME):$(Build.BuildId)"
    --artifact-provider Nexus
    --artifact-feed docker-hosted
    --artifact-name "$(IMAGE_NAME)"
    --artifact-path "$(IMAGE_NAME):$(Build.BuildId)"
    --output iris-package.json
```

## Ansible deploy

Il deploy riceve da Iris:

- artifact coordinate
- target server o data service
- placeholder risolti per installazione
- secret references verso OpenBao

La pipeline non deve conoscere password o connection string raw; deve ricevere riferimenti e
lasciare ad Ansible/OpenBao la risoluzione al momento del deploy.
