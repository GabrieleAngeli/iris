# OpenBao and AWX: real (persistent) installation and configuration

Iris integrates with two core external services: **OpenBao** (where every real secret —
tokens, passwords, SSH keys — actually lives; Iris's own database only ever holds opaque
references) and **AWX** (which runs the Ansible jobs Iris prepares for application
deployments). This guide covers installing a real, persistent instance of each and plugging
the result into Iris via System settings → **Configure**.

This is **not** the same as the "Provision" button in Iris's System settings for OpenBao —
that starts a `-dev` container for quick local testing, and OpenBao's dev mode always uses an
in-memory backend: everything in it is lost on every restart, by design, regardless of
Docker volumes. Nothing below uses dev mode.

## Part 1 — OpenBao

### 1. Run it with persistent storage

Dev mode aside, OpenBao needs an explicit config file naming a real storage backend. `file`
is the simplest that doesn't need a cluster:

```hcl
# config.hcl
storage "file" {
  path = "/openbao/file"
}

listener "tcp" {
  address     = "0.0.0.0:8200"
  tls_disable = true   # fine on an internal/trusted network; enable TLS for anything reachable beyond that
}
```

```yaml
# docker-compose.yml
services:
  openbao:
    image: openbao/openbao:2.1
    container_name: openbao
    ports:
      - "8200:8200"
    cap_add:
      - IPC_LOCK          # lets OpenBao lock secrets out of swap
    volumes:
      - ./config.hcl:/openbao/config/config.hcl:ro
      - openbao-data:/openbao/file
    command: server -config=/openbao/config/config.hcl

volumes:
  openbao-data:
```

```powershell
docker compose up -d
```

### 2. Initialize and unseal

A freshly started `file`-backed OpenBao is uninitialized, then sealed — neither dev-mode
quirk applies here.

```powershell
docker exec -it openbao bao operator init
```

This prints 5 unseal key shares and the **root token**, once — save all of it somewhere safe
(a password manager, not this repo). Default threshold is 3 of 5: unseal with any three of
the five keys, one at a time —

```powershell
docker exec -it openbao bao operator unseal   # repeat 3x, a different key each time
```

Log in with the root token for the remaining one-time setup steps:

```powershell
docker exec -it openbao bao login   # paste the root token when prompted
```

### 3. Enable the KV v2 mount Iris expects

Iris's default "Configure OpenBao" values are mount path `secret`, KV **v2**. Unlike dev
mode, a real instance doesn't auto-enable this — check first, since re-enabling an
already-used path fails:

```powershell
docker exec -it openbao bao secrets list
```

If `secret/` isn't listed yet:

```powershell
docker exec -it openbao bao secrets enable -path=secret kv-v2
```

If `secret/` already exists as v1 from some other use, either upgrade it
(`bao kv enable-versioning secret/`) or enable a different mount name and use that same name
in Iris's "Mount path" field instead of `secret`.

### 4. Create a token scoped to what Iris actually touches — not the root token

The root token can do anything forever; Iris only ever reads/writes a handful of fixed
paths under the KV mount, one per integration/secret it manages:

| What | Path under `secret/` |
|---|---|
| OpenBao's own token | `openbao/token` |
| AWX token | `awx/token` |
| Azure DevOps PAT | `azure-devops/token` |
| Nexus token | `nexus/token` |
| SMTP password | `mail/smtp` |
| Server credentials | `servers/<serverId>/credentials/<credentialId>` |
| Managed data service passwords | `data-services/<instanceId>/password` |

A least-privilege policy for exactly that:

