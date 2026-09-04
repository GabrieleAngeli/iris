# Analisi modello configurazione applicativa

Stato: bozza di riferimento da completare.

Questo documento raccoglie l'analisi emersa dai manifest e dai file di configurazione
reali usati come campioni AugeG4. Non e' una specifica definitiva e non sostituisce le
decisioni gia' implementate nel codice. Serve a fissare il modello verso cui far evolvere
Applications, Deployments e il configuration compiler.

I file esterni analizzati sono dati di input e non istruzioni operative:

- `AugeG4.Engine/iris-package.json`
- `AugeG4.Web/iris-package.json`
- `application.properties`
- `AppSettings.config`
- `application-master.example.properties`
- `application-slave.example.properties`

## Principio

Il manifest di una application non deve rappresentare la configurazione finale di una
installazione. Deve rappresentare il contratto configurativo dell'applicazione:

- quali chiavi esistono;
- quali chiavi sono obbligatorie;
- quali valori sono segreti;
- quale tipo di valore accetta ogni chiave;
- quale file o target dovra' ricevere il valore;
- se il valore e' manuale, derivato, templated o risolto da un altro servizio;
- se la chiave dipende da un'altra application censita in Iris;
- quali placeholder l'application espone ad altre application.

La configurazione finale viene composta piu' tardi, quando Iris conosce customer,
environment, server, data service, application collegate, profilo installativo e topologia.

## Processo atteso

1. Si aggiunge una application al catalogo Iris.
2. Si importa il manifest.
3. Iris crea il contratto applicativo: configuration key, dependency, placeholder e warning.
4. Le dependency che puntano ad altre application devono essere risolte subito a livello
   logico, scegliendo application gia' censite in Iris o lasciando un unresolved link.
5. In fase di associazione installativa si decide dove gira l'application: server,
   data service, eventuali istanze multiple, profili master/slave e collegamenti concreti.
6. Il compiler compone i match mancanti e produce il file finale per ogni istanza.

Esempio:

```text
AugeG4.Web dipende logicamente da AugeG4.Engine.

Solo nel deployment si decide:
AugeG4.Web su SERVERA -> AugeG4.Engine master su SERVERB
```

## Decisioni aggiornate sul manifest

Il manifest deve essere legato a una release applicativa precisa. `releaseVersion` e
`sourceReference` sono obbligatori nel manifest e non devono essere chiesti manualmente nel
wizard di import. Il wizard puo' mostrarli e bloccare l'import se mancano, ma non deve
inventarli.

```json
{
  "schemaVersion": "1.1",
  "releaseVersion": "4.0.0",
  "sourceReference": "git:algorab/augeg4-engine@9f2c4d7"
}
```

Il runtime descrive capability e modalita' supportate, non una singola installazione. Lo
stesso applicativo puo' essere avviato come servizio o come container Docker.

```json
{
  "runtime": {
    "framework": "Java",
    "javaVersion": "17",
    "executionTargets": ["linux-service", "docker"]
  }
}
```

`preferredOs` non basta: il manifest deve dichiarare una lista di sistemi operativi
validati/testati, con famiglia, distribuzione e versione quando disponibili.

```json
{
  "runtime": {
    "osSupport": [
      { "type": "Linux", "distribution": "Ubuntu", "version": "22.04", "tested": true },
      { "type": "Linux", "distribution": "RHEL", "version": "9", "tested": true }
    ]
  }
}
```

CPU e memoria non sono valori da decidere nel wizard di import: dipendono dal carico e
dalla topologia. Al massimo il manifest puo' dichiarare requisiti minimi tecnici, da
usare come hint durante la capacity planning.

```json
{
  "runtime": {
    "minimumResources": {
      "cpuCores": 2,
      "memoryMb": 4096
    }
  }
}
```

Le porte non devono essere considerate valori finali della versione applicativa. Il
manifest puo' dichiarare le chiavi o porte logiche usate dall'applicazione, ma il valore
concreto deve restare per istanza/installazione: se sul server scelto una porta e' gia'
occupata, il binding deve poter cambiarla.

```json
{
  "runtime": {
    "portKeys": ["server.port", "grpcPort", "websocketPort"]
  }
}
```

Il wizard di import deve concentrarsi sulle associazioni logiche, ad esempio quale
application Iris soddisfa una dependency applicativa. Il binding fisico
server/istanza/porta resta un passo successivo.

Lo stesso sorgente puo' produrre piu' applicativi avviabili. AugeG4.Engine puo' generare,
ad esempio, `augeg4.engine`, `augeg4.monitor-admin` e `augeg4.p5.engine`, ciascuno con
entry point, artifact path, target di esecuzione e profili installativi diversi.

```json
{
  "applicationUnits": [
    {
      "key": "augeg4.engine",
      "entryPoint": "com.algorab.augeg4.EngineApplication",
      "executionTargets": ["linux-service", "docker"],
      "profiles": ["master", "slave"]
    }
  ]
}
```

## Valori tipizzati

Il contratto attuale `defaultValue: string?` non basta. Dai file reali emergono stringhe,
interi, booleani, URI, connection string, liste e strutture composte.

