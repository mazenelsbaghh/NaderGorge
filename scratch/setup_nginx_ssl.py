import paramiko
import sys

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
    
    # 1. Nginx config contents
    nginx_config = """server {
    listen 80;
    server_name bsma-academy.com www.bsma-academy.com;

    client_max_body_size 500M;

    location / {
        proxy_pass http://127.0.0.1:8738;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:5245/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /swagger {
        proxy_pass http://127.0.0.1:5245/swagger;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
"""
    
    # 2. Install Nginx and certbot dependencies
    commands = [
        "apt-get update && apt-get install -y nginx certbot python3-certbot-nginx",
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
            
    # Write the nginx file
    nginx_file_path = "/etc/nginx/sites-available/bsma-academy.com"
    print(f"\nWriting Nginx config to {nginx_file_path}...")
    try:
        with sftp.file(nginx_file_path, "w") as f:
            f.write(nginx_config)
        print("Nginx config written successfully!")
    except Exception as e:
        print(f"Failed to write Nginx config: {e}")
        sftp.close()
        ssh.close()
        sys.exit(1)
        
    sftp.close()
    
    # 3. Enable site, test configuration and reload Nginx
    nginx_commands = [
        "ln -sf /etc/nginx/sites-available/bsma-academy.com /etc/nginx/sites-enabled/",
        "rm -f /etc/nginx/sites-enabled/default",
        "nginx -t",
        "systemctl reload nginx",
        "certbot --nginx -d bsma-academy.com -d www.bsma-academy.com --non-interactive --agree-tos --email mazen.elsbagh@gmail.com --redirect"
    ]
    
    for cmd in nginx_commands:
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
    print("\nNginx and SSL configuration complete.")

if __name__ == '__main__':
    main()
