import paramiko
import sys

def main():
    hostname = "72.62.27.189"
    username = "root"
    password = "MazenElsbagh.12"
    
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    
    try:
        ssh.connect(hostname, username=username, password=password, timeout=15)
        print("Connected!")
    except Exception as e:
        print(f"Failed: {e}")
        sys.exit(1)
        
    commands = [
        "git --git-dir=/var/www/nadergorge.git log -n 1 --oneline",
        "ls -la /var/www/nadergorge/frontend/src/data/avatars.ts",
        "head -n 25 /var/www/nadergorge/frontend/src/data/avatars.ts",
        "docker ps"
    ]
    
    for cmd in commands:
        print(f"\n--- {cmd} ---")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        print(stdout.read().decode('utf-8'))
        print(stderr.read().decode('utf-8'))
        
    ssh.close()

if __name__ == '__main__':
    main()
