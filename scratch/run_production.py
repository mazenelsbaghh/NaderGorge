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
    
    # 1. Read and update .env
    env_example_path = "/var/www/nadergorge/.env.example"
    try:
        with sftp.file(env_example_path, "r") as f:
            lines = f.readlines()
        print(".env.example read successfully.")
    except Exception as e:
        print(f"Failed to read .env.example: {e}")
        ssh.close()
        sys.exit(1)
        
    jwt_secret = generate_random_secret(32)
    callback_secret = generate_random_secret(16)
    
    new_lines = []
    for line in lines:
        if line.startswith("JWT_SECRET="):
            new_lines.append(f"JWT_SECRET={jwt_secret}\n")
        elif line.startswith("API_CALLBACK_SECRET="):
            new_lines.append(f"API_CALLBACK_SECRET={callback_secret}\n")
        elif line.startswith("ASPNETCORE_ENVIRONMENT="):
            new_lines.append("ASPNETCORE_ENVIRONMENT=Docker\n")
        elif line.startswith("NEXT_PUBLIC_API_URL="):
            # Change this to backend port 5245 which is exposed
            new_lines.append("NEXT_PUBLIC_API_URL=http://bsma-academy.com:5245/api\n")
        elif line.startswith("NEXT_PUBLIC_BACKEND_URL="):
            new_lines.append("NEXT_PUBLIC_BACKEND_URL=http://backend:5245\n")
        elif line.startswith("CORS_ALLOWED_ORIGINS="):
            # Allow origin without port since frontend is on port 80
            new_lines.append("CORS_ALLOWED_ORIGINS=http://bsma-academy.com\n")
        else:
            new_lines.append(line)
            
    env_path = "/var/www/nadergorge/.env"
    with sftp.file(env_path, "w") as f:
        f.writelines(new_lines)
    print(".env written successfully to server.")
    
    # 2. Read and modify docker-compose.yml to expose frontend on port 80
    dc_path = "/var/www/nadergorge/docker-compose.yml"
    try:
        with sftp.file(dc_path, "r") as f:
            dc_content = f.read().decode('utf-8')
        
        # Replace 8738:8738 with 80:8738 for frontend port mapping
        if '- "8738:8738"' in dc_content:
            dc_content = dc_content.replace('- "8738:8738"', '- "80:8738"')
            with sftp.file(dc_path, "w") as f:
                f.write(dc_content.encode('utf-8'))
            print("docker-compose.yml updated: frontend port mapped to 80.")
        else:
            print("Warning: could not find '- \"8738:8738\"' in docker-compose.yml")
    except Exception as e:
        print(f"Failed to modify docker-compose.yml: {e}")
        
    sftp.close()
    
    # 3. Build and run containers
    commands = [
        "cd /var/www/nadergorge && docker compose up -d --build",
        "cd /var/www/nadergorge && docker compose --profile migration run --rm migrator"
    ]
    
    for cmd in commands:
        print(f"\n--- Running: {cmd} ---")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        
        exit_status = stdout.channel.recv_exit_status()
        out = stdout.read().decode('utf-8', errors='replace')
        err = stderr.read().decode('utf-8', errors='replace')
        
        print(f"Exit Status: {exit_status}")
        if out:
            print("STDOUT:")
            print(out.strip())
        if err:
            print("STDERR:")
            print(err.strip())
            
    ssh.close()
    print("\nDeployment complete.")

if __name__ == '__main__':
    main()
