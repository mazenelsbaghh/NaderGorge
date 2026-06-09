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
        
    # Nginx config template for port 80 (Certbot will upgrade it to SSL)
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
}

server {
    listen 80;
    server_name app.bsma-academy.com student.bsma-academy.com;
    client_max_body_size 500M;
    location / {
        proxy_pass http://127.0.0.1:8739;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name admin.bsma-academy.com teacher.bsma-academy.com staff.bsma-academy.com super.bsma-academy.com;
    client_max_body_size 500M;
    location / {
        proxy_pass http://127.0.0.1:8740;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name api.bsma-academy.com;
    client_max_body_size 500M;
    location / {
        proxy_pass http://127.0.0.1:5245;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name ws.bsma-academy.com;
    location / {
        proxy_pass http://127.0.0.1:5245;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name assets.bsma-academy.com;
    location / {
        proxy_pass http://127.0.0.1:5245;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        add_header Access-Control-Allow-Origin "*";
        add_header Access-Control-Allow-Methods "GET, OPTIONS";
        add_header Access-Control-Allow-Headers "Range";
        add_header Access-Control-Expose-Headers "Content-Length, Content-Range";
        expires 30d;
        add_header Cache-Control "public, no-transform";
    }
}
"""

    sftp = ssh.open_sftp()
    
    # Steps to initialize the fresh server
    steps = [
        # 1. Update package list and install Git + Nginx + Certbot
        ("Installing Git, Nginx, Certbot", "apt-get update && apt-get install -y git curl nginx certbot python3-certbot-nginx"),
        
        # 2. Install Docker using the official convenience script
        ("Downloading Docker Installer", "curl -fsSL https://get.docker.com -o get-docker.sh"),
        ("Installing Docker", "sh get-docker.sh"),
        ("Starting & Enabling Docker Service", "systemctl enable docker --now"),
        
        # 3. Setup deploy folder & Git bare repository
        ("Creating application folders", "mkdir -p /var/www/nadergorge /var/www/nadergorge.git"),
        ("Initializing Git bare repository", "git init --bare /var/www/nadergorge.git")
    ]
    
    for desc, cmd in steps:
        print(f"\n⚡ {desc}...")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        exit_status = stdout.channel.recv_exit_status()
        print(f"Exit Status: {exit_status}")
        if exit_status != 0:
            print("STDERR:")
            print(stderr.read().decode('utf-8', errors='replace'))
            sftp.close()
            ssh.close()
            sys.exit(1)
            
    # 4. Write Nginx Config
    nginx_file_path = "/etc/nginx/sites-available/bsma-academy.com"
    print(f"\n⚡ Writing Nginx Config to {nginx_file_path}...")
    try:
        with sftp.file(nginx_file_path, "w") as f:
            f.write(nginx_config)
        print("Successfully wrote Nginx config.")
    except Exception as e:
        print(f"Failed to write Nginx config: {e}")
        sftp.close()
        ssh.close()
        sys.exit(1)
        
    sftp.close()
    
    # 5. Enable site, remove default, and reload Nginx
    nginx_steps = [
        ("Enabling bsma-academy.com site", "ln -sf /etc/nginx/sites-available/bsma-academy.com /etc/nginx/sites-enabled/"),
        ("Removing default Nginx site", "rm -f /etc/nginx/sites-enabled/default"),
        ("Testing Nginx configuration", "nginx -t"),
        ("Restarting Nginx service", "systemctl restart nginx")
    ]
    
    for desc, cmd in nginx_steps:
        print(f"\n⚡ {desc}...")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        exit_status = stdout.channel.recv_exit_status()
        print(f"Exit Status: {exit_status}")
        if exit_status != 0:
            print("STDERR:")
            print(stderr.read().decode('utf-8', errors='replace'))
            ssh.close()
            sys.exit(1)

    # 6. Run Certbot to generate SSL certificates
    print("\n⚡ Running Certbot to generate SSL certificates (this might take a minute)...")
    certbot_cmd = (
        "certbot --nginx "
        "-d bsma-academy.com "
        "-d www.bsma-academy.com "
        "-d app.bsma-academy.com "
        "-d student.bsma-academy.com "
        "-d admin.bsma-academy.com "
        "-d teacher.bsma-academy.com "
        "-d staff.bsma-academy.com "
        "-d super.bsma-academy.com "
        "-d api.bsma-academy.com "
        "-d ws.bsma-academy.com "
        "-d assets.bsma-academy.com "
        "--non-interactive --agree-tos --email mazen.elsbagh@gmail.com --redirect"
    )
    
    stdin, stdout, stderr = ssh.exec_command(certbot_cmd)
    exit_status = stdout.channel.recv_exit_status()
    print(f"Certbot Exit Status: {exit_status}")
    if exit_status != 0:
        print("STDERR:")
        print(stderr.read().decode('utf-8', errors='replace'))
    else:
        print("STDOUT:")
        print(stdout.read().decode('utf-8', errors='replace'))
        print("✅ SSL Certs created and Nginx configured successfully!")
        
    ssh.close()
    print("\n🎉 Fresh server setup complete!")

if __name__ == '__main__':
    main()
