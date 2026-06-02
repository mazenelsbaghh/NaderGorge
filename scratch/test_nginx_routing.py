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
        # Check Nginx locally with Host header on HTTP
        "curl -I -H 'Host: bsma-academy.com' http://localhost/uploads/avatars/einstein.png",
        # Check Nginx locally with Host header on HTTPS (using -k)
        "curl -I -k https://127.0.0.1/uploads/avatars/einstein.png -H 'Host: bsma-academy.com'",
        # Check admin domain too
        "curl -I -k https://127.0.0.1/uploads/avatars/einstein.png -H 'Host: admin.bsma-academy.com'"
    ]
    
    for cmd in commands:
        print(f"\n--- {cmd} ---")
        stdin, stdout, stderr = ssh.exec_command(cmd)
        print(stdout.read().decode('utf-8'))
        print(stderr.read().decode('utf-8'))
        
    ssh.close()

if __name__ == '__main__':
    main()
