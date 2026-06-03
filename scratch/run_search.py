import paramiko
import sys

def main():
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
        
    sftp = ssh.open_sftp()
    
    # Upload search_code.py
    try:
        sftp.put('scratch/search_code.py', '/tmp/search_code.py')
    except Exception as e:
        print(f"Failed to upload search script: {e}")
        ssh.close()
        sys.exit(1)
        
    sftp.close()
    
    # Run the script
    stdin, stdout, stderr = ssh.exec_command('python3 /tmp/search_code.py')
    out = stdout.read().decode('utf-8', errors='replace')
    err = stderr.read().decode('utf-8', errors='replace')
    
    print("STDOUT:")
    print(out)
    if err:
        print("STDERR:")
        print(err)
        
    ssh.close()

if __name__ == '__main__':
    main()