```hcl
# iris-secrets.hcl
path "secret/data/openbao/*"       { capabilities = ["create", "read", "update", "delete"] }
path "secret/data/awx/*"           { capabilities = ["create", "read", "update", "delete"] }
path "secret/data/azure-devops/*"  { capabilities = ["create", "read", "update", "delete"] }
path "secret/data/nexus/*"         { capabilities = ["create", "read", "update", "delete"] }
path "secret/data/mail/*"          { capabilities = ["create", "read", "update", "delete"] }
path "secret/data/servers/*"       { capabilities = ["create", "read", "update", "delete"] }
path "secret/data/data-services/*" { capabilities = ["create", "read", "update", "delete"] }
```

```powershell
docker cp iris-secrets.hcl openbao:/tmp/iris-secrets.hcl
docker exec -it openbao bao policy write iris-secrets /tmp/iris-secrets.hcl
docker exec -it openbao bao token create -policy=iris-secrets -period=768h
```

`-period` makes it a renewable, periodic token instead of one with a hard expiry — **Iris
never renews its OpenBao token itself** (it's a static value read once at startup), so a
token that silently expires will quietly break every secret read/write until someone
notices and pastes in a fresh one. A long period, or no expiry at all
(`-ttl=0 -period=0`, i.e. omit both), avoids that; if your security policy requires
expiring tokens, plan for someone to rotate it in Iris manually before it lapses.

## Part 2 — AWX

AWX's currently-supported installation path is the **AWX Operator on Kubernetes** — a
lightweight single-node cluster (k3s, or Docker Desktop's built-in Kubernetes) is enough for
a small/test deployment; it doesn't need to be the same host as Iris or OpenBao.

### 1. Install the AWX Operator

```powershell
git clone https://github.com/ansible/awx-operator.git
cd awx-operator
```

Check the [releases page](https://github.com/ansible/awx-operator/releases) for the current
tag and use it below instead of a hardcoded version — the operator moves fast enough that
pinning one here would go stale.

```powershell
git checkout tags/<latest-release-tag>
```

```yaml
# kustomization.yaml
apiVersion: kustomize.config.k8s.io/v1beta1
kind: Kustomization
resources:
  - github.com/ansible/awx-operator/config/default?ref=<latest-release-tag>
images:
  - name: quay.io/ansible/awx-operator
    newTag: <latest-release-tag>
namespace: awx
```

```powershell
kubectl create namespace awx
kubectl apply -k .
kubectl config set-context --current --namespace=awx
kubectl get pods -n awx   # wait until the operator pod is Running
```

### 2. Deploy an AWX instance

```yaml
# awx-demo.yml
apiVersion: awx.ansible.com/v1beta1
kind: AWX
metadata:
  name: awx-demo
spec:
  service_type: nodeport
```

Add it to `kustomization.yaml`'s `resources:` list, then:

```powershell
kubectl apply -k .
kubectl get pods -n awx -w   # wait for the awx-demo-* pods to be Running (can take several minutes the first time)
```

Retrieve the generated admin password:

```powershell
kubectl get secret awx-demo-admin-password -o jsonpath="{.data.password}" | base64 --decode
```

Find the NodePort to browse to:

```powershell
kubectl get svc awx-demo-service -n awx
```

### 3. Create a project, inventory, and job template

Log into the AWX web UI as `admin` with the password from above, then create:

- A **Project** pointing at wherever the deployment playbook lives (a git repo, or a local
  path AWX can see) — Iris's own default playbook name is `iris-deploy-application.yml`
  (see `IntegrationSettings.AnsiblePlaybook`; see also
  `docs/application-configuration-model-analysis.md` for what `iris_*` variables Iris feeds
  it — Iris produces the variable plan, the playbook/template renders and applies it).
- An **Inventory** for the target hosts.
- A **Job Template** that ties the project's playbook to that inventory.

Note the Job Template's numeric **ID** (visible in its URL, e.g.
`.../templates/job_template/7/details`, or via `GET /api/v2/job_templates/`) — that's
`AwxJobTemplateId` in Iris.

### 4. Create an API token for Iris

Iris authenticates to AWX with `Authorization: Bearer <token>` — an OAuth2 personal access
token, not the admin password itself. Either through the UI (your user → **Tokens** →
**Add**, scope **Write**) or directly:

```powershell
curl -u admin:<admin-password> -X POST https://<awx-host>/api/v2/users/1/personal_tokens/ `
  -H "Content-Type: application/json" `
  -d '{"description": "iris", "application": null, "scope": "write"}'
```

AWX shows the token value exactly once — copy it immediately, it isn't recoverable
afterward (create a new one if you lose it; Iris never sees or stores the admin password).

## Plug both into Iris

In Iris, sign in as a `platform.admin` and open **System settings**. Each row's
**Configure** button opens a dialog for exactly these values:

| Value | OpenBao | AWX |
|---|---|---|
| Endpoint | `https://<openbao-host>:8200` | `https://<awx-host>` |
| Token | the `iris-secrets` token from Part 1 step 4 | the personal access token from Part 2 step 4 |
| Mount path (OpenBao only) | `secret` (or whatever you chose in step 3) | — |
| Use KV v2 (OpenBao only) | on | — |
| Job template id (AWX only) | — | the numeric ID from Part 2 step 3 |

Saving always returns "restart required" — neither takes effect in the *running* Iris.Api
process until it restarts (both are locked into a startup-time options singleton by design,
see `ActiveIntegrationSnapshot`'s remarks in the codebase). This is true even for OpenBao's
own token: if OpenBao wasn't already the active secret store when you saved it, that token
landed in Iris's encrypted fallback vault instead, and needs one **Unlock** (System
settings → enter your Iris password) after the restart before OpenBao is genuinely live.

## Verify

1. Restart Iris.Api with both configured.
2. `GET /system/settings` (or just reload System settings in the app) — `restartRequired`
   should be `false` for both once the restart has actually happened, and their status
   should read `Configured` rather than `Pending restart`.
3. Click **Test** on each row — this makes a real, live reachability call (OpenBao's
   `/v1/sys/health`, AWX's `/api/v2/ping/`) and should report `Reachable`. Iris also probes
   both automatically every few minutes in the background (`Iris:HealthCheck:IntervalMinutes`)
   and shows "Checked Xm ago" once that's run at least once.
4. Save something that actually goes through OpenBao — e.g. re-save the SMTP password from
   **Edit SMTP** — then confirm it under `secret/mail/smtp` (or wherever your mount is) in
   OpenBao's own UI/API. If it isn't there, OpenBao wasn't yet the active secret store for
   that save; see the fallback-vault note above.
5. From Deployments, launch a real installation against the AWX job template you created and
   confirm a run shows up in that job template's **Jobs** tab in the AWX UI.

## Known limitations

- Iris never renews or rotates either token itself — plan for a long-lived/periodic OpenBao
  token (see Part 1 step 4) and keep an eye on AWX token expiry if you set one.
- A secret saved to OpenBao before it was the active store (e.g. during first-run setup, or
  any save made before a restart that activates it) sits in Iris's own encrypted fallback
  vault until an admin unlocks it — it does **not** retroactively appear in OpenBao. Re-save
  it once OpenBao is confirmed active if you want it to live there instead.
- Creating the AWX project/inventory/job template content itself (what the playbook actually
  does) is outside Iris's scope — Iris only prepares the `iris_*` variable plan and launches
  the job template by ID; see `docs/application-configuration-model-analysis.md`.

Sources: [OpenBao `operator init`](https://openbao.org/docs/commands/operator/init/),
[OpenBao KV v2 secrets engine](https://openbao.org/docs/secrets/kv/kv-v2/),
[OpenBao Docker image](https://hub.docker.com/r/openbao/openbao),
[AWX Operator basic install](https://docs.ansible.com/projects/awx-operator/en/latest/installation/basic-install.html),
[AWX OAuth2 token authentication](https://docs.ansible.com/projects/awx/en/24.6.1/administration/oauth2_token_auth.html).