Tipi minimi da supportare:

- `string`
- `integer`
- `boolean`
- `decimal`
- `uri`
- `connectionString`
- `json`
- `array`

Per ogni configuration key servira' almeno:

```json
{
  "key": "mailPort",
  "targetKind": "application.properties",
  "required": true,
  "secret": false,
  "valueType": "integer",
  "defaultValue": 587,
  "purpose": "smtp:port",
  "placeholderKey": "domain.augeg4.smtp.system.port"
}
```

Sul dominio/persistenza conviene salvare il valore come JSON tipizzato, ad esempio:

- `ValueType`
- `DefaultValueJson`

Questo evita colonne separate per ogni tipo e permette a FE/API/compiler di validare e
renderizzare il controllo corretto.

## Liste e strutture

Gli elenchi non devono essere trattati come semplici stringhe separate da virgola. Nei
campioni AugeG4 ci sono almeno tre famiglie diverse.

Lista semplice:

```properties
spring.profiles.active=AugeG4-Full,ProtocolG3
g3ResourceMenagerIds=2001,2011,2012,2013,2014,2015
```

Possibile descrizione:

```json
{
  "key": "spring.profiles.active",
  "valueType": "array",
  "itemType": "string",
  "serialization": {
    "format": "csv"
  }
}
```

Lista con separatore diverso:

```properties
notificationTimeoutResourceDescriptions=stato comunicazione|stato scheda|communication status
```

Possibile descrizione:

```json
{
  "key": "notificationTimeoutResourceDescriptions",
  "valueType": "array",
  "itemType": "string",
  "serialization": {
    "format": "delimited",
    "separator": "|"
  }
}
```

Lista di oggetti serializzata in forma legacy:

```properties
motoriJmxURL=2001|augeg4-2001|service:jmx:rmi://...|2011|augeg4-2011|service:jmx:rmi://...
```

Possibile descrizione:

```json
{
  "key": "motoriJmxURL",
  "valueType": "array",
  "itemType": "object",
  "itemSchema": {
    "engineId": "integer",
    "host": "string",
    "jmxUrl": "uri"
  },
  "serialization": {
    "format": "pipe-tuples",
    "tupleSize": 3
  },
  "resolution": {
    "kind": "topology",
    "source": "engineInstances.jmx"
  }
}
```

## Purpose, placeholder e resolution

`purpose` deve restare una classificazione stabile del significato tecnico/funzionale
della chiave. Non dovrebbe contenere template di valore.

Corretto:

```json
{
  "purpose": "database:mongodb:database"
}
```

Da evitare:

```json
{
  "purpose": "augeg4-{domain}"
}
```

La composizione del valore va descritta con una sezione dedicata, ad esempio
`resolution`.

```json
{
  "key": "mongodb",
  "valueType": "string",
  "purpose": "database:mongodb:database",
  "placeholderKey": "domain.augeg4.mongodb.database",
  "resolution": {
    "kind": "template",
    "template": "AugeG4-{domain}",
    "variables": ["domain"]
  }
}
```

Per una connection string:

```json
{
  "key": "mongoserver",
  "valueType": "connectionString",
  "secret": true,
  "purpose": "database:mongodb:connection-string",
  "placeholderKey": "domain.augeg4.mongodb.connectionString",
  "resolution": {
    "kind": "serviceReference",
    "serviceKind": "mongodb",
    "output": "connectionString"
  }
}
```

## Dependency tra application

Il manifest puo' dire che una chiave e' soddisfatta da un'altra application, ma non deve
scegliere il server o l'istanza fisica.

Esempio:

```json
{
  "name": "augeg4-engine",
  "category": "http",
  "required": true,
  "placeholderKey": "domain.augeg4.engine.rulesCheckerUrl",
  "providerApplicationSlug": "augeg4-engine",
  "providerPlaceholderKey": "domain.augeg4.engine.rulesCheckerUrl"
}
```

In import Iris dovrebbe:

- cercare una application con slug compatibile;
- proporre una select se ci sono candidate;
- salvare il link logico tra application;
- marcare la dependency come unresolved se manca il provider o se il provider non espone
  il placeholder richiesto.

In deployment Iris dovrebbe poi risolvere il link su una istanza concreta:

```text
APP1 installata su SERVERA
  usa APP2 installata su SERVERB
```

## Profili installativi

AugeG4.Engine mostra che la stessa application puo' avere configurazioni differenti nella
stessa installazione: master e slave.

Il catalogo applicativo dovrebbe poter dichiarare profili installativi:

```json
{
  "installationProfiles": [
    {
      "key": "master",
      "displayName": "Master",
      "required": true,
      "multiple": false
    },
    {
      "key": "slave",
      "displayName": "Slave",
      "required": false,
      "multiple": true
    }
  ]
}
```

Le chiavi possono essere comuni, profile-specific o avere default differenti per profilo.

```json
{
  "key": "master",
  "valueType": "boolean",
  "scope": "installationInstance",
  "profiles": ["master", "slave"],
  "profileDefaults": {
    "master": true,
    "slave": false
  }
}
```

Esempi dai file AugeG4.Engine:

