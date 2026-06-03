import paramiko
import sys

def main():
    if len(sys.argv) < 2:
        print("Usage: python ssh_cmd.py <command>")
        sys.exit(1)
        
    cmd = sys.argv[1]
    hostname = "72.62.27.189"
    username = "root"
    password = "MazenElsbagh.12"
    
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    
    try:
        ssh.connect(hostname, username=username, password=password, timeout=30)
    except Exception as e:
        print(f"Failed to connect: {e}")
        sys.exit(1)
        
    stdin, stdout, stderr = ssh.exec_command(cmd)
    
    exit_status = stdout.channel.recv_exit_status()
    out = stdout.read().decode('utf-8', errors='replace')
    err = stderr.read().decode('utf-8', errors='replace')
    
    ssh.close()
    
    if out:
        sys.stdout.write(out)
    if err:
        sys.stderr.write(err)
        
    sys.exit(exit_status)

if __name__ == '__main__':
    main()
