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
    
    # Nginx config template for massar-academy.net subdomains on port 80
    nginx_config = """# Nginx configurations for massar-academy.net

server {
    listen 80;
    server_name massar-academy.net www.massar-academy.net;
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
    server_name app.massar-academy.net;
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
    server_name admin.massar-academy.net super.massar-academy.net;
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
    server_name teacher.massar-academy.net;
    client_max_body_size 500M;
    location / {
        proxy_pass http://127.0.0.1:8741;
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
    server_name staff.massar-academy.net;
    client_max_body_size 500M;
    location / {
        proxy_pass http://127.0.0.1:8742;
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
    server_name api.massar-academy.net;
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
    server_name ws.massar-academy.net;
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
    server_name assets.massar-academy.net;
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

    nginx_file_path = "/etc/nginx/sites-available/massar-academy.net"
    print(f"Writing Nginx config to {nginx_file_path}...")
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
    
    # Enable site, disable old one, and reload Nginx
    nginx_steps = [
        ("Enabling massar-academy.net site", "ln -sf /etc/nginx/sites-available/massar-academy.net /etc/nginx/sites-enabled/"),
        ("Disabling old bsma-academy.com site", "rm -f /etc/nginx/sites-enabled/bsma-academy.com"),
        ("Testing Nginx configuration", "nginx -t"),
        ("Reloading Nginx service", "systemctl reload nginx")
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

    # Run Certbot to generate SSL certificates
    print("\n⚡ Running Certbot to generate SSL certificates for massar-academy.net (this might take a minute)...")
    certbot_cmd = (
        "certbot --nginx "
        "-d massar-academy.net "
        "-d www.massar-academy.net "
        "-d app.massar-academy.net "
        "-d admin.massar-academy.net "
        "-d teacher.massar-academy.net "
        "-d staff.massar-academy.net "
        "-d super.massar-academy.net "
        "-d api.massar-academy.net "
        "-d ws.massar-academy.net "
        "-d assets.massar-academy.net "
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
    print("\n🎉 Nginx and SSL setup complete!")

if __name__ == '__main__':
    main()