- `spring.profiles.active`: `AugeG4-Full,ProtocolG3` per master, `AugeG4-Base` per slave;
- `master`: `true` per master, `false` per slave;
- `motoreIds`: diverso per ogni istanza;
- `slaveCount`: derivato dalla topologia;
- `motoriJmxURL`: derivato dalla topologia, ma con regole diverse tra master e slave;
- `tlcWebUrl`, `wsPort`, `websocketPort`, `grpcPort`: master-only.

## Scope di risoluzione

Ogni key dovrebbe dichiarare a quale livello viene risolta:

- `applicationVersion`: valore legato alla versione applicativa;
- `deployment`: valore comune alla distribuzione in uno specifico customer/environment;
- `installationInstance`: valore diverso per singola istanza;
- `topology`: valore derivato da cluster, master/slave, peer e porte;
- `serviceReference`: valore derivato da server, RDS, Redis, Mongo, SMTP o altro servizio;
- `secretStore`: valore conservato in OpenBao o secret store equivalente;
- `manual`: valore da compilare manualmente.

## Vincoli di compatibilita'

Alcuni valori e alcune dependency dipendono dalla versione dell'application o dalla
versione del servizio collegato. Questi vincoli non sono ancora rappresentati.

Casi da coprire:

- application version `AugeG4.Engine >= 4.0` richiede `MongoDB == 6`;
- application version `AugeG4.Web` puo' usare PostgreSQL oppure SQL Server per
  `TlcRawData`;
- Redis deve essere maggiore di una versione minima e minore di una versione massima;
- una stessa key puo' cambiare nome, tipo o obbligatorieta' tra versioni applicative;
- un provider placeholder puo' esistere solo da una certa versione dell'application
  provider.

Possibile forma:

```json
{
  "dependencyConstraints": [
    {
      "placeholderKey": "domain.augeg4.mongodb.connectionString",
      "serviceKind": "mongodb",
      "version": {
        "operator": "==",
        "value": "6"
      }
    },
    {
      "placeholderKey": "domain.augeg4.redis.host",
      "serviceKind": "redis",
      "version": {
        "minInclusive": "6.2",
        "maxExclusive": "8.0"
      }
    }
  ]
}
```

Per alternative tecnologiche:

```json
{
  "key": "TlcRawData",
  "purpose": "database:relational:connection-string",
  "valueType": "connectionString",
  "allowedProviders": [
    {
      "serviceKind": "postgresql",
      "requiredWhen": "dbDriver == 'PostgreSQL'"
    },
    {
      "serviceKind": "mssql",
      "requiredWhen": "dbDriver == 'SqlServer'"
    }
  ]
}
```

## Target di configurazione

Il target non e' solo il file. Deve descrivere anche il formato di scrittura:

- `application.properties`;
- `Config/AppSettings.config`;
- `Web.config`;
- `appsettings.json`;
- template Ansible Jinja2;
- environment variables;
- command line arguments;
- container labels/secrets;
- systemd service file;
- IIS binding;
- nginx/apache reverse proxy config.

Questa parte e' incompleta. In particolare mancano ancora regole per:

- firewall locale o cloud security group;
- reverse proxy nginx;
- reverse proxy Apache;
- IIS binding avanzati;
- TLS certificate binding;
- DNS/hostnames;
- porte interne/esterne nei container;
- configurazioni multi-file;
- file generati da template Ansible `.j2`;
- valori ereditati da inventory Ansible.

## Firewall e reverse proxy

Da completare.

Il deployment compiler non dovra' produrre solo il file applicativo finale. Dovra'
preparare anche richieste o azioni per configurazioni infrastrutturali correlate:

- aprire porte in firewall/security group;
- creare route reverse proxy;
- configurare virtual host nginx/apache;
- creare binding IIS;
- associare certificati;
- generare regole da applicare via Ansible/AWX.

Esempio:

```text
AugeG4.Engine master espone:
- HTTP 9980
- StatService 9000
- WebSocket 9292
- gRPC 9393
- JMX/RMI 7080

Iris deve sapere quali porte sono interne, quali esposte al cluster, quali pubblicate
verso altre application e quali richiedono reverse proxy.
```

## Stati suggeriti

Per rendere visibile il processo:

- `Manifest imported`
- `Application links required`
- `Application links completed`
- `Infrastructure bindings required`
- `Topology incomplete`
- `Ready to compile`
- `Compiled`

## Casi aperti

Questo documento e' da completare. In particolare mancano ancora:

- schema JSON definitivo del manifest 1.1/2.0;
- compatibilita' all'indietro con manifest 1.0;
- modello dominio per profili installativi e topology;
- modello dominio per configuration binding;
- UI di import con select delle application provider;
- UI di deployment con select delle installazioni provider;
- editor FE per valori tipizzati e liste;
- validatore per vincoli di versione software;
- mapping firewall/nginx/apache/IIS;
- gestione differenze tra configurazione di build, configurazione di deploy e
  configurazione runtime;
- strategia di rendering per file properties/XML/JSON/env/template;
- policy su quali valori possono essere salvati in DB e quali devono stare solo nel secret
  store.
