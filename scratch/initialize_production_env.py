import paramiko
import sys
import secrets

def generate_random_secret(length=32):
    return secrets.token_hex(length)

def main():
    hostname = "72.62.27.189"
    username = "root"
    password = "MazenElsbagh.12"
    
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    
    print(f"Connecting to {hostname}...")
    try:
        ssh.connect(hostname, username=username, password=password, timeout=15)
        print("Connected successfully!")
    except Exception as e:
        print(f"Failed to connect: {e}")
        sys.exit(1)
        
    sftp = ssh.open_sftp()
    
    # Generate random secrets for the production environment
    jwt_secret = generate_random_secret(32)
    api_callback_secret = generate_random_secret(24)
    ai_callback_secret = generate_random_secret(24)
    parent_report_signing_secret = generate_random_secret(32)
    worker_admin_token = generate_random_secret(32)
    postgres_password = generate_random_secret(16)
    
    env_content = f"""# =============================================================================
# Massar Platform — Docker Stack Production Environment Variables
# =============================================================================

# ─── 1. PostgreSQL ─────────────────────────────────────────────────────────
POSTGRES_USER=postgres
POSTGRES_PASSWORD={postgres_password}
POSTGRES_DB=massar_platform
POSTGRES_HOST=db
POSTGRES_PORT=5432
MASSAR_POSTGRES_PORT=5435

# ─── 2. Redis ───────────────────────────────────────────────────────────────
REDIS_HOST=redis
REDIS_PORT=6379
MASSAR_REDIS_PORT=6382

# ─── 3. Separated Runtime Ports ────────────────────────────────────────────
MASSAR_LANDING_PORT=8738
MASSAR_STUDENT_PORT=8739
MASSAR_ADMIN_PORT=8740
MASSAR_BACKEND_PORT=5245
MASSAR_WORKER_PORT=3001

LANDING_PUBLIC_ORIGIN=https://massar-academy.net
STUDENT_PUBLIC_ORIGIN=https://app.massar-academy.net
ADMIN_PUBLIC_ORIGIN=https://admin.massar-academy.net
NEXT_PUBLIC_APP_DOMAIN=massar-academy.net

# ─── 4. Backend (.NET) ──────────────────────────────────────────────────────
JWT_SECRET={jwt_secret}
JWT_ISSUER=MassarPlatformAPI
JWT_AUDIENCE=MassarPlatformClients
JWT_EXPIRY_MINUTES=60
JWT_REFRESH_DAYS=30

MAX_DEVICES_PER_STUDENT=2

ASPNETCORE_ENVIRONMENT=Docker
ASPNETCORE_URLS=http://+:5245
CORS_ALLOWED_ORIGINS=https://massar-academy.net,https://www.massar-academy.net,https://app.massar-academy.net,https://admin.massar-academy.net,https://teacher.massar-academy.net,https://staff.massar-academy.net,https://student.massar-academy.net,https://super.massar-academy.net

API_CALLBACK_SECRET={api_callback_secret}
AI_CALLBACK_SECRET={ai_callback_secret}
PARENT_REPORT_SIGNING_SECRET={parent_report_signing_secret}

EVOLUTION_API_BASE_URL=
EVOLUTION_API_KEY=
EVOLUTION_API_INSTANCE=Massar

# ─── 5. AI Worker (Node.js) ─────────────────────────────────────────────────
GEMINI_API_KEY=
WORKER_ADMIN_TOKEN={worker_admin_token}

# ─── 6. Frontend (Next.js) ──────────────────────────────────────────────────
NEXT_PUBLIC_API_URL=https://api.massar-academy.net/api
NEXT_PUBLIC_BACKEND_URL=https://api.massar-academy.net

INTERNAL_API_URL=http://backend:5245/api
INTERNAL_BACKEND_URL=http://backend:5245
WORKER_URL=http://worker:3001
"""

    env_path = "/var/www/nadergorge/.env"
    print(f"Writing production .env file to {env_path}...")
    try:
        with sftp.file(env_path, "w") as f:
            f.write(env_content)
        print("✅ Production .env written successfully!")
    except Exception as e:
        print(f"❌ Failed to write .env: {e}")
        sftp.close()
        ssh.close()
        sys.exit(1)
        
    sftp.close()
    ssh.close()
    print("Done!")

if __name__ == '__main__':
    main()
