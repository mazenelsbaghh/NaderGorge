import os

def search_files(directory, search_str):
    for root, dirs, files in os.walk(directory):
        if '.git' in root or 'bin' in root or 'obj' in root:
            continue
        for file in files:
            if not file.endswith(('.cs', '.ts', '.json', '.xml')):
                continue
            path = os.path.join(root, file)
            try:
                with open(path, 'r', encoding='utf-8') as f:
                    content = f.read()
                    if search_str.lower() in content.lower():
                        print(f"Found in {path}")
                        # print matching lines
                        lines = content.split('\n')
                        for idx, line in enumerate(lines):
                            if search_str.lower() in line.lower():
                                print(f"  Line {idx+1}: {line.strip()}")
            except Exception as e:
                pass

if __name__ == '__main__':
    print("Searching for 'telegram'...")
    search_files('/var/www/nadergorge/backend', 'telegram')
    print("\nSearching for 'Invalid provider'...")
    search_files('/var/www/nadergorge/backend', 'Invalid provider')
    print("\nSearching for 'telegram' in frontend...")
    search_files('/var/www/nadergorge/frontend', 'telegram')
