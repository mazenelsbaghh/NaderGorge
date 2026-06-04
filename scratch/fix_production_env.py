import paramiko
import sys
import secrets
import re

def main():
    hostname = "72.62.27.189"
    username = "root"
    password = "MazenElsbagh.12"
    
    print("🚀 Connecting to production server...")
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    
    try:
        ssh.connect(hostname, username=username, password=password, timeout=30, look_for_keys=False, allow_agent=False)
        print("✅ Connected successfully to production server!")
    except Exception as e:
        print(f"❌ Failed to connect: {e}")
        sys.exit(1)

    env_path = "/var/www/nadergorge/.env"
    print(f"🔎 Reading remote .env file at {env_path}...")
    
    # Read the .env file from the remote VPS
    sftp = ssh.open_sftp()
    try:
        with sftp.file(env_path, "r") as f:
            content = f.read().decode('utf-8')
        print("✅ Successfully read remote .env file.")
    except Exception as e:
        print(f"❌ Failed to read remote .env file: {e}")
        sftp.close()
        ssh.close()
        sys.exit(1)

    # Check which variables are missing or use default placeholders
    required_vars = [
        "API_CALLBACK_SECRET",
        "AI_CALLBACK_SECRET",
        "PARENT_REPORT_SIGNING_SECRET",
        "WORKER_ADMIN_TOKEN"
    ]
    
    appended_vars = []
    lines = content.splitlines()
    
    # Simple parser for existing keys
    existing_keys = {}
    for line in lines:
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            parts = line.split("=", 1)
            existing_keys[parts[0].strip()] = parts[1].strip()

    # Generate secure values for any missing or unsafe keys
    for var in required_vars:
        val = existing_keys.get(var)
        # If missing or set to placeholder/default unsafe value
        if not val or val in ["change_me", "changeme", "CHANGE_ME", "secretxyz", ""]:
            # Generate a secure 40-character hex token (20 bytes)
            secure_val = secrets.token_hex(20)
            existing_keys[var] = secure_val
            appended_vars.append((var, secure_val))
            print(f"⚡ Variable '{var}' is missing or insecure. Generated new secure value.")

    if appended_vars:
        # Re-build the file or append to it
        new_lines = list(lines)
        new_lines.append("\n# Added automatically by deployment fix script")
        for var, val in appended_vars:
            new_lines.append(f"{var}={val}")
        
        updated_content = "\n".join(new_lines) + "\n"
        
        # Write it back to the remote server
        print("✍️ Writing updated .env file back to VPS...")
        try:
            with sftp.file(env_path, "w") as f:
                f.write(updated_content.encode('utf-8'))
            print("✅ Successfully updated .env file on VPS!")
        except Exception as e:
            print(f"❌ Failed to write remote .env file: {e}")
            sftp.close()
            ssh.close()
            sys.exit(1)
    else:
        print("💡 All required environment variables are already configured with safe values.")

    sftp.close()
    ssh.close()
    print("👋 Connection closed.")

if __name__ == '__main__':
    main()
