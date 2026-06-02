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
        
    commands = [
        "mkdir -p /var/www/actions-runner",
        "cd /var/www/actions-runner && curl -o actions-runner-linux-x64-2.334.0.tar.gz -L https://github.com/actions/runner/releases/download/v2.334.0/actions-runner-linux-x64-2.334.0.tar.gz",
        "cd /var/www/actions-runner && tar xzf ./actions-runner-linux-x64-2.334.0.tar.gz",
        # Configure the runner as root with RUNNER_ALLOW_RUNASROOT=1
        "cd /var/www/actions-runner && export RUNNER_ALLOW_RUNASROOT=1 && ./config.sh --url https://github.com/mazenelsbaghh/NaderGorge --token BKYWXQ2M76P4GBOVGV3ONOLKD334O --name nadergorge-vps-runner --unattended --replace",
        # Install as a systemd service so it runs persistently in the background
        "cd /var/www/actions-runner && ./svc.sh install root",
        "cd /var/www/actions-runner && ./svc.sh start"
    ]
    
    for cmd in commands:
        print(f"\n--- Running: {cmd} ---")
        # Set environment variables for the command session
        stdin, stdout, stderr = ssh.exec_command(f"export RUNNER_ALLOW_RUNASROOT=1 && {cmd}")
        
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
    print("\nRunner setup complete and started as a system service.")

if __name__ == '__main__':
    main()
