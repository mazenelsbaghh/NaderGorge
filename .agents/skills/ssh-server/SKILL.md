---
name: ssh-server
description: Connect and manage the Nader Gorge production server via SSH. Use for deployments, server maintenance, file transfers, service management, Docker operations, and any remote server tasks. Trigger when user mentions "السيرفر", "server", "deploy", "production", "الديبلوي", "الدهول", "سيرفر", or server-related operations.
---

# SSH Server Skill — Nader Gorge Production

This skill provides structured workflows for connecting to and managing the Nader Gorge production server.

## 🚀 Quick Deploy (Recommended)

Use the built-in deploy script for one-command deployments:

```bash
# Full deploy: push to git + migrate + rebuild Docker
bash ".agents/skills/ssh-server/scripts/deploy.sh"

# Deploy without running migrations
bash ".agents/skills/ssh-server/scripts/deploy.sh" --no-migrate

# Run migrations only (no push, no rebuild)
bash ".agents/skills/ssh-server/scripts/deploy.sh" --migrate-only
```

**The script does automatically:**
1. 📤 Push current branch → GitHub (`origin`)
2. 📤 Push current branch → Production server (`prod`)
3. 🔄 Checkout latest code on server
4. 🗄️ Run EF Core migrations (via `migrator` Docker profile)
5. 🐳 Rebuild & restart all Docker containers
6. ✅ Health-check all containers

---

## Server Credentials

```
hostname = "72.62.27.189"
username = "root"
password = "MazenElsbagh.12"
```

## Connection Command

```bash
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189
```

> If `sshpass` is not installed on the local machine, install it first:
> ```bash
> brew install sshpass   # macOS
> ```

## Quick Connect (Interactive)

```bash
ssh root@72.62.27.189
# Password: MazenElsbagh.12
```

---

## Preparation Steps

Before executing any remote task:

1. **Verify connectivity** — Ping the server first:
   ```bash
   ping -c 3 72.62.27.189
   ```

2. **Check local `sshpass`** — Required for non-interactive scripts:
   ```bash
   which sshpass || brew install sshpass
   ```

3. **Define a helper alias** for this session (optional):
   ```bash
   alias srun='sshpass -p "MazenElsbagh.12" ssh -o StrictHostKeyChecking=no root@72.62.27.189'
   ```

---

## Common Workflows

### 1. Run a Single Remote Command

```bash
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 '<command>'
```

Example — check disk space:
```bash
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'df -h'
```

### 2. Upload a File (SCP)

```bash
sshpass -p 'MazenElsbagh.12' scp -o StrictHostKeyChecking=no <local_file> root@72.62.27.189:<remote_path>
```

Example — upload docker-compose:
```bash
sshpass -p 'MazenElsbagh.12' scp -o StrictHostKeyChecking=no docker-compose.yml root@72.62.27.189:/app/nader-gorge/
```

### 3. Download a File from Server

```bash
sshpass -p 'MazenElsbagh.12' scp -o StrictHostKeyChecking=no root@72.62.27.189:<remote_path> <local_destination>
```

### 4. Sync a Directory (rsync)

```bash
sshpass -p 'MazenElsbagh.12' rsync -avz --progress -e "ssh -o StrictHostKeyChecking=no" <local_dir>/ root@72.62.27.189:<remote_dir>/
```

### 5. Docker Operations

```bash
# List running containers
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker ps'

# Pull and restart all services
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'cd /app/nader-gorge && docker compose pull && docker compose up -d'

# View logs for a service
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker compose -f /app/nader-gorge/docker-compose.yml logs --tail=100 <service_name>'

# Restart a single service
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker compose -f /app/nader-gorge/docker-compose.yml restart <service_name>'
```

### 6. Full Deployment Workflow

```bash
# Step 1: Build locally (if needed)
# Step 2: Upload updated files
sshpass -p 'MazenElsbagh.12' scp -o StrictHostKeyChecking=no docker-compose.yml root@72.62.27.189:/app/nader-gorge/

# Step 3: Pull latest images and redeploy
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 '
  cd /app/nader-gorge
  docker compose pull
  docker compose up -d --remove-orphans
  docker compose ps
'
```

### 7. Check Server Health

```bash
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 '
  echo "=== CPU & Memory ===" && top -bn1 | head -20
  echo "=== Disk ===" && df -h
  echo "=== Docker ===" && docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
  echo "=== Memory ===" && free -h
'
```

### 8. View Application Logs

```bash
# Backend logs
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker compose -f /app/nader-gorge/docker-compose.yml logs --tail=200 -f backend'

# Frontend logs  
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker compose -f /app/nader-gorge/docker-compose.yml logs --tail=200 -f frontend'
```

### 9. Database Operations

```bash
# Connect to PostgreSQL
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker compose -f /app/nader-gorge/docker-compose.yml exec postgres psql -U postgres nadergorge'

# Run a migration
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker compose -f /app/nader-gorge/docker-compose.yml exec backend dotnet ef database update'

# Backup database
sshpass -p 'MazenElsbagh.12' ssh -o StrictHostKeyChecking=no root@72.62.27.189 'docker compose -f /app/nader-gorge/docker-compose.yml exec postgres pg_dump -U postgres nadergorge > /backups/nadergorge_$(date +%Y%m%d_%H%M%S).sql'
```

---

## Execution Rules

1. **Always show the command before running it** — let the user review it.
2. **Use `sshpass`** for automated/scripted operations to avoid interactive password prompts.
3. **Always use `-o StrictHostKeyChecking=no`** to bypass host key prompts on first connect.
4. **Prefer multi-line heredoc scripts** for complex multi-step remote operations.
5. **Check exit codes** — after any deployment, run `docker compose ps` to verify all containers are healthy.
6. **Never store credentials in committed files** — the credentials in this skill file are for local agent use only.

---

## SSH Key Setup (One-Time, Recommended)

To avoid password prompts permanently:

```bash
# Generate key if not exists
ssh-keygen -t ed25519 -C "nader-gorge-prod" -f ~/.ssh/nader_gorge_prod

# Copy key to server
sshpass -p 'MazenElsbagh.12' ssh-copy-id -o StrictHostKeyChecking=no -i ~/.ssh/nader_gorge_prod.pub root@72.62.27.189

# Test passwordless login
ssh -i ~/.ssh/nader_gorge_prod root@72.62.27.189
```

Then update `~/.ssh/config`:
```
Host nader-gorge-prod
    HostName 72.62.27.189
    User root
    IdentityFile ~/.ssh/nader_gorge_prod
    StrictHostKeyChecking no
```

After setup, connect simply with:
```bash
ssh nader-gorge-prod
```
