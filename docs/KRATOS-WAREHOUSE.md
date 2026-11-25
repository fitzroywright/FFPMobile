# Kratos installation and rollback

Install only on the internal organizational network. Use private DNS for `mobile.ffpja.org`. Replace the documentation addresses in the nginx template with the approved Kratos private bind address, Warehouse tablet ranges, VPN ranges and management subnet. Confirm the host firewall and upstream firewall reject public ingress. Configure private certificates trusted by tablets and by the local curl health probe. Kratos must reach the approved FFTPJ.LOCAL LDAPS controllers, OpenBao and the XAF evaluator. Only the evaluator requires access to JMDB01.

From Windows PowerShell, stage the non-secret templates on Kratos. Supply the authorized SSH username. Then connect to Kratos for the one-time setup.

```powershell
Set-Location D:\Projects\FFPMobile
$kratosUser = 'your-authorized-ssh-user'
scp -o "User=$kratosUser" -r .\deploy\kratos kratos:ffpmobile-bootstrap
ssh -l $kratosUser kratos
```

On Kratos, with administrator approval, install the ASP.NET Core .NET 11 runtime using the organizational package process. Confirm `dotnet --list-runtimes` includes the 11.0 ASP.NET Core runtime. The following commands prepare the install directories and restricted service account. Use the site's nginx layout if it differs from `sites-available` and `sites-enabled`.

```bash
sudo useradd --system --user-group --no-create-home --shell /usr/sbin/nologin aegis-ffpmobile-services
sudo install -d -m 0755 /opt/aegis-ffpmobile-services/releases
sudo install -d -m 0750 -o aegis-ffpmobile-services -g aegis-ffpmobile-services /var/lib/aegis-ffpmobile-services
sudo install -d -m 0750 -o root -g aegis-ffpmobile-services /etc/aegis/ffpmobile
sudo install -m 0755 ~/ffpmobile-bootstrap/aegis-deploy-ffpmobile /usr/local/sbin/aegis-deploy-ffpmobile
sudo install -m 0644 ~/ffpmobile-bootstrap/aegis-ffpmobile-services.service /etc/systemd/system/
sudo install -m 0640 -o root -g aegis-ffpmobile-services ~/ffpmobile-bootstrap/appsettings.private.example.json /etc/aegis/ffpmobile/appsettings.private.json
sudoedit /etc/aegis/ffpmobile/appsettings.private.json
sudo install -m 0644 ~/ffpmobile-bootstrap/mobile.ffpja.org.conf /etc/nginx/sites-available/mobile.ffpja.org.conf
sudoedit /etc/nginx/sites-available/mobile.ffpja.org.conf
sudo ln -s /etc/nginx/sites-available/mobile.ffpja.org.conf /etc/nginx/sites-enabled/mobile.ffpja.org.conf
sudo nginx -t
sudo systemctl reload nginx
sudo systemctl daemon-reload
```

Give only authorized deployment operators sudo access to the root-owned `/usr/local/sbin/aegis-deploy-ffpmobile` using the local Aegis deployment policy. The script accepts only `deploy` or `rollback` plus a generated release ID. Keep the service account isolated from other applications. Load the approved account bindings and set ownership of the original database file to the service identity if the import runs under a different privileged account.

## OpenBao

Run these commands from an authenticated OpenBao administrator shell with the approved policy file present. Use a dedicated AppRole and the single KV v2 read path in `ffpmobile-openbao.hcl`. Populate the `value` field with the service bearer issued by the internal XAF evaluator through the organization's secure secret input procedure.

```bash
bao policy write aegis-ffpmobile ffpmobile-openbao.hcl
bao write auth/approle/role/aegis-ffpmobile token_policies=aegis-ffpmobile token_ttl=15m token_max_ttl=1h token_num_uses=0 secret_id_num_uses=0 secret_id_ttl=24h
bao read auth/approle/role/aegis-ffpmobile/role-id
bao write -f auth/approle/role/aegis-ffpmobile/secret-id
```

Enter the RoleId and trusted OpenBao HTTPS endpoint in the protected JSON. Deliver the SecretId to `/etc/aegis/ffpmobile/openbao-secret-id`, owner `root:aegis-ffpmobile-services`, mode `0640`. This follows the Aegis.Operations bridge convention for a protected, reusable, rotating bootstrap file so health checks, service restarts and rollback can authenticate. Arrange rotation before the chosen TTL. Verify allowed read, denied cross-application access and successful reauthentication after a restart. `/health/ready` requires successful resolution of the service bearer and the evaluator's authenticated health response.

## Deploy from Windows

After the private endpoint, AD, account bindings, OpenBao and evaluator are commissioned, run this from PowerShell. The script tests, publishes for linux-x64, uploads the release, restarts the service and checks both HTTPS health endpoints. If health checks fail, it restores the previous release or stops an unsuccessful first installation.

```powershell
Set-Location D:\Projects\FFPMobile
.\scripts\Deploy-FFPMobile-Kratos.ps1 -HostName kratos -UserName $kratosUser
curl.exe --fail https://mobile.ffpja.org/health
curl.exe --fail https://mobile.ffpja.org/health/ready
```

Enable start on boot after the first healthy deployment.

```bash
sudo systemctl enable aegis-ffpmobile-services
sudo systemctl status aegis-ffpmobile-services
sudo journalctl -u aegis-ffpmobile-services -n 100
readlink /opt/aegis-ffpmobile-services/current
```

`/health` reports liveness. `/health/ready` checks the account and device stores, AD reachability via Common.Diagnostics and the authorized XAF readiness endpoint. Verify a trusted LDAPS bind during commissioning with an approved test employee. For rollback, use the release ID recorded before the attempted deployment. The account and tablet state directories are shared across releases, so retain protected backups and check schema compatibility before any future migration. A service restart expires existing mobile sessions.

```powershell
.\scripts\Deploy-FFPMobile-Kratos.ps1 -HostName kratos -UserName $kratosUser -RollbackRelease $previousReleaseId
curl.exe --fail https://mobile.ffpja.org/health/ready
```
